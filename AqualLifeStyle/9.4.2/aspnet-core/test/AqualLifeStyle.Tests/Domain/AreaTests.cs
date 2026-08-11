using System;
using System.Linq;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AreaTests
    {
        private static readonly DateTime Baseline =
            new DateTime(2026, 8, 11, 15, 2, 51, DateTimeKind.Utc);

        [Fact]
        public void Area_UsesTenantScopedStableCodeAndLifecycle()
        {
            var area = Area.Create(1, " jhb ", " Johannesburg ");

            area.TenantId.ShouldBe(1);
            area.Code.ShouldBe("JHB");
            area.Name.ShouldBe("Johannesburg");
            area.IsActive.ShouldBeTrue();

            area.Rename("Johannesburg Central");
            area.Code.ShouldBe("JHB");
            area.Deactivate();
            area.IsActive.ShouldBeFalse();
            area.Activate();
            area.IsActive.ShouldBeTrue();
        }

        [Fact]
        public void CustomerAreaMovement_IsEffectiveDatedAndRetainsHistory()
        {
            var johannesburg = Area.Create(1, "JHB", "Johannesburg");
            var pretoria = Area.Create(1, "PTA", "Pretoria");
            var customer = Customer.Create(
                1, 42, "Aqua Member", new EmailAddress("member@example.test"));

            customer.AssignInitialArea(johannesburg, Baseline, "Initial assignment");
            customer.MoveToArea(pretoria, Baseline.AddDays(30), "Relocated");

            customer.AreaId.ShouldBe(pretoria.Id);
            customer.AreaAssignments.Count.ShouldBe(2);
            customer.AreaAssignments.Single(item => item.AreaId == johannesburg.Id)
                .EffectiveTo.ShouldBe(Baseline.AddDays(30));
            customer.AreaAssignments.Single(item => item.AreaId == pretoria.Id)
                .EffectiveTo.ShouldBeNull();
        }

        [Fact]
        public void CustomerAreaAssignment_RejectsCrossTenantAndInactiveAreas()
        {
            var customer = Customer.Create(
                1, 42, "Aqua Member", new EmailAddress("member@example.test"));
            var otherTenant = Area.Create(2, "JHB", "Johannesburg");
            var inactive = Area.Create(1, "PTA", "Pretoria");
            inactive.Deactivate();

            Should.Throw<InvalidOperationException>(() =>
                customer.AssignInitialArea(otherTenant, Baseline, "Invalid"));
            Should.Throw<InvalidOperationException>(() =>
                customer.AssignInitialArea(inactive, Baseline, "Invalid"));
        }

        [Fact]
        public void AdministratorMayHoldMultipleSameTenantAssignmentsButNotCrossTenant()
        {
            var johannesburg = Area.Create(1, "JHB", "Johannesburg");
            var pretoria = Area.Create(1, "PTA", "Pretoria");

            AreaAdminAssignment.Assign(johannesburg, 9, 1, Baseline).IsActive.ShouldBeTrue();
            AreaAdminAssignment.Assign(pretoria, 9, 1, Baseline).IsActive.ShouldBeTrue();
            Should.Throw<InvalidOperationException>(() =>
                AreaAdminAssignment.Assign(johannesburg, 10, 2, Baseline));
        }

        [Fact]
        public void SameTenantDifferentAreaRecruitment_RemainsInOneAQGreenNetwork()
        {
            var johannesburg = Area.Create(1, "JHB", "Johannesburg");
            var pretoria = Area.Create(1, "PTA", "Pretoria");
            var terms = EntryProgrammeTerms.Create(
                "entry-area-test", Baseline, 600m, 600m, 600m, 7);
            var sponsorCustomer = Customer.Create(
                1, 101, "Sponsor", new EmailAddress("sponsor@example.test"));
            sponsorCustomer.AssignInitialArea(johannesburg, Baseline, "Test");
            var sponsor = EntryParticipation.StartIndependently(
                1, sponsorCustomer.Id = 1, terms, Baseline);
            Activate(sponsor);
            var network = new List<EntryParticipation> { sponsor };

            for (var index = 0; index < 5; index++)
            {
                var customer = Customer.Create(
                    1,
                    200 + index,
                    $"Pretoria recruit {index}",
                    new EmailAddress($"pretoria-{index}@example.test"));
                customer.Id = index + 2;
                customer.AssignInitialArea(pretoria, Baseline, "Test");
                var recruit = EntryParticipation.StartUnderRecruiter(
                    1, customer.Id, sponsor, terms, Baseline);
                Activate(recruit);
                network.Add(recruit);
            }

            new EntryNetworkQualificationEvaluator()
                .Evaluate(sponsor.CustomerId, network)
                .ShouldBe(EntryNetworkLevel.Level1);
        }

        private static void Activate(EntryParticipation participation)
        {
            var registration = MemberPayment.CreatePending(
                1, participation.CustomerId, MemberPaymentPurpose.EntryRegistration,
                600m, "Test", $"area-registration-{participation.CustomerId}", Baseline);
            registration.Confirm(Baseline.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = MemberPayment.CreatePending(
                1, participation.CustomerId, MemberPaymentPurpose.EntryActivation,
                600m, "Test", $"area-activation-{participation.CustomerId}", Baseline);
            activation.Confirm(Baseline.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activation);
            participation.ApproveByAdministrator(1, Baseline.AddMinutes(3));
        }
    }
}
