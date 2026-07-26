using System.Collections.Generic;
using System.Linq;
using Abp.UI;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    internal static class RecruiterPlacementCycleValidator
    {
        internal static void EnsureNoCycle(
            IEnumerable<(int CustomerId, int? RecruiterCustomerId)> placements,
            int targetCustomerId,
            int newRecruiterCustomerId)
        {
            if (targetCustomerId == newRecruiterCustomerId)
                throw InvalidPlacement("A Club Member cannot recruit themselves.");

            var byCustomer = placements.ToDictionary(item => item.CustomerId);
            var visited = new HashSet<int>();
            var current = newRecruiterCustomerId;
            while (visited.Add(current) && byCustomer.TryGetValue(current, out var placement))
            {
                if (current == targetCustomerId)
                    throw InvalidPlacement("This correction would create a recruitment cycle.");
                if (!placement.RecruiterCustomerId.HasValue) return;
                current = placement.RecruiterCustomerId.Value;
            }

            if (current == targetCustomerId)
                throw InvalidPlacement("This correction would create a recruitment cycle.");
        }

        private static UserFriendlyException InvalidPlacement(string details) =>
            new UserFriendlyException("Recruiter correction failed.", details);
    }
}
