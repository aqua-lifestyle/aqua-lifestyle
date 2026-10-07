using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AqualLifeStyle.EntityFrameworkCore
{
    internal static class AQGreenBackfillSchemaGuard
    {
        internal static async Task VerifyAsync(AqualLifeStyleDbContext context, CancellationToken ct)
        {
            var physicalTables = await RowsAsync(context,"""
                SELECT jsonb_build_object('name',r.relname,'kind',r.relkind,'rls',r.relrowsecurity)::text
                FROM pg_class r JOIN pg_namespace n ON n.oid=r.relnamespace
                WHERE n.nspname='public' AND r.relname IN
                  ('AQGreenPlacementTreeScopes','AQGreenNetworkPlacements','EntryParticipations',
                   'EntryRecruiterCorrections','EntryParticipationApprovalDecisions','MemberPayments',
                   'AQGreenJoiningCheckouts','YocoWebhookReceipts','Customers','AbpUsers','AbpTenants')
                """,ct);
            Require(physicalTables.Count == 11 && physicalTables.All(t=>Text(t,"kind")=="r" && !t.GetProperty("rls").GetBoolean()),
                "Source/topology table identity or RLS visibility is not the required unfiltered contract.");
            var constraints = await RowsAsync(context, """
                SELECT jsonb_build_object('table',r.relname,'name',c.conname,'type',c.contype,
                  'valid',c.convalidated,'deferred',c.condeferrable,
                  'columns',ARRAY(SELECT a.attname FROM unnest(c.conkey) WITH ORDINALITY k(attnum,ord)
                    JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.attnum ORDER BY k.ord),
                  'foreignTable',fr.relname,'foreignSchema',fn.nspname,
                  'foreignColumns',ARRAY(SELECT a.attname FROM unnest(c.confkey) WITH ORDINALITY k(attnum,ord)
                    JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.attnum ORDER BY k.ord),
                  'deleteAction',c.confdeltype,'checkExpression',pg_get_expr(c.conbin,c.conrelid))::text
                FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace
                LEFT JOIN pg_class fr ON fr.oid=c.confrelid LEFT JOIN pg_namespace fn ON fn.oid=fr.relnamespace
                WHERE n.nspname='public' AND r.relname IN ('AQGreenPlacementTreeScopes','AQGreenNetworkPlacements')
                """, ct);
            foreach (var type in new[] { typeof(AQGreenPlacementTreeScope), typeof(AQGreenNetworkPlacement) })
            {
                var entity = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.FindEntityType(type);
                foreach (var key in entity.GetKeys())
                {
                    var actual = constraints.SingleOrDefault(c => Text(c,"name") == key.GetName() && Text(c,"table") == entity.GetTableName());
                    Require(actual.ValueKind != JsonValueKind.Undefined && actual.GetProperty("valid").GetBoolean() &&
                            Text(actual,"type") == (key.IsPrimaryKey()?"p":"u") &&
                            Strings(actual,"columns").SequenceEqual(key.Properties.Select(p => p.GetColumnName())), "Required key unavailable or changed.");
                }
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var actual = constraints.SingleOrDefault(c => Text(c,"name") == foreign.GetConstraintName() && Text(c,"table") == entity.GetTableName());
                    Require(actual.ValueKind != JsonValueKind.Undefined && actual.GetProperty("valid").GetBoolean() && Text(actual,"type")=="f" &&
                            Strings(actual,"columns").SequenceEqual(foreign.Properties.Select(p=>p.GetColumnName())) &&
                            Text(actual,"foreignTable")==foreign.PrincipalEntityType.GetTableName() && Text(actual,"foreignSchema")=="public" &&
                            Strings(actual,"foreignColumns").SequenceEqual(foreign.PrincipalKey.Properties.Select(p=>p.GetColumnName())) &&
                            Text(actual,"deleteAction")=="r", "Required Tenant-qualified restrictive foreign key unavailable or changed.");
                }
                foreach (var check in entity.GetCheckConstraints())
                {
                    var actual = constraints.SingleOrDefault(c => Text(c,"name")==check.Name && Text(c,"table")==entity.GetTableName());
                    Require(actual.ValueKind != JsonValueKind.Undefined && actual.GetProperty("valid").GetBoolean() && Text(actual,"type")=="c" &&
                            CheckDefinitions.TryGetValue(check.Name, out var expected) && Compact(Text(actual,"checkExpression"))==Compact(expected),
                        "Required check constraint unavailable or changed.");
                }
            }
            var indexes = await RowsAsync(context, """
                SELECT jsonb_build_object('table',r.relname,'name',ix.relname,'unique',i.indisunique,'valid',i.indisvalid,
                  'ready',i.indisready,'columns',ARRAY(SELECT a.attname FROM unnest(i.indkey) WITH ORDINALITY k(attnum,ord)
                    JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum ORDER BY k.ord),
                  'filter',pg_get_expr(i.indpred,i.indrelid))::text
                FROM pg_index i JOIN pg_class r ON r.oid=i.indrelid JOIN pg_namespace n ON n.oid=r.relnamespace
                JOIN pg_class ix ON ix.oid=i.indexrelid
                WHERE n.nspname='public' AND r.relname IN ('AQGreenPlacementTreeScopes','AQGreenNetworkPlacements')
                """, ct);
            foreach(var type in new[] { typeof(AQGreenPlacementTreeScope), typeof(AQGreenNetworkPlacement) })
            {
                var entity=context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.FindEntityType(type);
                foreach(var index in entity.GetIndexes().Where(i=>i.IsUnique))
                {
                    var actual=indexes.SingleOrDefault(i=>Text(i,"name")==index.GetDatabaseName() && Text(i,"table")==entity.GetTableName());
                    Require(actual.ValueKind!=JsonValueKind.Undefined && actual.GetProperty("unique").GetBoolean() && actual.GetProperty("valid").GetBoolean() && actual.GetProperty("ready").GetBoolean() &&
                            Strings(actual,"columns").SequenceEqual(index.Properties.Select(p=>p.GetColumnName())) &&
                            Compact(Text(actual,"filter")??"")==Compact(IndexFilterDefinitions.TryGetValue(index.GetDatabaseName(),out var filter)?filter:""),
                        "Required unique root/participant/slot index unavailable or changed.");
                }
                // Check validity of backing primary/alternate-key indexes too.
                Require(indexes.Where(i=>Text(i,"table")==entity.GetTableName()).All(i=>i.GetProperty("valid").GetBoolean() && i.GetProperty("ready").GetBoolean()),
                    "Invalid topology index prevents safe application.");
            }
            var triggers=await RowsAsync(context,"""
                SELECT jsonb_build_object('table',r.relname,'name',t.tgname,'enabled',t.tgenabled,
                  'type',t.tgtype,'deferred',t.tgdeferrable,'initiallyDeferred',t.tginitdeferred,
                  'function',p.proname,'functionSchema',pn.nspname,'body',p.prosrc,'securityDefiner',p.prosecdef,
                  'configuration',p.proconfig,'rls',r.relrowsecurity)::text
                FROM pg_trigger t JOIN pg_class r ON r.oid=t.tgrelid JOIN pg_namespace n ON n.oid=r.relnamespace
                JOIN pg_proc p ON p.oid=t.tgfoid JOIN pg_namespace pn ON pn.oid=p.pronamespace
                WHERE n.nspname='public' AND r.relname IN ('AQGreenPlacementTreeScopes','AQGreenNetworkPlacements') AND NOT t.tgisinternal
                """,ct);
            var expectedTriggers=new[] {
                ("TR_AQGreenNetworkPlacements_ValidateInsert","AQGreenNetworkPlacements","ValidateAQGreenNetworkPlacementInsert",7,false),
                ("TR_AQGreenNetworkPlacements_AppendOnly","AQGreenNetworkPlacements","PreventAQGreenPlacementTopologyMutation",27,false),
                ("TR_AQGreenNetworkPlacements_PreventTruncate","AQGreenNetworkPlacements","PreventAQGreenPlacementTopologyMutation",34,false),
                ("TR_AQGreenPlacementTreeScopes_RequireRoot","AQGreenPlacementTreeScopes","EnsureAQGreenPlacementTreeScopeHasOneRoot",5,true),
                ("TR_AQGreenPlacementTreeScopes_AppendOnly","AQGreenPlacementTreeScopes","PreventAQGreenPlacementTopologyMutation",27,false),
                ("TR_AQGreenPlacementTreeScopes_PreventTruncate","AQGreenPlacementTreeScopes","PreventAQGreenPlacementTopologyMutation",34,false)
            };
            Require(triggers.Count==expectedTriggers.Length,"Unexpected/missing topology trigger requires review.");
            var assembly=context.GetService<IMigrationsAssembly>();
            var migration=assembly.CreateMigration(assembly.Migrations["20260825095740_AddAQGreenPlacementV2Foundation"],context.Database.ProviderName);
            var sql=string.Join("\n",migration.UpOperations.OfType<SqlOperation>().Select(o=>o.Sql));
            var bodies=Regex.Matches(sql,"CREATE FUNCTION \"(?<name>[^\"]+)\"\\(\\).*?AS \\$function\\$(?<body>.*?)\\$function\\$;",RegexOptions.Singleline)
                .ToDictionary(m=>m.Groups["name"].Value,m=>Compact(m.Groups["body"].Value));
            foreach(var expected in expectedTriggers)
            {
                var actual=triggers.SingleOrDefault(t=>Text(t,"name")==expected.Item1 && Text(t,"table")==expected.Item2);
                Require(actual.ValueKind!=JsonValueKind.Undefined && Text(actual,"enabled")=="O" && actual.GetProperty("type").GetInt32()==expected.Item4 &&
                        actual.GetProperty("deferred").GetBoolean()==expected.Item5 && actual.GetProperty("initiallyDeferred").GetBoolean()==expected.Item5 &&
                        Text(actual,"function")==expected.Item3 && Text(actual,"functionSchema")=="public" && !actual.GetProperty("securityDefiner").GetBoolean() &&
                        !actual.GetProperty("rls").GetBoolean() && Strings(actual,"configuration").SequenceEqual(new[]{"search_path=pg_catalog"}) &&
                        bodies.TryGetValue(expected.Item3,out var body) && Compact(Text(actual,"body"))==body,
                    "Required append-only/root/path trigger unavailable, disabled or altered.");
            }
        }

        // PostgreSQL canonical expressions for the unchanged foundation migration.
        // Parentheses are preserved: changed logical grouping must fail closed.
        private static readonly Dictionary<string,string> CheckDefinitions = new()
        {
            ["CK_AQGreenPlacementTreeScopes_TenantId_Positive"] = "(\"TenantId\" > 0)",
            ["CK_AQGreenNetworkPlacements_TenantId_Positive"] = "(\"TenantId\" > 0)",
            ["CK_AQGreenNetworkPlacements_RootOrNonRootShape"] = "(((\"PlacementParentParticipantId\" IS NULL) AND (\"PlacementSlot\" IS NULL) AND (\"CanonicalPath\" = ''::text)) OR ((\"PlacementParentParticipantId\" IS NOT NULL) AND (\"PlacementSlot\" IS NOT NULL) AND (\"CanonicalPath\" <> ''::text)))",
            ["CK_AQGreenNetworkPlacements_PlacementSlot_Range"] = "((\"PlacementSlot\" IS NULL) OR ((\"PlacementSlot\" >= 1) AND (\"PlacementSlot\" <= 5)))",
            ["CK_AQGreenNetworkPlacements_NoSelfParent"] = "((\"PlacementParentParticipantId\" IS NULL) OR (\"ParticipantId\" <> \"PlacementParentParticipantId\"))",
            ["CK_AQGreenNetworkPlacements_CanonicalPath_Characters"] = "(length(replace(replace(replace(replace(replace(\"CanonicalPath\", '1'::text, ''::text), '2'::text, ''::text), '3'::text, ''::text), '4'::text, ''::text), '5'::text, ''::text)) = 0)",
            ["CK_AQGreenNetworkPlacements_RulesVersion_NotBlank"] = "(length(TRIM(BOTH FROM \"RulesVersion\")) > 0)"
        };
        private static readonly Dictionary<string,string> IndexFilterDefinitions = new()
        {
            ["IX_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId"] = "(\"PlacementParentParticipantId\" IS NULL)",
            ["IX_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId_Plac~"] = "((\"PlacementParentParticipantId\" IS NOT NULL) AND (\"PlacementSlot\" IS NOT NULL))"
        };
        // Ignore formatting only outside SQL literals/quoted identifiers. Whitespace
        // inside a literal may change a constraint or trigger's actual semantics.
        private static string Compact(string value) => Regex.Replace(value??"",
            "'(?:[^']|'')*'|\"(?:[^\"]|\"\")*\"|\\s+",
            match => string.IsNullOrWhiteSpace(match.Value) ? "" : match.Value);
        private static string Text(JsonElement row,string key) => row.GetProperty(key).ValueKind==JsonValueKind.Null?null:row.GetProperty(key).GetString();
        private static IEnumerable<string> Strings(JsonElement row,string key) => row.GetProperty(key).EnumerateArray().Select(v=>v.GetString());
        private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
        private static async Task<List<JsonElement>> RowsAsync(AqualLifeStyleDbContext context,string sql,CancellationToken ct)
        {
            var result=new List<JsonElement>();
            await using var command=new NpgsqlCommand(sql,(NpgsqlConnection)context.Database.GetDbConnection(),(NpgsqlTransaction)context.Database.CurrentTransaction.GetDbTransaction());
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct)) { using var json=JsonDocument.Parse(reader.GetString(0)); result.Add(json.RootElement.Clone()); }
            return result;
        }
    }
}
