using System.Diagnostics;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace AqualLifeStyle.Customer11Backfill.Tests;

[CollectionDefinition("Customer11 PostgreSQL", DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture> { }

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string _container = "aqgreen-customer11-writer-" + Guid.NewGuid().ToString("N");
    private readonly string _template = "customer11_template_" + Guid.NewGuid().ToString("N");
    private int _port;
    public byte[] Input { get; private set; } = null!;
    public byte[] Output { get; private set; } = null!;
    public byte[] Capture { get; private set; } = null!;
    public AQGreenCustomer11BackfillPlan Plan { get; private set; } = null!;
    private string Admin => ConnectionString("postgres");
    private string ConnectionString(string database) => new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1", Port = _port, Database = database, Username = "fixture",
        Password = "local-disposable-only", Pooling = false, IncludeErrorDetail = false
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        Input = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_INPUT") ?? throw new InvalidOperationException("Protected accepted input path required; no production data is committed as a test fixture."));
        Output = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_OUTPUT") ?? throw new InvalidOperationException("Protected accepted output path required."));
        Capture = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("AQGREEN_CUSTOMER11_ACCEPTED_CAPTURE") ?? throw new InvalidOperationException("Protected source capture path required."));
        Plan = AQGreenCustomer11BackfillPlan.Load(Input, Output, Capture);
        await Docker("run", "-d", "--name", _container, "-e", "POSTGRES_DB=postgres", "-e", "POSTGRES_USER=fixture", "-e", "POSTGRES_PASSWORD=local-disposable-only", "-p", "127.0.0.1::5432", "postgres@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873");
        var binding = (await Docker("port", _container, "5432/tcp")).Trim();
        _port = int.Parse(binding.Split(':').Last());
        for (var i=0;i<60;i++)
        {
            try { await using var c=new NpgsqlConnection(Admin); await c.OpenAsync(); break; }
            catch when(i<59) { await Task.Delay(500); }
        }
        await Sql(Admin, $"CREATE DATABASE \"{_template}\";");
        await using(var context=Context(ConnectionString(_template))) await context.Database.MigrateAsync();
        await Seed(ConnectionString(_template));
    }
    public async Task DisposeAsync() => await Docker("rm", "-fv", _container);

    public AqualLifeStyleDbContext Context(string dsn, params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AqualLifeStyleDbContext>().UseNpgsql(dsn).AddInterceptors(interceptors).Options);
    public async Task<Lease> NewDatabase()
    {
        var name="customer11_test_"+Guid.NewGuid().ToString("N");
        await Sql(Admin,$"CREATE DATABASE \"{name}\" TEMPLATE \"{_template}\";");
        return new Lease(this,name,ConnectionString(name));
    }
    public async Task<AQGreenBackfillTargetIdentity> Target(string dsn)
    {
        await using var c=new NpgsqlConnection(dsn); await c.OpenAsync();
        await using var cmd=new NpgsqlCommand("SELECT current_database(), current_user, host(inet_server_addr()), inet_server_port()",c);
        await using var r=await cmd.ExecuteReaderAsync(); await r.ReadAsync();
        return new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetInt32(3),Plan.SourceSnapshotSha256);
    }
    public static async Task Sql(string dsn,string sql,params NpgsqlParameter[] parameters)
    {
        await using var c=new NpgsqlConnection(dsn); await c.OpenAsync();
        await using var cmd=new NpgsqlCommand(sql,c); cmd.Parameters.AddRange(parameters); await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<T> Scalar<T>(string dsn,string sql)
    {
        await using var c=new NpgsqlConnection(dsn); await c.OpenAsync();
        await using var cmd=new NpgsqlCommand(sql,c); return (T)(await cmd.ExecuteScalarAsync())!;
    }
    public sealed class Lease(PostgreSqlFixture fixture,string name,string dsn) : IAsyncDisposable
    {
        public string Dsn { get; }=dsn;
        public async ValueTask DisposeAsync() => await Sql(fixture.Admin,$"DROP DATABASE \"{name}\" WITH (FORCE);");
    }
    private async Task Seed(string dsn)
    {
        await Sql(dsn,"""
            INSERT INTO "AbpTenants" ("Id","TenancyName","Name","IsActive","CreationTime","IsDeleted")
            VALUES (1,'fixture-one','Fixture One',true,NOW(),false),(2,'fixture-two','Fixture Two',true,NOW(),false);
            INSERT INTO "Areas" ("Id","TenantId","Code","Name","IsActive","CreationTime","IsDeleted")
            VALUES ('cccccccc-0000-0000-0000-000000000001',1,'FIXTURE','Fixture',true,NOW(),false);
            INSERT INTO "AbpUsers" ("Id","TenantId","UserName","EmailAddress","Name","Surname","NormalizedUserName","NormalizedEmailAddress","Password","Role","IsEmailConfirmed","IsActive","CreationTime","IsDeleted","AccessFailedCount","IsLockoutEnabled","IsPhoneNumberConfirmed","IsTwoFactorEnabled")
            VALUES (9000,NULL,'fixture-actor','actor@example.test','Fixture','Actor','FIXTURE-ACTOR','ACTOR@EXAMPLE.TEST','fixture-only',1,true,true,TIMESTAMPTZ '2026-01-01',false,0,false,false,false);
            """);
        foreach(var actor in Plan.Members.Where(m=>m.CapturedApprovingActorId.HasValue).GroupBy(m=>m.CapturedApprovingActorId).Select(g=>g.First()))
            await Sql(dsn,"""
                INSERT INTO "AbpUsers" ("Id","TenantId","UserName","EmailAddress","Name","Surname","NormalizedUserName","NormalizedEmailAddress","Password","Role","IsEmailConfirmed","IsActive","CreationTime","IsDeleted","AccessFailedCount","IsLockoutEnabled","IsPhoneNumberConfirmed","IsTwoFactorEnabled")
                VALUES (@id,NULL,@name,@email,'Fixture','Actor',upper(@name),upper(@email),'fixture-only',1,true,true,@created,false,0,false,false,false);
                """,new NpgsqlParameter("id",actor.CapturedApprovingActorId!.Value),new NpgsqlParameter("name","fixture-actor-"+actor.CapturedApprovingActorId),new NpgsqlParameter("email",$"fixture-actor-{actor.CapturedApprovingActorId}@example.test"),new NpgsqlParameter("created",actor.CapturedActorCreationTimeUtc!.Value));
        foreach(var m in Plan.Members)
        {
            var pay=m.CapturedJoiningPaymentId; var checkout=m.CapturedCheckoutId; var external="fixture-pay-"+m.CustomerId; var providerCheckout="fixture-checkout-"+m.CustomerId;
            // Exercise the real domain's uppercase representation in every baseline test.
            var receipt=YocoWebhookReceipt.Record(1,"fixture-event-"+m.CustomerId,external,
                providerCheckout,new string('a',64),YocoCheckoutProgramme.AQGreen,checkout,m.CapturedReceiptProcessedAtUtc);
            await Sql(dsn,"""
                INSERT INTO "AbpUsers" ("Id","TenantId","UserName","EmailAddress","Name","Surname","NormalizedUserName","NormalizedEmailAddress","Password","Role","IsEmailConfirmed","IsActive","CreationTime","IsDeleted","AccessFailedCount","IsLockoutEnabled","IsPhoneNumberConfirmed","IsTwoFactorEnabled")
                VALUES (@user,1,@username,@email,'Fixture','Member',upper(@username),upper(@email),'fixture-only',3,true,true,@start,false,0,true,false,false);
                INSERT INTO "Customers" ("Id","TenantId","Name","Email","AreaId","IsActive","CreationTime","ClubMemberNumber","UserId","IsDeleted")
                VALUES (@customer,1,'Fixture Member',@email,'cccccccc-0000-0000-0000-000000000001',true,@start,@username,@user,false);
                INSERT INTO "MemberPayments" ("Id","TenantId","CustomerId","Purpose","Amount","Currency","Provider","ExternalReference","Status","InitiatedAt","ConfirmedAt","CreationTime","IsDeleted")
                VALUES (@pay,1,@customer,7,@amount,'ZAR','YOCO',@external,1,@start,@confirmed,@start,false);
                INSERT INTO "EntryParticipations" ("Id","TenantId","CustomerId","RecruiterCustomerId","Status","StartedAt","ActivatedAt","JoiningPaymentId","JoiningPaymentSchedule","TermsVersion","TermsEffectiveFrom","JoiningPaymentAmount","JoiningInstallmentAmount","RegistrationPaymentAmount","ActivationPaymentAmount","MonthlyCommitmentAmount","GracePeriodDays","Currency","CreationTime","IsDeleted")
                VALUES (@participant,1,@customer,@recruiter,2,@start,@activated,@pay,0,'fixture-terms',TIMESTAMPTZ '2026-01-01',@amount,0,0,0,600,7,'ZAR',@start,false);
                INSERT INTO "AQGreenJoiningCheckouts" ("Id","TenantId","ParticipationId","CustomerId","Schedule","Stage","Amount","Currency","Status","ProviderCheckoutId","CheckoutUrl","CreatedAt","PaymentId","CompletedAt","CreationTime","IsDeleted")
                VALUES (@checkout,1,@participant,@customer,0,0,@amount,'ZAR',2,@provider,'https://example.test/fixture',@start,@pay,@confirmed,@start,false);
                INSERT INTO "YocoWebhookReceipts" ("Id","TenantId","EventId","PaymentId","ProviderCheckoutId","PayloadHash","Programme","CheckoutReferenceId","ProcessedAt","CreationTime")
                VALUES (@receipt,1,@event,@external,@provider,@hash,0,@checkout,@processed,@processed);
                """,
                new NpgsqlParameter("user",m.CapturedUserId),new NpgsqlParameter("username","fixture-"+m.CustomerId),new NpgsqlParameter("email",$"fixture-{m.CustomerId}@example.test"),
                new NpgsqlParameter("customer",m.CustomerId),new NpgsqlParameter("start",m.StartedAtUtc),new NpgsqlParameter("pay",pay),new NpgsqlParameter("amount",m.JoiningAmount),new NpgsqlParameter("external",external),
                new NpgsqlParameter("confirmed",m.PaymentConfirmedAtUtc),new NpgsqlParameter("participant",m.ParticipantId),new NpgsqlParameter("activated",m.ActivatedAtUtc),
                new NpgsqlParameter("recruiter",NpgsqlTypes.NpgsqlDbType.Integer){Value=(object?)m.RecruiterCustomerId??DBNull.Value},
                new NpgsqlParameter("checkout",checkout),new NpgsqlParameter("provider",providerCheckout),new NpgsqlParameter("receipt",m.CapturedReceiptId),new NpgsqlParameter("processed",m.CapturedReceiptProcessedAtUtc),new NpgsqlParameter("event",receipt.EventId),new NpgsqlParameter("hash",receipt.PayloadHash));
            if(m.RequiresRetainedPositiveApproval)
                await Sql(dsn,"INSERT INTO \"EntryParticipationApprovalDecisions\" (\"Id\",\"EntryParticipationId\",\"AdministratorUserId\",\"Approved\",\"DecidedAt\") VALUES (@id,@participant,@actor,true,@activated)",new NpgsqlParameter("id",m.CapturedApprovalDecisionId!.Value),new NpgsqlParameter("participant",m.ParticipantId),new NpgsqlParameter("actor",m.CapturedApprovingActorId!.Value),new NpgsqlParameter("activated",m.ActivatedAtUtc));
        }
    }
    private static async Task<string> Docker(params string[] args)
    {
        var start=new ProcessStartInfo("docker"){RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args) start.ArgumentList.Add(arg);
        using var process=Process.Start(start)??throw new InvalidOperationException("Docker unavailable");
        var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); var result=await stdout; var error=await stderr;
        if(process.ExitCode!=0) throw new InvalidOperationException("Disposable Docker fixture failed: "+error);
        return result;
    }
}
