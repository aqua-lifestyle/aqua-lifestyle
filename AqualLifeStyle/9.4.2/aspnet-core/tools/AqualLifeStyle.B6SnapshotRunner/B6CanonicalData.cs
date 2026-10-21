using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed class B6Allowlist
{
    public B6Allowlist(string? path)
    {
        Path = System.IO.Path.GetFullPath(path ?? System.IO.Path.Combine(B6Paths.PackageRoot(), "allowlist.json"));
        var node = CanonicalJson.Read(Path, controlled: false);
        Digest = CanonicalJson.Digest(node);
        if (Digest != B6Contract.ExpectedAllowlistDigest)
            throw new B6PackageException("The executable B6 allowlist does not match the reviewed .NET package allowlist.");
        Value = node.Deserialize<AllowlistContract>(CanonicalJson.Options)
            ?? throw new B6PackageException("The B6 allowlist is empty.");
        Validate(node);
        ByName = Value.Tables.ToDictionary(table => table.SourceTable, StringComparer.Ordinal);
    }

    public string Path { get; }
    public string Digest { get; }
    public AllowlistContract Value { get; }
    public IReadOnlyDictionary<string, TableContract> ByName { get; }

    private void Validate(JsonNode node)
    {
        var expectedTop = new HashSet<string>(
        ["allowlistVersion", "packageSchemaVersion", "fieldContract", "transformSemantics",
         "tables", "prohibitedOutputColumnPatterns", "prohibitedSourceColumns", "prohibitedTablePatterns"],
            StringComparer.Ordinal);
        var actualTop = node.AsObject().Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        if (!actualTop.SetEquals(expectedTop)) throw new B6PackageException("Allowlist top-level contract is unexpected.");
        if (Value.PackageSchemaVersion != B6Contract.PackageVersion)
            throw new B6PackageException("Allowlist package version is not the selected .NET-native package profile.");
        if (Value.Tables.Count == 0 || Value.Tables.Select(table => table.SourceTable).Distinct(StringComparer.Ordinal).Count() != Value.Tables.Count)
            throw new B6PackageException("Allowlist contains no tables or duplicate tables.");
        var tablePatterns = Value.ProhibitedTablePatterns.Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase)).ToList();
        foreach (var table in Value.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.ScopeRelationship) || tablePatterns.Any(pattern => pattern.IsMatch(table.SourceTable)))
                throw new B6PackageException($"Allowlist table {table.SourceTable} lacks safe scope semantics.");
            var outputs = table.Fields.Select(field => field.Output).ToList();
            if (outputs.Distinct(StringComparer.Ordinal).Count() != outputs.Count ||
                table.PrimaryKey.Count == 0 || table.PrimaryKey.Except(outputs, StringComparer.Ordinal).Any())
                throw new B6PackageException($"Allowlist table {table.SourceTable} has an invalid key/output contract.");
            var denied = Value.ProhibitedSourceColumns.GetValueOrDefault(table.SourceTable, []);
            foreach (var field in table.Fields)
            {
                var transformKey = field.Transform.StartsWith("pseudonym-", StringComparison.Ordinal) ? "pseudonym-*" :
                    field.Transform.StartsWith("canonical-", StringComparison.Ordinal) ? "canonical-*" : field.Transform;
                if (field.SourceColumns.Count != 1 || string.IsNullOrWhiteSpace(field.Purpose) ||
                    string.IsNullOrWhiteSpace(field.Classification) || !Value.TransformSemantics.ContainsKey(transformKey))
                    throw new B6PackageException($"Incomplete field contract in {table.SourceTable}.{field.Output}.");
                if (denied.Intersect(field.SourceColumns, StringComparer.Ordinal).Any() && field.Transform != "presence-bool")
                    throw new B6PackageException($"Prohibited source data is retained by {table.SourceTable}.{field.Output}.");
            }
        }
    }
}

public sealed class Pseudonymizer
{
    private readonly byte[] _secret;
    private readonly Func<byte[], byte[]> _digest;
    private readonly Dictionary<(string Kind, string Namespace), Dictionary<string, string>> _maps = [];
    private readonly Dictionary<(string Kind, string Candidate), (string Namespace, string Raw)> _used = [];

    public Pseudonymizer(byte[] secret, Func<byte[], byte[]>? digest = null)
    {
        if (secret.Length < 32 || secret.All(value => value == 0) || secret.Distinct().Count() < 16)
            throw new B6PackageException("The pseudonymisation key fails the >=32-byte cryptographic-strength contract.");
        _secret = secret.ToArray();
        _digest = digest ?? (message => HMACSHA256.HashData(_secret, message));
    }

    public void Prepare(string kind, string @namespace, IEnumerable<string> values)
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in values.Where(value => value != B6Contract.Null).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var message = Encoding.UTF8.GetBytes($"B6PSEUDONYM3\0{kind}\0{@namespace}\0{raw}");
            var digest = _digest(message);
            if (digest.Length < 16) throw new B6PackageException("Pseudonym digest is too short.");
            string candidate;
            if (kind == "uuid")
            {
                var material = digest[..16].ToArray();
                material[6] = (byte)((material[6] & 0x0f) | 0x40);
                material[8] = (byte)((material[8] & 0x3f) | 0x80);
                candidate = new Guid(material, bigEndian: true).ToString("D");
            }
            else if (kind == "int")
            {
                candidate = (BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(0, 8)) % 2_000_000_000UL + 1UL)
                    .ToString(CultureInfo.InvariantCulture);
            }
            else throw new B6PackageException($"Unsupported pseudonym kind {kind}.");
            var collision = (kind, candidate);
            var identity = (@namespace, raw);
            if (_used.TryGetValue(collision, out var prior) && prior != identity)
                throw new B6PackageException("Fatal pseudonym collision detected; no fallback identity was generated.");
            _used[collision] = identity;
            mapping.Add(raw, candidate);
        }
        _maps.Add((kind, @namespace), mapping);
    }

    public string Map(string kind, string @namespace, string raw)
    {
        if (raw == B6Contract.Null) return raw;
        if (!_maps.TryGetValue((kind, @namespace), out var map) || !map.TryGetValue(raw, out var value))
            throw new B6PackageException($"Unprepared {@namespace} identity encountered.");
        return value;
    }
}

public static class B6Digests
{
    public static byte[] EncodeField(string value)
    {
        if (value == B6Contract.Null) return [(byte)'N'];
        var bytes = Encoding.UTF8.GetBytes(value);
        return Encoding.ASCII.GetBytes($"V{bytes.Length}:").Concat(bytes).ToArray();
    }

    public static byte[] CanonicalTableBytes(TableContract table, IEnumerable<Dictionary<string, string>> sourceRows)
    {
        var fields = table.Fields.Select(field => field.Output).ToList();
        var definitions = table.Fields.ToDictionary(field => field.Output, StringComparer.Ordinal);
        var rows = sourceRows.Select(row => fields.ToDictionary(name => name,
                name => B6Values.CanonicalScalar(row[name], definitions[name].Type, definitions[name].Transform),
                StringComparer.Ordinal))
            .OrderBy(row => string.Join('\0', table.PrimaryKey.Select(name => row[name])), StringComparer.Ordinal);
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("B6CANON1\n"));
        foreach (var row in rows)
        {
            stream.WriteByte((byte)'R');
            foreach (var name in fields) stream.Write(EncodeField(row[name]));
            stream.WriteByte((byte)'\n');
        }
        return stream.ToArray();
    }

    public static string Tuples(IEnumerable<IEnumerable<string>> values)
    {
        var rows = values.Select(row => row.ToArray())
            .OrderBy(row => string.Join('\0', row), StringComparer.Ordinal);
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("B6TUPLE1\n"));
        foreach (var row in rows)
        {
            stream.WriteByte((byte)'R');
            foreach (var value in row) stream.Write(EncodeField(value));
            stream.WriteByte((byte)'\n');
        }
        return B6Values.Sha256Hex(stream.ToArray());
    }
}

public static class DeterministicUuid
{
    private static readonly Guid UrlNamespace = Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    public static Guid CreateUrlV5(string name)
    {
        Span<byte> namespaceBytes = stackalloc byte[16];
        UrlNamespace.TryWriteBytes(namespaceBytes, bigEndian: true, out _);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = namespaceBytes.ToArray().Concat(nameBytes).ToArray();
        var digest = SHA1.HashData(input);
        var result = digest[..16];
        result[6] = (byte)((result[6] & 0x0f) | 0x50);
        result[8] = (byte)((result[8] & 0x3f) | 0x80);
        return new Guid(result, bigEndian: true);
    }
}
