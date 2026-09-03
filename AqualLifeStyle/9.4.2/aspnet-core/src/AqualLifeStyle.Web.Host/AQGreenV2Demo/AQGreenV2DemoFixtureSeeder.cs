using System;
using System.Collections.Generic;
using System.Linq;
using Abp;
using Abp.Authorization.Users;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using Castle.Core.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AqualLifeStyle.Web.Host.AQGreenV2Demo
{
    /// <summary>
    /// Idempotently prepares expensive structural prerequisites in the guarded
    /// disposable demo database. It deliberately does not confirm the root
    /// weekly-sales review or calculate the root commission ledger.
    /// </summary>
    public sealed class AQGreenV2DemoFixtureSeeder : ITransientDependency
    {
        public const string RootUserName = "aqgreen.demo.member";
        public const string RootEmail = "aqgreen.demo.member@example.test";
        private const int TenantId = 1;

        private readonly IDbContextProvider<AqualLifeStyleDbContext>
            _dbContextProvider;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly LatestClosedCommissionWeekResolver _weekResolver;
        private readonly IConfiguration _configuration;

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public AQGreenV2DemoFixtureSeeder(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider,
            IUnitOfWorkManager unitOfWorkManager,
            LatestClosedCommissionWeekResolver weekResolver,
            IConfiguration configuration)
        {
            _dbContextProvider = dbContextProvider;
            _unitOfWorkManager = unitOfWorkManager;
            _weekResolver = weekResolver;
            _configuration = configuration;
        }

        public void Seed()
        {
            if (!_configuration.GetValue<bool>("AQGreenV2Demo:Fixture:Enabled"))
            {
                return;
            }

            var memberPassword = _configuration["AQGreenV2Demo:Fixture:MemberPassword"];
            if (string.IsNullOrWhiteSpace(memberPassword) ||
                memberPassword.Length < 16)
            {
                throw new InvalidOperationException(
                    "AQGreenV2Demo__Fixture__MemberPassword must contain at least " +
                    "16 characters when the demo fixture is enabled.");
            }

            using (var unitOfWork = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true
            }))
            using (_unitOfWorkManager.Current.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant,
                       AbpDataFilters.SoftDelete))
            {
                SeedInCurrentUnitOfWork(memberPassword);
                unitOfWork.Complete();
            }
        }

        private void SeedInCurrentUnitOfWork(string memberPassword)
        {
            var context = _dbContextProvider.GetDbContext();
            if (!context.Database.IsNpgsql())
            {
                throw new InvalidOperationException(
                    "The AQGreen V2 demo fixture requires PostgreSQL.");
            }

            var week = _weekResolver.Resolve(DateTime.UtcNow);
            var existingRootUser = context.Users.IgnoreQueryFilters()
                .SingleOrDefault(user =>
                    user.TenantId == TenantId &&
                    user.UserName == RootUserName);
            if (existingRootUser != null)
            {
                ValidateExistingFixture(context, existingRootUser.Id, week);
                Logger.Info(
                    "NON-PRODUCTION AQGreen V2 demo fixture already exists for " +
                    $"week {week.PeriodStartUtc:O}.");
                return;
            }

            var tenant = context.Tenants.IgnoreQueryFilters()
                .Single(item => item.Id == TenantId);
            var area = context.Areas.IgnoreQueryFilters()
                .Single(item => item.TenantId == TenantId && item.Code == "JHB");
            var hostAdministrator = context.Users.IgnoreQueryFilters()
                .Single(user =>
                    user.TenantId == null &&
                    user.UserName == AbpUserBase.AdminUserName);
            var memberRole = context.Roles.IgnoreQueryFilters()
                .Single(role =>
                    role.TenantId == TenantId &&
                    role.Name == AquaUserRole.Member.ToString());

            var users = CreateUsers(context, memberPassword);
            context.SaveChanges();
            context.UserRoles.Add(new UserRole(
                TenantId,
                users[0].Id,
                memberRole.Id));

            var startedAt = week.PeriodStartUtc.AddDays(-30);
            var customers = users.Select((user, index) =>
            {
                var customer = Customer.Create(
                    TenantId,
                    user.Id,
                    index == 0
                        ? "AQGreen V2 Demo Member"
                        : $"AQGreen V2 Demo Network {index:00}",
                    new EmailAddress(user.EmailAddress));
                customer.AssignInitialArea(
                    area,
                    startedAt,
                    "NON-PRODUCTION AQGreen V2 demo fixture");
                return customer;
            }).ToList();
            context.Customers.AddRange(customers);
            context.SaveChanges();

            var programmeTerms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                "aqgreen-v2-demo-entry",
                startedAt.AddDays(-1),
                1200m,
                600m,
                7);
            var participations = CreateActiveParticipations(
                context,
                customers,
                programmeTerms,
                hostAdministrator.Id,
                startedAt);
            context.SaveChanges();

            var placements = CreatePlacements(participations, startedAt.AddHours(1));
            context.AQGreenPlacementTreeScopes.Add(placements.Scope);
            context.AQGreenNetworkPlacements.AddRange(placements.Rows);

            if (!context.EntryCommissionTermsVersions.Any(item =>
                    item.EffectiveAt <= week.PeriodStartUtc))
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "aqgreen-v2-demo-commission",
                        week.PeriodStartUtc,
                        150m,
                        250m,
                        1250m));
            }

            context.AreaActivationStateRecords.Add(
                AreaActivationStateRecord.Record(
                    Guid.NewGuid(),
                    TenantId,
                    true,
                    startedAt,
                    DateTime.UtcNow,
                    hostAdministrator.Id,
                    "NON-PRODUCTION AQGreen V2 demo fixture",
                    AreaActivationStateRecordKind.ObservedBaseline));

            var rootReview = AQGreenWeeklySalesEligibilityDecision.Begin(
                TenantId,
                participations[0].Id,
                AQGreenCommissionWeek.FromStartUtc(week.PeriodStartUtc),
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            context.AQGreenWeeklySalesEligibilityDecisions.Add(rootReview);

            var subordinateReviews = new List<AQGreenWeeklySalesEligibilityDecision>();
            for (var index = 1; index <= 5; index++)
            {
                var controlReview = AQGreenWeeklySalesEligibilityDecision.Begin(
                    TenantId,
                    participations[index].Id,
                    AQGreenCommissionWeek.FromStartUtc(week.PeriodStartUtc),
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                subordinateReviews.Add(controlReview);
                context.AQGreenWeeklySalesEligibilityDecisions.Add(controlReview);
            }

            // PostgreSQL immutability guards require the durable transition to
            // begin as HeldForEvidence before any finalized state is written.
            context.SaveChanges();
            for (var index = 0; index < subordinateReviews.Count; index++)
            {
                var controlReview = subordinateReviews[index];
                controlReview.AddManualEvidence(
                    $"demo-fixture:subordinate:{index + 1}",
                    DateTime.UtcNow);
            }
            context.SaveChanges();
            foreach (var controlReview in subordinateReviews)
            {
                controlReview.Confirm(
                    new AQGreenWeeklySalesQuantities(0, 0, 0),
                    hostAdministrator.Id,
                    DateTime.UtcNow);
            }

            context.SaveChanges();
            Logger.Info(
                "NON-PRODUCTION AQGreen V2 demo fixture created: " +
                $"tenant={tenant.Id}, member={RootUserName}, " +
                $"participation={participations[0].Id}, " +
                $"heldWeek={week.PeriodStartUtc:O}. " +
                "The root review and root commission remain uncompleted.");
        }

        private static List<User> CreateUsers(
            AqualLifeStyleDbContext context,
            string memberPassword)
        {
            var passwordHasher = new PasswordHasher<User>(
                new OptionsWrapper<PasswordHasherOptions>(
                    new PasswordHasherOptions()));
            var users = new List<User>();
            for (var index = 0; index < 31; index++)
            {
                var userName = index == 0
                    ? RootUserName
                    : $"aqgreen.demo.network.{index:00}";
                var user = new User
                {
                    TenantId = TenantId,
                    UserName = userName,
                    EmailAddress = index == 0
                        ? RootEmail
                        : $"{userName}@example.test",
                    Name = index == 0 ? "AQGreen" : "Demo",
                    Surname = index == 0 ? "Member" : $"Network {index:00}",
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                user.SetNormalizedNames();
                user.SetRole(AquaUserRole.Member);
                user.Password = passwordHasher.HashPassword(
                    user,
                    index == 0 ? memberPassword : User.CreateRandomPassword());
                context.Users.Add(user);
                users.Add(user);
            }

            return users;
        }

        private static List<EntryParticipation> CreateActiveParticipations(
            AqualLifeStyleDbContext context,
            IReadOnlyList<Customer> customers,
            EntryProgrammeTerms terms,
            long administratorUserId,
            DateTime startedAt)
        {
            var root = EntryParticipation.StartIndependently(
                TenantId,
                customers[0].Id,
                terms,
                startedAt);
            var participations = new List<EntryParticipation> { root };
            Activate(context, root, administratorUserId, startedAt, 0);

            var depthOne = new List<EntryParticipation>();
            for (var index = 1; index <= 5; index++)
            {
                var child = EntryParticipation.StartUnderRecruiter(
                    TenantId,
                    customers[index].Id,
                    root,
                    terms,
                    startedAt.AddMinutes(index));
                Activate(context, child, administratorUserId, startedAt, index);
                depthOne.Add(child);
                participations.Add(child);
            }

            var customerIndex = 6;
            foreach (var parent in depthOne)
            {
                for (var slot = 1; slot <= 5; slot++)
                {
                    var child = EntryParticipation.StartUnderRecruiter(
                        TenantId,
                        customers[customerIndex].Id,
                        parent,
                        terms,
                        startedAt.AddMinutes(customerIndex));
                    Activate(
                        context,
                        child,
                        administratorUserId,
                        startedAt,
                        customerIndex);
                    participations.Add(child);
                    customerIndex++;
                }
            }

            context.EntryParticipations.AddRange(participations);
            return participations;
        }

        private static void Activate(
            AqualLifeStyleDbContext context,
            EntryParticipation participation,
            long administratorUserId,
            DateTime startedAt,
            int index)
        {
            var payment = MemberPayment.CreatePending(
                TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "AQGreenV2Demo",
                $"aqgreen-v2-demo-joining-{index:00}",
                startedAt.AddMinutes(index + 40));
            payment.Confirm(startedAt.AddMinutes(index + 41));
            participation.ApplyConfirmedJoiningPayment(payment);
            participation.ApproveByAdministrator(
                administratorUserId,
                startedAt.AddMinutes(index + 42));
            context.MemberPayments.Add(payment);
        }

        private static PlacementFixture CreatePlacements(
            IReadOnlyList<EntryParticipation> participations,
            DateTime placedAt)
        {
            var scope = AQGreenPlacementTreeScope.Create(TenantId);
            var root = AQGreenNetworkPlacement.CreateRoot(
                scope,
                participations[0].Id,
                placedAt,
                AQGreenPlacementRules.CurrentVersion);
            var rows = new List<AQGreenNetworkPlacement> { root };
            for (var index = 1; index <= 5; index++)
            {
                rows.Add(AQGreenNetworkPlacement.CreateChild(
                    root,
                    participations[index].Id,
                    index,
                    placedAt,
                    AQGreenPlacementRules.CurrentVersion));
            }

            var participationIndex = 6;
            for (var parentIndex = 1; parentIndex <= 5; parentIndex++)
            {
                for (var slot = 1; slot <= 5; slot++)
                {
                    rows.Add(AQGreenNetworkPlacement.CreateChild(
                        rows[parentIndex],
                        participations[participationIndex++].Id,
                        slot,
                        placedAt,
                        AQGreenPlacementRules.CurrentVersion));
                }
            }

            return new PlacementFixture(scope, rows);
        }

        private static void ValidateExistingFixture(
            AqualLifeStyleDbContext context,
            long rootUserId,
            ClosedCommissionWeek week)
        {
            var rootCustomer = context.Customers.IgnoreQueryFilters()
                .Single(customer =>
                    customer.TenantId == TenantId &&
                    customer.UserId == rootUserId);
            var rootParticipation = context.EntryParticipations.IgnoreQueryFilters()
                .Single(participation =>
                    participation.TenantId == TenantId &&
                    participation.CustomerId == rootCustomer.Id);
            var decision = context.AQGreenWeeklySalesEligibilityDecisions
                .IgnoreQueryFilters()
                .SingleOrDefault(item =>
                    item.TenantId == TenantId &&
                    item.ParticipantId == rootParticipation.Id &&
                    item.CommissionWeekStartUtc == week.PeriodStartUtc);
            if (decision == null)
            {
                throw new InvalidOperationException(
                    "The existing AQGreen V2 demo fixture targets an older week. " +
                    "Recreate the disposable demo database.");
            }
        }

        private sealed class PlacementFixture
        {
            public PlacementFixture(
                AQGreenPlacementTreeScope scope,
                IReadOnlyCollection<AQGreenNetworkPlacement> rows)
            {
                Scope = scope;
                Rows = rows;
            }

            public AQGreenPlacementTreeScope Scope { get; }
            public IReadOnlyCollection<AQGreenNetworkPlacement> Rows { get; }
        }
    }
}
