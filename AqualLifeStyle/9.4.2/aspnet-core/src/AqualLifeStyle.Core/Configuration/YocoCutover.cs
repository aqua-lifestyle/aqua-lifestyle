using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Configuration
{
    // The two artifacts share this protocol; only the baseline artifact carries
    // the assembly marker. Configuration cannot turn that artifact into V2.
    public sealed class YocoCutoverOptions
    {
        // A reload cannot silently drop an established fence or change its
        // operator. Deactivation requires a checked restart after Normal.
        private sealed class Selection { public bool Enabled; public long Operator; }
        private static readonly ConditionalWeakTable<IConfiguration, Selection> Selections = new();
        public const string Prefix = "App:YocoCutover:";
        public bool CompatibilityReceiver { get; }
        public bool Enabled { get; }
        public long OperatorUserId { get; }
        public YocoCutoverOptions(IConfiguration configuration)
        {
            CompatibilityReceiver = Assembly.GetEntryAssembly()?.GetCustomAttribute<YocoReceiverArtifactAttribute>() != null;
            var selected = configuration[Prefix + "Enabled"];
            if (CompatibilityReceiver && selected != "true") throw new InvalidOperationException("YOCO_RECEIVER_MUST_REMAIN_ENABLED");
            if (selected != null && selected != "true" && selected != "false") throw new InvalidOperationException("YOCO_CUTOVER_SELECTION_INVALID");
            var selection = Selections.GetValue(configuration, _ => new Selection());
            lock (selection)
            {
                Enabled = CompatibilityReceiver || selected == "true" || selection.Enabled;
                if (Enabled)
                {
                    // Forced capture needs no application login. Privileged
                    // operations remain unavailable without a pinned operator.
                    var operatorValue = configuration[Prefix + "OperatorUserId"];
                    long id = 0;
                    if (operatorValue != null && (!long.TryParse(operatorValue, out id) || id <= 0) ||
                        !CompatibilityReceiver && id <= 0 ||
                        selection.Enabled && selection.Operator != id)
                        throw new InvalidOperationException("YOCO_CUTOVER_OPERATOR_REQUIRED");
                    OperatorUserId = id; selection.Enabled = true; selection.Operator = id;
                }
            }
            if (!Enabled) return;
            foreach (var key in new[] { "App:BackgroundJobs:Enabled", "App:WeeklyCommissions:Enabled",
                "App:EntryMonthlyObligations:Enabled", "App:AutomaticPayouts:Enabled", "AQGreenV2Demo:Enabled", "AQGreenV2Pilot:Enabled", "SEED_DEMO_DATA" })
                if (configuration.GetValue<bool>(key)) throw new InvalidOperationException("YOCO_CUTOVER_AUTOMATION_MUST_BE_HELD");
            if (ApplicationRecoveryMode.IsEnabled(configuration)) throw new InvalidOperationException("YOCO_CUTOVER_REQUIRES_SEPARATE_RECEIVE_ONLY_MODE");
            if (CompatibilityReceiver && configuration.GetValue<bool>("App:FinancialOperations:Enabled"))
                throw new InvalidOperationException("YOCO_RECEIVER_FINANCIAL_OPERATIONS_MUST_BE_HELD");
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class YocoReceiverArtifactAttribute : Attribute { }

    public static class YocoCutoverDispatch
    {
        private static readonly AsyncLocal<bool> Authorized = new();
        private static readonly AsyncLocal<bool> Business = new();
        private static readonly AsyncLocal<bool> ReadOnly = new();
        public static bool IsAuthorized => Authorized.Value;
        public static bool IsBusinessAuthorized => Business.Value;
        public static bool IsReadOnlyAuthorized => ReadOnly.Value;
        public static IDisposable EnterReadOnly()
        {
            var prior = ReadOnly.Value; ReadOnly.Value = true;
            return new Scope(() => ReadOnly.Value = prior);
        }
        public static IDisposable EnterBusiness()
        {
            var prior = Business.Value; Business.Value = true;
            return new Scope(() => Business.Value = prior);
        }
        public static IDisposable Enter()
        {
            var prior = Authorized.Value; Authorized.Value = true;
            return new Scope(() => Authorized.Value = prior);
        }
        private sealed class Scope : IDisposable
        {
            private readonly Action _exit;
            public Scope(Action exit) => _exit = exit;
            public void Dispose() => _exit();
        }
    }

    // Explicit member reads only. HTTP GET is not itself proof of read-only
    // behavior: GetMyInvitations, for example, creates invitation records.
    // ABP permission, ownership and Tenant checks still run for every method.
    public static class YocoCutoverMemberReads
    {
        private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
        {
            "AQGreenPilotAppService.GetStatusAsync",
            "MyAccountAppService.GetProfileAsync",
            "CustomerAppService.GetMyCustomerAsync",
            "MembershipAppService.GetAllAsync",
            "MembershipAppService.GetActiveTiersAsync",
            "MembershipAppService.GetAsync",
            "MembershipAppService.GetTierBenefitsAsync",
            "MembershipAppService.GetSavingsWindowStatuses",
            "ProductAppService.GetAllAsync",
            "ProductAppService.GetAllForCustomerAsync",
            "ProductAppService.GetAsync",
            "OrderIntentAppService.GetMineAsync",
            "EnquiryAppService.GetMineAsync",
            "ClubMemberProgrammeParticipationAppService.GetMyParticipationsAsync",
            "ClubMemberProgrammeProgressAppService.GetMyJourneyAsync",
            "ClubMemberProgrammeProgressAppService.GetMyProgressAsync",
            "ClubMemberSavingsAppService.GetMyAccountAsync",
            "ClubMemberOnyxLoanAppService.GetMyAgreementsAsync",
            "ClubMemberEntryMonthlyObligationAppService.GetMyObligationsAsync"
        };
        private static readonly HashSet<string> Paths = BuildPaths();
        private static HashSet<string> BuildPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var operation in Operations)
            {
                var parts = operation.Split('.');
                var service = parts[0].Substring(0, parts[0].Length - "AppService".Length);
                var method = parts[1].EndsWith("Async", StringComparison.Ordinal) ? parts[1][..^5] : parts[1];
                paths.Add("/api/services/app/" + service + "/" + method);
            }
            return paths;
        }
        public static bool AllowsOperation(string operation) => Operations.Contains(operation);
        public static bool AllowsRoute(string method, string path) => method == "GET" && path != null && Paths.Contains(path);
    }

    public sealed class YocoCutoverEvent
    {
        [JsonIgnore] public long Id { get; set; }
        public string Contract { get; set; } = "Aqua.YocoCutover.EventV1";
        public string EventId { get; set; }
        public string PayloadHash { get; set; }
        public string BodyBase64 { get; set; }
        public string Signature { get; set; }
        public string SignatureTimestamp { get; set; }
        public DateTimeOffset VerifiedAtUtc { get; set; }
        public bool RequiresReconciliation { get; set; }
        public string Status { get; set; } = "Pending";
        public string HoldReason { get; set; }
        public string ReconciliationDigest { get; set; }
        public long? ReplayedByUserId { get; set; }
        public DateTimeOffset? ReplayedAtUtc { get; set; }
    }

    public sealed class YocoCutoverSnapshot
    {
        public string Phase { get; set; } = "Uninitialized";
        public bool HasMigration55 { get; set; }
        public List<YocoCutoverPhaseEvidence> PhaseEvidence { get; set; } = new();
        public List<YocoCutoverEvent> Events { get; } = new();
    }
    public sealed class YocoCutoverPhaseEvidence
    {
        public string Phase { get; set; }
        public long OperatorUserId { get; set; }
        public DateTimeOffset CommittedAtUtc { get; set; }
        public string ReconciliationDigest { get; set; }
    }

    public interface IYocoCutoverStore
    {
        // Commits only after action and every joined business write succeed.
        Task<T> TransactAsync<T>(Func<YocoCutoverSnapshot, Task<T>> action);
        // Shared session lease drains HTTP writers before an exclusive phase change.
        Task<IAsyncDisposable> EnterBusinessAsync();
        // Reviewed HTTP reads join a bounded, server-enforced read-only UOW.
        // It is rolled back, never used as authorization for business writes.
        Task ReadOnlyAsync(Func<Task> read);
    }
}
