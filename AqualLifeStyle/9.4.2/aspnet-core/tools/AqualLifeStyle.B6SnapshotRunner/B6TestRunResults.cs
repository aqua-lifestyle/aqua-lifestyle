using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace AqualLifeStyle.B6SnapshotRunner;

public sealed record B6TestRunSummary(
    int Discovered,
    int Executed,
    int Passed,
    int Failed,
    int Skipped);

public static class B6TestRunResults
{
    private static readonly XNamespace TrxNamespace =
        "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static readonly string[] AccountingAndSuccessCounters =
        ["total", "executed", "passed"];

    private static readonly string[] NonSuccessCounters =
    [
        "failed", "error", "timeout", "aborted", "inconclusive", "passedButRunAborted",
        "notRunnable", "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending"
    ];

    private static readonly HashSet<string> SupportedCounters =
        new(AccountingAndSuccessCounters.Concat(NonSuccessCounters), StringComparer.Ordinal);

    public static B6TestRunSummary ReadAndValidate(string trxPath, int? expectedCount)
    {
        using var stream = SecureFiles.OpenRead(trxPath, controlled: false);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.None);

        var root = document.Root;
        if (root?.Name != TrxNamespace + "TestRun")
            throw new B6PackageException("TRX test results do not have the supported authoritative TestRun root.");
        if (root.Descendants(TrxNamespace + "TestRun").Any())
            throw new B6PackageException(
                "TRX test results contain more than one supported-namespace TestRun element.");

        var resultSummaries = root.Elements(TrxNamespace + "ResultSummary").ToArray();
        if (resultSummaries.Length != 1)
            throw new B6PackageException(
                "TRX test results do not contain exactly one authoritative direct ResultSummary element.");
        var resultSummary = resultSummaries[0];
        if (!string.Equals(resultSummary.Attribute("outcome")?.Value, "Completed", StringComparison.Ordinal))
            throw new B6PackageException("TRX ResultSummary.outcome must be explicitly Completed.");

        var authoritativeCounters = resultSummary.Elements(TrxNamespace + "Counters").ToArray();
        if (authoritativeCounters.Length != 1)
            throw new B6PackageException(
                "TRX test results do not contain exactly one authoritative direct Counters element.");
        var counters = authoritativeCounters[0];
        foreach (var attribute in counters.Attributes()
                     .Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace == XNamespace.None))
        {
            if (!SupportedCounters.Contains(attribute.Name.LocalName))
                throw new B6PackageException(
                    $"TRX Counters contains unsupported execution-status counter '{attribute.Name.LocalName}'.");
        }

        var values = SupportedCounters.ToDictionary(name => name, name => Attribute(counters, name),
            StringComparer.Ordinal);
        var summary = new B6TestRunSummary(
            values["total"],
            values["executed"],
            values["passed"],
            values["failed"],
            values["notExecuted"]);

        if (summary.Discovered == 0 || summary.Executed == 0)
            throw new B6PackageException("Filtered validation matched or executed zero tests.");
        if (summary.Executed != summary.Passed + summary.Failed)
            throw new B6PackageException("TRX executed count is inconsistent with passed and failed counts.");
        if (summary.Discovered != summary.Executed + summary.Skipped)
            throw new B6PackageException("TRX discovered count is inconsistent with executed and skipped counts.");
        foreach (var counter in NonSuccessCounters)
        {
            if (values[counter] != 0)
                throw new B6PackageException(
                    $"TRX Counters.{counter} must be zero for B6 evidence but is {values[counter]}.");
        }
        if (expectedCount is <= 0)
            throw new B6PackageException("Expected focused test count must be a positive integer.");
        if (expectedCount is { } expected && summary.Discovered != expected)
            throw new B6PackageException($"Filtered validation expected {expected} tests but discovered {summary.Discovered}.");
        return summary;
    }

    private static int Attribute(XElement counters, string name)
    {
        var text = counters.Attribute(name)?.Value;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new B6PackageException($"TRX Counters.{name} is missing or invalid.");
        return value;
    }
}
