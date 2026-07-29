using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Moq;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class CustomerFallbackRoleResolverTests
    {
        private static readonly DateTime StartedAt =
            new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task ActiveProgrammeParticipation_ResolvesMemberRole(
            bool hasAQGreen,
            bool hasOnyx)
        {
            var customer = CreateCustomer();
            var aqGreenParticipations = hasAQGreen
                ? new[] { CreateActiveAQGreen(customer.Id) }
                : Array.Empty<EntryParticipation>();
            var onyxParticipations = hasOnyx
                ? new[] { CreateActiveOnyx(customer.Id) }
                : Array.Empty<OnyxParticipation>();
            var resolver = CreateResolver(aqGreenParticipations, onyxParticipations);

            var role = await resolver.ResolveAsync(customer);

            role.ShouldBe(AquaUserRole.Member);
        }

        [Fact]
        public async Task CustomerWithoutMembershipOrActiveProgramme_ResolvesGuestRole()
        {
            var role = await CreateResolver(
                    Array.Empty<EntryParticipation>(),
                    Array.Empty<OnyxParticipation>())
                .ResolveAsync(CreateCustomer());

            role.ShouldBe(AquaUserRole.Guest);
        }

        private static CustomerFallbackRoleResolver CreateResolver(
            IReadOnlyCollection<EntryParticipation> aqGreenParticipations,
            IReadOnlyCollection<OnyxParticipation> onyxParticipations)
        {
            var aqGreenRepository = new Mock<IRepository<EntryParticipation, Guid>>();
            aqGreenRepository
                .Setup(repository => repository.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<EntryParticipation, bool>>>() ))
                .ReturnsAsync((Expression<Func<EntryParticipation, bool>> predicate) =>
                    aqGreenParticipations.FirstOrDefault(predicate.Compile()));
            var onyxRepository = new Mock<IRepository<OnyxParticipation, Guid>>();
            onyxRepository
                .Setup(repository => repository.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<OnyxParticipation, bool>>>() ))
                .ReturnsAsync((Expression<Func<OnyxParticipation, bool>> predicate) =>
                    onyxParticipations.FirstOrDefault(predicate.Compile()));
            return new CustomerFallbackRoleResolver(
                aqGreenRepository.Object,
                onyxRepository.Object);
        }

        private static Customer CreateCustomer()
        {
            var customer = Customer.Create(
                1,
                10,
                "Programme Member",
                new EmailAddress("programme-member@example.com"));
            customer.Id = 20;
            return customer;
        }

        private static EntryParticipation CreateActiveAQGreen(int customerId)
        {
            var participation = EntryParticipation.StartIndependently(
                1,
                customerId,
                EntryProgrammeTerms.CreateSingleJoiningPayment(
                    "test-single-payment",
                    StartedAt,
                    1200m,
                    600m,
                    7),
                StartedAt);
            var payment = CreateConfirmedPayment(
                customerId,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "aqgreen-fallback-role");
            participation.ApplyConfirmedJoiningPayment(payment);
            return participation;
        }

        private static OnyxParticipation CreateActiveOnyx(int customerId)
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                1,
                customerId,
                30,
                OnyxPlanTerms.Create("test-onyx", StartedAt, 6120m),
                StartedAt);
            var payment = CreateConfirmedPayment(
                customerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "onyx-fallback-role");
            participation.ApplyConfirmedDirectEntryPayment(payment);
            return participation;
        }

        private static MemberPayment CreateConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string reference)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Test",
                reference,
                StartedAt);
            payment.Confirm(StartedAt.AddMinutes(1));
            return payment;
        }
    }
}
