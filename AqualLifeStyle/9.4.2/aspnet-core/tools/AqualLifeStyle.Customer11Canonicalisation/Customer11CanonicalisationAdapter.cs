using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Customer11Canonicalisation;

internal static class Customer11CanonicalisationAdapter
{
    internal static AQGreenLegacyCanonicalisationResult Invoke(VerifiedInput input)
    {
        // The existing domain is the only topology authority. No independent traversal or allocation.
        var result = AQGreenLegacyCanonicalisationSimulator.Simulate(input.Request);
        if (!result.IsCanonicalisable)
            throw new AdapterValidationException("DomainRejected:" + string.Join(',', result.Anomalies.Select(a => a.Code)));
        if (result.Placements.Count != input.Request.Candidates.Count)
            throw new AdapterValidationException("DomainPopulation");
        return result;
    }
}
