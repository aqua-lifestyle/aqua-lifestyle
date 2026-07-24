using System.Linq;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.EntityFrameworkCore.Seed.Host;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class RequiredProgrammeConfigurationBuilderTests
        : AqualLifeStyleTestBase
    {
        [Fact]
        public void CreatesMissingOnyxConfigurationWhenOtherPlansAlreadyExist()
        {
            UsingDbContext(context =>
            {
                var existingOnyxPlans = context.Memberships
                    .IgnoreQueryFilters()
                    .Where(membership =>
                        membership.MembershipType == MembershipType.Onyx)
                    .ToList();
                context.Memberships.RemoveRange(existingOnyxPlans);
                if (!context.Memberships.IgnoreQueryFilters().Any())
                {
                    context.Memberships.Add(Membership.Create(
                        tenantId: null,
                        name: "Existing plan",
                        description: "Pre-existing production configuration"));
                }
                context.SaveChanges();

                var builder =
                    new RequiredProgrammeConfigurationBuilder(context);
                builder.Create();
                builder.Create();

                var onyxPlans = context.Memberships
                    .IgnoreQueryFilters()
                    .Where(membership =>
                        membership.MembershipType == MembershipType.Onyx)
                    .ToList();
                onyxPlans.Count.ShouldBe(1);
                onyxPlans[0].TenantId.ShouldBeNull();
                onyxPlans[0].IsActive.ShouldBeTrue();
            });
        }
    }
}
