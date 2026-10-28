using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Configuration;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Payments.Yoco
{
    public interface IYocoCutoverReleaseGate { Task VerifyAsync(); }
    public sealed class YocoCutoverReleaseGate : IYocoCutoverReleaseGate, ITransientDependency
    {
        private readonly AqualLifeStyle.Application.ProgrammeParticipations.AQGreenD10Runtime _runtime;
        public YocoCutoverReleaseGate(AqualLifeStyle.Application.ProgrammeParticipations.AQGreenD10Runtime runtime) => _runtime = runtime;
        public async Task VerifyAsync()
        { if (!await _runtime.ForTenantAsync(1, true)) throw new InvalidOperationException("YOCO_CUTOVER_D10_RELEASE_REQUIRED"); }
    }
    public interface IYocoCutoverDispatcher
    {
        Task DispatchAsync(VerifiedYocoPaymentNotification notification, int? expectedTenantId);
    }
    public sealed class YocoCutoverDispatcher : IYocoCutoverDispatcher, ITransientDependency
    {
        private readonly YocoPaymentNotificationProcessor _processor;
        public YocoCutoverDispatcher(YocoPaymentNotificationProcessor processor) => _processor = processor;
        public async Task DispatchAsync(VerifiedYocoPaymentNotification notification, int? expectedTenantId)
        {
            if (expectedTenantId.HasValue) await _processor.VerifyCutoverTenantAsync(notification, expectedTenantId.Value);
            await _processor.ProcessAsync(notification);
        }
    }

    // Authoritative merchant records remain human-reviewed, not inferred from
    // application/test flags. The separately authorized deployment pins the file.
    public sealed class YocoCutoverReconciliation
    {
        public string Contract { get; set; }
        public string Authority { get; set; }
        public long OperatorUserId { get; set; }
        public DateTimeOffset AcceptedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public string AquaAcceptanceReference { get; set; }
        public string OwnerAuthorizationReference { get; set; }
        public string CapturedEventSetSha256 { get; set; }
        public bool? OutstandingReferencesReconciled { get; set; }
        public bool? UnresolvedObligationsRemain { get; set; }
        public List<Entry> Entries { get; set; }
        public sealed class Entry
        {
            public string EventId { get; set; }
            public string PayloadHash { get; set; }
            public int TenantId { get; set; }
            public string PaymentId { get; set; }
            public string ProviderCheckoutId { get; set; }
            public int AmountInCents { get; set; }
            public string Currency { get; set; }
            public string Mode { get; set; }
            public string EventType { get; set; }
            public string Disposition { get; set; }
            public string AuthoritativeRecordReference { get; set; }
            public string EvidenceSha256 { get; set; }
        }
    }
    public interface IYocoCutoverReconciliationSource { (YocoCutoverReconciliation Manifest, string Digest) Read(); }
    public sealed class YocoCutoverReconciliationSource : IYocoCutoverReconciliationSource, ITransientDependency
    {
        private readonly IConfiguration _configuration;
        public YocoCutoverReconciliationSource(IConfiguration configuration) => _configuration = configuration;
        public (YocoCutoverReconciliation, string) Read()
        {
            var path = _configuration[YocoCutoverOptions.Prefix + "ReconciliationPath"];
            var hash = _configuration[YocoCutoverOptions.Prefix + "ReconciliationSha256"];
            if (string.IsNullOrWhiteSpace(path) || !YocoCutoverService.Digest(hash)) throw new InvalidOperationException("YOCO_RECONCILIATION_PIN_REQUIRED");
            var file = new FileInfo(Path.GetFullPath(path));
            if (!file.Exists || file.Length > 1024 * 1024 || file.LinkTarget != null) throw new InvalidOperationException("YOCO_RECONCILIATION_CUSTODY_REQUIRED");
            for (var d = file.Directory; d != null; d = d.Parent)
                if (d.LinkTarget != null || File.Exists(Path.Combine(d.FullName, ".git")) || Directory.Exists(Path.Combine(d.FullName, ".git")))
                    throw new InvalidOperationException("YOCO_RECONCILIATION_CUSTODY_REQUIRED");
            if (!OperatingSystem.IsWindows())
            {
                const UnixFileMode forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                if ((File.GetUnixFileMode(file.FullName) & forbidden) != 0 || (File.GetUnixFileMode(file.Directory.FullName) & forbidden) != 0)
                    throw new InvalidOperationException("YOCO_RECONCILIATION_CUSTODY_REQUIRED");
            }
            var bytes = File.ReadAllBytes(file.FullName);
            if (YocoCutoverService.Hash(bytes) != hash) throw new InvalidOperationException("YOCO_RECONCILIATION_PIN_CHANGED");
            using var doc = JsonDocument.Parse(bytes); RejectDuplicates(doc.RootElement);
            return (JsonSerializer.Deserialize<YocoCutoverReconciliation>(bytes), hash);
        }
        internal static void RejectDuplicates(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            { var keys = new HashSet<string>(StringComparer.Ordinal); foreach (var p in e.EnumerateObject())
                { if (!keys.Add(p.Name)) throw new InvalidOperationException("YOCO_RECONCILIATION_DUPLICATE_FIELD"); RejectDuplicates(p.Value); } }
            else if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) RejectDuplicates(item);
        }
    }

    public sealed class YocoCutoverService : ITransientDependency
    {
        private readonly IYocoCutoverStore _store;
        private readonly IYocoWebhookSignatureVerifier _verifier;
        private readonly IYocoCutoverDispatcher _dispatcher;
        private readonly IYocoCutoverReconciliationSource _reconciliation;
        private readonly IConfiguration _configuration;
        private readonly IYocoCutoverReleaseGate _release;
        public YocoCutoverService(IYocoCutoverStore store, IYocoWebhookSignatureVerifier verifier, IYocoCutoverDispatcher dispatcher,
            IYocoCutoverReconciliationSource reconciliation, IConfiguration configuration, IYocoCutoverReleaseGate release)
        { _store = store; _verifier = verifier; _dispatcher = dispatcher; _reconciliation = reconciliation; _configuration = configuration; _release = release; }

        public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        public static string EventSetHash(IEnumerable<YocoCutoverEvent> events) => Hash(JsonSerializer.SerializeToUtf8Bytes(
            events.OrderBy(x => x.EventId, StringComparer.Ordinal).Select(x => new { x.EventId, x.PayloadHash })));
        public static bool Digest(string s) => s != null && s != new string('0',64) && Regex.IsMatch(s,"\\A[0-9a-f]{64}\\z");
        public bool Enabled => new YocoCutoverOptions(_configuration).Enabled;
        public bool CompatibilityReceiver => new YocoCutoverOptions(_configuration).CompatibilityReceiver;

        public async Task ReceiveAsync(byte[] raw, string eventId, string timestamp, string signature)
        {
            var options = new YocoCutoverOptions(_configuration);
            if (!options.Enabled || raw == null || raw.Length > 64 * 1024 || string.IsNullOrWhiteSpace(eventId) || eventId.Length > 256 ||
                signature?.Length > 4096 || timestamp?.Length > 32) throw new InvalidOperationException("YOCO_CAPTURE_INPUT_INVALID");
            if (!_verifier.IsConfigured || !_verifier.IsValid(eventId, timestamp, signature, raw))
                throw new UnauthorizedAccessException("YOCO_SIGNATURE_REJECTED");
            var hash = Hash(raw);
            // Capture is always its own committed transaction, even if later
            // normal processing crashes or the HTTP client disconnects.
            await _store.TransactAsync(async s =>
            {
                var old = s.Events.SingleOrDefault(x => x.EventId == eventId);
                if (old != null)
                { if (old.PayloadHash != hash) throw new InvalidOperationException("YOCO_CAPTURE_CONFLICT"); return true; }
                s.Events.Add(new YocoCutoverEvent { EventId = eventId, PayloadHash = hash, BodyBase64 = Convert.ToBase64String(raw),
                    Signature = signature, SignatureTimestamp = timestamp, VerifiedAtUtc = DateTimeOffset.UtcNow,
                    RequiresReconciliation = options.CompatibilityReceiver || s.Phase != "Normal" || s.Events.Any(Unfinished) });
                return await Task.FromResult(true);
            });
            if (!options.CompatibilityReceiver)
                await TryNormalAsync(eventId);
        }

        private async Task TryNormalAsync(string eventId)
        {
            try
            {
                await _store.TransactAsync(async s =>
                {
                    var e = s.Events.Single(x => x.EventId == eventId);
                    if (s.Phase != "Normal" || e.Status != "Pending" || e.RequiresReconciliation) return false;
                    using (YocoCutoverDispatch.Enter()) await _dispatcher.DispatchAsync(Parse(e), null);
                    e.Status = "Applied"; return true;
                });
            }
            catch { await HoldAsync(eventId, "NORMAL_PROCESSING_REQUIRES_RECONCILIATION"); }
        }

        public Task<YocoCutoverSnapshot> StatusAsync() => _store.TransactAsync(s => Task.FromResult(s));
        public Task<bool> SetPhaseAsync(string phase) => _store.TransactAsync(async s =>
        {
            var options = new YocoCutoverOptions(_configuration);
            if (!options.Enabled || options.OperatorUserId <= 0 || !new[] { "ReceiveOnly", "Replay", "Normal" }.Contains(phase) ||
                options.CompatibilityReceiver && phase != "ReceiveOnly" || phase != "ReceiveOnly" && !s.HasMigration55)
                throw new InvalidOperationException("YOCO_CUTOVER_PHASE_NOT_AUTHORIZED");
            if (phase == "Normal" && (s.Phase != "Replay" || s.Events.Any(Unfinished)))
                throw new InvalidOperationException("YOCO_CUTOVER_UNRESOLVED_EVENTS");
            string digest = null;
            if (phase == "Normal")
            {
                var (m, pin) = _reconciliation.Read();
                RequireAuthority(m, options.OperatorUserId);
                if (!Digest(pin) || m.CapturedEventSetSha256 != EventSetHash(s.Events) ||
                    m.OutstandingReferencesReconciled != true || m.UnresolvedObligationsRemain != false)
                    throw new InvalidOperationException("YOCO_RELEASE_RECONCILIATION_REQUIRED");
                await _release.VerifyAsync(); digest = pin;
            }
            s.PhaseEvidence.Add(new YocoCutoverPhaseEvidence { Phase = phase, OperatorUserId = options.OperatorUserId,
                CommittedAtUtc = DateTimeOffset.UtcNow, ReconciliationDigest = digest });
            s.Phase = phase; return true;
        });

        public async Task<string> ReplayAsync(long id, long operatorUserId)
        {
            var options = new YocoCutoverOptions(_configuration);
            if (!options.Enabled || options.CompatibilityReceiver || operatorUserId != options.OperatorUserId)
                throw new InvalidOperationException("YOCO_REPLAY_OPERATOR_REQUIRED");
            string eventId = null;
            try
            {
                return await _store.TransactAsync(async s =>
                {
                    if (s.Phase != "Replay" || !s.HasMigration55) throw new InvalidOperationException("YOCO_REPLAY_PHASE_REQUIRED");
                    var e = s.Events.Single(x => x.Id == id); eventId = e.EventId;
                    if (!Unfinished(e)) return e.Status;
                    var n = Parse(e); var (m, digest) = _reconciliation.Read();
                    var now = DateTimeOffset.UtcNow;
                    RequireAuthority(m, operatorUserId);
                    if (!Digest(digest)) throw new InvalidOperationException("YOCO_RECONCILIATION_PIN_REQUIRED");
                    var entry = m.Entries.SingleOrDefault(x => x.EventId == e.EventId);
                    if (entry == null || entry.Disposition != "MATCHED" || entry.PayloadHash != e.PayloadHash || entry.TenantId <= 0 ||
                        entry.PaymentId != n.PaymentId || entry.ProviderCheckoutId != CheckoutId(n) || entry.AmountInCents != n.AmountInCents ||
                        entry.Currency != n.Currency || entry.Mode != n.Mode || entry.EventType != n.EventType ||
                        !Digest(entry.EvidenceSha256) || string.IsNullOrWhiteSpace(entry.AuthoritativeRecordReference))
                        throw new InvalidOperationException("YOCO_AUTHORITATIVE_RECONCILIATION_REQUIRED");
                    using (YocoCutoverDispatch.Enter()) await _dispatcher.DispatchAsync(n, entry.TenantId);
                    e.Status = n.Mode == "test" ? "RetainedTest" : "Applied";
                    e.HoldReason = null; e.ReconciliationDigest = digest; e.ReplayedByUserId = operatorUserId; e.ReplayedAtUtc = now;
                    return e.Status;
                });
            }
            catch
            {
                if (eventId == null) throw new InvalidOperationException("YOCO_REPLAY_NOT_AVAILABLE");
                await HoldAsync(eventId, "AUTHORITATIVE_RECONCILIATION_OR_BUSINESS_BASELINE_REQUIRED");
                return "Held";
            }
        }

        private Task<bool> HoldAsync(string eventId, string reason) => _store.TransactAsync(s =>
        { var e = s.Events.Single(x => x.EventId == eventId); if (Unfinished(e)) { e.Status = "Held"; e.HoldReason = reason; }
            return Task.FromResult(true); });
        public static bool Unfinished(YocoCutoverEvent e) => e.Status != "Applied" && e.Status != "RetainedTest";
        private static void RequireAuthority(YocoCutoverReconciliation m, long operatorId)
        {
            var now = DateTimeOffset.UtcNow;
            if (m == null || m.Contract != "Aqua.YocoCutover.ReconciliationV1" ||
                m.Authority != "OPERATOR_ATTESTED_AUTHORITATIVE_YOCO_RECORDS" || m.OperatorUserId != operatorId ||
                string.IsNullOrWhiteSpace(m.AquaAcceptanceReference) || string.IsNullOrWhiteSpace(m.OwnerAuthorizationReference) ||
                m.AcceptedAtUtc > now || m.ExpiresAtUtc <= now || m.ExpiresAtUtc > m.AcceptedAtUtc.AddDays(1) || m.Entries == null ||
                m.Entries.Any(x => x == null) || m.Entries.GroupBy(x => x.EventId).Any(g => g.Count() != 1))
                throw new InvalidOperationException("YOCO_RECONCILIATION_AUTHORITY_REQUIRED");
        }
        private static string CheckoutId(VerifiedYocoPaymentNotification n) => n.Metadata != null &&
            n.Metadata.TryGetValue(YocoCheckoutMetadata.ProviderCheckoutId, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        public static VerifiedYocoPaymentNotification Parse(YocoCutoverEvent e)
        {
            var raw = Convert.FromBase64String(e.BodyBase64);
            if (Hash(raw) != e.PayloadHash) throw new InvalidOperationException("YOCO_CAPTURE_INTEGRITY_REQUIRED");
            using var document = JsonDocument.Parse(raw);
            YocoCutoverReconciliationSource.RejectDuplicates(document.RootElement);
            var data = JsonSerializer.Deserialize<Envelope>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var p = data?.Payload;
            if (p == null || data.Type is not ("payment.succeeded" or "payment.failed") || p.Type != "payment" ||
                p.Status != (data.Type == "payment.succeeded" ? "succeeded" : "failed") || p.Amount <= 0 ||
                string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Currency) || p.Mode is not ("test" or "live") || p.CreatedDate == default)
                throw new InvalidOperationException("YOCO_EVENT_REQUIRES_RECONCILIATION");
            return new VerifiedYocoPaymentNotification { EventId = e.EventId, EventType = data.Type, PaymentId = p.Id,
                AmountInCents = p.Amount, Currency = p.Currency, Mode = p.Mode, ConfirmedAt = p.CreatedDate,
                PayloadHash = e.PayloadHash, Metadata = p.Metadata };
        }
        private sealed class Envelope { public string Type { get; set; } public Payment Payload { get; set; } }
        private sealed class Payment
        {
            public string Id { get; set; } public string Type { get; set; } public string Status { get; set; }
            public int Amount { get; set; } public string Currency { get; set; } public string Mode { get; set; }
            public DateTimeOffset CreatedDate { get; set; } public Dictionary<string, JsonElement> Metadata { get; set; }
        }
    }
}
