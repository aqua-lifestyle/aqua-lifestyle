using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Abp;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    /// <summary>
    /// Definitive backend-only continuous proof. One fresh prospective root is
    /// carried through root bootstrap, 5 + 25 ordinary allocator approvals, B4,
    /// B5.1, B5.2, test-gated B5.3, B5.4, durable replay, and retry. No placement,
    /// graduation, sales decision, commission, or evidence row is directly seeded.
    /// </summary>
    [Collection("WeeklyCommissionPostgreSqlRegression")]
    public sealed class AQGreenV2ContinuousFreshNetworkE2ETests
        : AqualLifeStyleWebTestBase
    {
        private const int TenantId = 1;

        [Fact]
        public async Task SameFreshRoot_ReachesDurableLevelTwoR400_AndReplays()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            var postgresVersion = await AssertFreshPostgreSqlDatabaseAsync();
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var programmeTerms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                $"continuous-{suffix}",
                DateTime.UtcNow.AddDays(-1),
                1200m,
                600m,
                7);

            LoginAsDefaultTenantAdmin();
            var root = await SeedAwaitingPaymentParticipantAsync(
                programmeTerms,
                null,
                "root",
                authorisedProspectiveRoot: true);
            await ConfirmJoiningPaymentAsync(root);
            await AssertAwaitingApprovalAsync(root.ParticipationId);

            LoginAsHostAdmin();
            using (ApprovalGate.Enable(root.ParticipationId))
            {
                await ApproveAsync(root.ParticipationId);
            }
            await AssertActiveApprovalAsync(root, isRoot: true);

            LoginAsDefaultTenantAdmin();
            var depthOne = new List<ParticipantFixture>();
            for (var slot = 1; slot <= 5; slot++)
            {
                var child = await SeedAwaitingPaymentParticipantAsync(
                    programmeTerms,
                    root,
                    $"depth1-{slot}",
                    authorisedProspectiveRoot: false);
                await ConfirmJoiningPaymentAsync(child);
                await AssertAwaitingApprovalAsync(child.ParticipationId);
                using (ApprovalGate.Enable(child.ParticipationId))
                {
                    await ApproveAsync(child.ParticipationId);
                }
                await AssertActiveApprovalAsync(child, isRoot: false);
                depthOne.Add(child);
            }

            var depthTwo = new List<ParticipantFixture>();
            foreach (var parent in depthOne)
            {
                for (var slot = 1; slot <= 5; slot++)
                {
                    var child = await SeedAwaitingPaymentParticipantAsync(
                        programmeTerms,
                        parent,
                        $"depth2-{parent.Label}-{slot}",
                        authorisedProspectiveRoot: false);
                    await ConfirmJoiningPaymentAsync(child);
                    await AssertAwaitingApprovalAsync(child.ParticipationId);
                    using (ApprovalGate.Enable(child.ParticipationId))
                    {
                        await ApproveAsync(child.ParticipationId);
                    }
                    await AssertActiveApprovalAsync(child, isRoot: false);
                    depthTwo.Add(child);
                }
            }

            var allParticipants = new[] { root }
                .Concat(depthOne)
                .Concat(depthTwo)
                .ToList();
            allParticipants.Count.ShouldBe(31);

            var cycleResolver = Resolve<LatestClosedCommissionWeekResolver>();
            var calculationAsOf = DateTime.UtcNow.AddDays(21);
            var primaryWeek = cycleResolver.Resolve(calculationAsOf);
            var controlWeek = cycleResolver.Resolve(calculationAsOf.AddDays(7));
            AssertCanonicalClosedWeek(cycleResolver, primaryWeek, calculationAsOf);
            AssertCanonicalClosedWeek(
                cycleResolver,
                controlWeek,
                calculationAsOf.AddDays(7));
            controlWeek.PeriodStartUtc.ShouldBe(primaryWeek.PeriodStartUtc.AddDays(7));
            (await GraduationSelector.SelectAsync(TenantId, root.ParticipationId))
                .ShouldBe(AQGreenGraduationStructuralModel.LegacyV1,
                    "the test gate must start disabled; production remains LegacyV1");
            (await CommissionSelector.SelectAsync(TenantId, primaryWeek.PeriodEndUtc))
                .ShouldBe(AQGreenCommissionStructuralModel.LegacyV1,
                    "the test gate must start disabled; production remains LegacyV1");
            (await SalesReviewGate.IsEnabledAsync(TenantId)).ShouldBeFalse(
                "the B5.3 application path is explicitly test-gated");

            var topology = await AssertTopologyAsync(
                root,
                depthOne,
                depthTwo,
                primaryWeek.PeriodEndUtc);

            var b4 = await InUnitOfWorkAsync(() =>
                Resolve<AQGreenStructuralCompletionEvaluator>()
                    .EvaluateAsync(
                        TenantId,
                        root.ParticipationId,
                        primaryWeek.PeriodEndUtc));
            b4.ParticipantId.ShouldBe(root.ParticipationId);
            b4.PlacementTreeScopeId.ShouldBe(topology.ScopeId);
            b4.StructuralCompletionLevel.ShouldBe(
                AQGreenStructuralCompletionLevel.Level2);
            b4.QualifyingDepth1Count.ShouldBe(5);
            b4.QualifyingDepth2Count.ShouldBe(25);
            b4.QualifyingDepth3Count.ShouldBe(0);
            b4.RulesVersion.ShouldBe(
                AQGreenStructuralQualificationRules.CurrentVersion);
            b4.Cutoff.ShouldBe(primaryWeek.PeriodEndUtc);

            var b4Evidence = await InUnitOfWorkAsync(() =>
                Resolve<IAQGreenCommissionStructuralEvidenceEvaluator>()
                    .EvaluateAsync(
                        TenantId,
                        root.ParticipationId,
                        primaryWeek.PeriodEndUtc));
            b4Evidence.Observations.Count.ShouldBe(31);
            b4Evidence.Observations.ShouldAllBe(observation =>
                observation.ParticipationStatusObserved ==
                    EntryParticipationStatus.Active &&
                observation.CustomerTenantMatchedObserved &&
                observation.CustomerIsActiveObserved &&
                !observation.CustomerIsDeletedObserved &&
                observation.UserTenantMatchedObserved &&
                observation.UserIsActiveObserved &&
                !observation.UserIsDeletedObserved);

            using (ProgressGate.Enable(root.ParticipationId))
            {
                AbpSession.TenantId = TenantId;
                AbpSession.UserId = root.UserId;
                var progress = await InUnitOfWorkAsync(() =>
                    Resolve<IClubMemberProgrammeProgressAppService>()
                        .GetMyProgressAsync());
                progress.QualifiedLevel.ShouldBe(2);
                progress.DirectRecruits.ShouldBe(5);
                progress.StructuralProgress.CompletedLevel.ShouldBe(2);
                progress.StructuralProgress.TargetLevel.ShouldBe(3);
                progress.StructuralProgress.AchievedCount.ShouldBe(0);
                progress.StructuralProgress.RequiredCount.ShouldBe(125);
                progress.StructuralProgress.RemainingCount.ShouldBe(125);
                progress.StructuralProgress.ProgressPercent.ShouldBe(0);
                progress.StructuralProgress.RulesVersion.ShouldBe(
                    AQGreenStructuralQualificationRules.CurrentVersion);
            }
            StructuralEvaluator.GetCallCount(root.ParticipationId).ShouldBe(1);

            LoginAsDefaultTenantAdmin();
            var loanId = await CreateAcceptedLoanWithSatisfiedInitialRequirementsAsync(
                root,
                allParticipants);
            OnyxGraduationDecisionDto graduation;
            OnyxGraduationDecisionDto graduationRetry;
            using (GraduationSelector.Enable(root.ParticipationId))
            {
                graduation = await Resolve<IAdminProgrammeParticipationAppService>()
                    .GraduateAQGreenToOnyxAsync(new GraduateAQGreenToOnyxInput
                    {
                        LoanAgreementId = loanId,
                        Justification =
                            "Continuous E2E: Placement V2 Level 2 and accepted agreement"
                    });
                graduationRetry = await Resolve<IAdminProgrammeParticipationAppService>()
                    .GraduateAQGreenToOnyxAsync(new GraduateAQGreenToOnyxInput
                    {
                        LoanAgreementId = loanId,
                        Justification =
                            "Continuous E2E: Placement V2 Level 2 and accepted agreement"
                    });
            }
            graduation.AQGreenParticipationId.ShouldBe(root.ParticipationId);
            graduation.StructuralModel.ShouldBe(
                AQGreenGraduationStructuralModel.PlacementV2);
            graduation.EvaluatedNetworkLevel.ShouldBeNull();
            graduationRetry.DecisionId.ShouldBe(graduation.DecisionId);
            graduationRetry.OnyxParticipationId.ShouldBe(
                graduation.OnyxParticipationId);
            await AssertGraduationAsync(root, graduation, topology);

            await SeedCommissionPrerequisitesAsync(
                suffix,
                primaryWeek,
                controlWeek);

            AQGreenWeeklySalesEligibilityDecisionDto rootPrimarySales;
            AQGreenWeeklySalesEligibilityDecisionDto rootControlSales;
            LoginAsHostAdmin();
            using (SalesReviewGate.Enable(TenantId))
            {
                rootPrimarySales = await ReviewSalesAsync(
                    root.ParticipationId,
                    primaryWeek,
                    5,
                    5,
                    5,
                    "continuous:root:primary",
                    requireHeldTransition: true);

                foreach (var participant in depthOne)
                {
                    await ReviewSalesAsync(
                        participant.ParticipationId,
                        primaryWeek,
                        5,
                        5,
                        4,
                        $"continuous:{participant.Label}:primary",
                        requireHeldTransition: false);
                }

                rootControlSales = await ReviewSalesAsync(
                    root.ParticipationId,
                    controlWeek,
                    5,
                    5,
                    4,
                    "continuous:root:control",
                    requireHeldTransition: true);

                foreach (var participant in depthOne)
                {
                    await ReviewSalesAsync(
                        participant.ParticipationId,
                        controlWeek,
                        5,
                        5,
                        4,
                        $"continuous:{participant.Label}:control",
                        requireHeldTransition: false);
                }
            }
            rootPrimarySales.ParticipantId.ShouldBe(root.ParticipationId);
            rootPrimarySales.ReviewStatus.ShouldBe(
                AQGreenWeeklySalesReviewStatus.Confirmed);
            rootPrimarySales.ThresholdResult.ShouldBe(
                AQGreenWeeklySalesThresholdResult.Met);
            rootControlSales.ThresholdResult.ShouldBe(
                AQGreenWeeklySalesThresholdResult.NotMet);

            var primaryCalculation = await CalculateAsync(primaryWeek);
            primaryCalculation.WasAlreadyCalculated.ShouldBeFalse();
            primaryCalculation.RecordsCreated.ShouldBe(31);
            primaryCalculation.TotalEarnedAmount.ShouldBe(400m);

            var primaryLedger = await LoadAndAssertPrimaryLedgerAsync(
                root,
                primaryWeek,
                rootPrimarySales,
                topology);
            var replay = await InUnitOfWorkAsync(() =>
                Resolve<IAQGreenV2WeeklyCommissionEvidenceReplayValidator>()
                    .ValidateAsync(primaryLedger.LedgerId));
            AssertPrimaryReplay(replay);

            var retry = await CalculateAsync(primaryWeek);
            retry.WasAlreadyCalculated.ShouldBeTrue();
            retry.PeriodId.ShouldBe(primaryLedger.PeriodId);
            retry.RecordsCreated.ShouldBe(0);
            retry.TotalEarnedAmount.ShouldBe(400m);
            await AssertNoDuplicatePrimaryLedgerAsync(
                root.ParticipationId,
                primaryLedger);

            var controlCalculation = await CalculateAsync(controlWeek);
            controlCalculation.WasAlreadyCalculated.ShouldBeFalse();
            var controlLedger = await LoadAndAssertControlLedgerAsync(
                root,
                controlWeek,
                rootControlSales,
                topology);
            var controlReplay = await InUnitOfWorkAsync(() =>
                Resolve<IAQGreenV2WeeklyCommissionEvidenceReplayValidator>()
                    .ValidateAsync(controlLedger.LedgerId));
            controlReplay.QualifiedStructuralLevel.ShouldBe(
                AQGreenStructuralCompletionLevel.Level2);
            controlReplay.CommissionedLevel.ShouldBe(0);
            controlReplay.TotalAmount.ShouldBe(0m);
            controlReplay.SalesThresholdResult.ShouldBe(
                AQGreenWeeklySalesThresholdResult.NotMet);

            await ProveReplayIgnoresMutableCurrentUserAsync(
                root.UserId,
                primaryLedger.LedgerId);
            await AssertTopologyUnchangedAsync(topology);
            await WriteSnapshotAsync(
                postgresVersion,
                root,
                topology,
                primaryWeek,
                b4,
                graduation,
                rootPrimarySales,
                primaryLedger,
                replay,
                controlLedger);
        }

        private async Task<string> AssertFreshPostgreSqlDatabaseAsync()
        {
            return await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue(
                    "the continuous E2E must run against PostgreSQL");
                (await context.EntryParticipations.IgnoreQueryFilters().CountAsync())
                    .ShouldBe(0, "a fresh database must contain no AQGreen participation");
                (await context.AQGreenNetworkPlacements.IgnoreQueryFilters().CountAsync())
                    .ShouldBe(0, "a fresh database must contain no AQGreen placement");
                (await context.EntryWeeklyCommissions.IgnoreQueryFilters().CountAsync())
                    .ShouldBe(0, "a fresh database must contain no commission ledger");
                (await context.OnyxGraduationDecisions.IgnoreQueryFilters().CountAsync())
                    .ShouldBe(0, "a fresh database must contain no graduation decision");
                (await context.AQGreenWeeklySalesEligibilityDecisions
                    .IgnoreQueryFilters().CountAsync()).ShouldBe(0,
                    "a fresh database must contain no weekly-sales decision");
                return await context.Database
                    .SqlQueryRaw<string>("SELECT version() AS \"Value\"")
                    .SingleAsync();
            });
        }

        private async Task<ParticipantFixture> SeedAwaitingPaymentParticipantAsync(
            EntryProgrammeTerms terms,
            ParticipantFixture sponsor,
            string label,
            bool authorisedProspectiveRoot)
        {
            return await UsingDbContextAsync(async context =>
            {
                var now = DateTime.UtcNow.AddMinutes(-1);
                var area = await (
                        from assignment in context.AreaAdminAssignments
                        join candidate in context.Areas on assignment.AreaId equals candidate.Id
                        where assignment.TenantId == TenantId &&
                              assignment.UserId == AbpSession.UserId.Value &&
                              !assignment.RevokedAt.HasValue &&
                              candidate.IsActive
                        select candidate)
                    .FirstAsync();
                var guestRole = await context.Roles.SingleAsync(role =>
                    role.TenantId == TenantId && role.Name == "Guest");
                var suffix = Guid.NewGuid().ToString("N");
                var userName = $"aqg-continuous-{label}-{suffix}";
                var user = new User
                {
                    TenantId = TenantId,
                    UserName = userName,
                    EmailAddress = $"{userName}@example.test",
                    Name = "AQGreen",
                    Surname = "ContinuousE2E",
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                user.SetRole(AquaUserRole.Guest);
                user.SetNormalizedNames();
                user.Password = new PasswordHasher<User>(
                        new OptionsWrapper<PasswordHasherOptions>(
                            new PasswordHasherOptions()))
                    .HashPassword(user, User.DefaultPassword);
                context.Users.Add(user);
                await context.SaveChangesAsync();
                context.UserRoles.Add(new UserRole(TenantId, user.Id, guestRole.Id));

                var customer = Customer.Create(
                    TenantId,
                    user.Id,
                    $"AQGreen Continuous {label} {suffix}",
                    new EmailAddress(user.EmailAddress),
                    user: user);
                customer.AssignInitialArea(
                    area,
                    now,
                    "AQGreen V2 continuous fresh-data E2E");
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                EntryParticipation participation;
                EntryParticipation sponsorParticipation = null;
                if (sponsor == null)
                {
                    participation = EntryParticipation.StartIndependently(
                        TenantId,
                        customer.Id,
                        terms,
                        now);
                }
                else
                {
                    sponsorParticipation = await context.EntryParticipations
                        .SingleAsync(item => item.Id == sponsor.ParticipationId);
                    sponsorParticipation.Status.ShouldBe(EntryParticipationStatus.Active);
                    participation = EntryParticipation.StartUnderRecruiter(
                        TenantId,
                        customer.Id,
                        sponsorParticipation,
                        terms,
                        now);
                }
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                if (authorisedProspectiveRoot)
                {
                    sponsor.ShouldBeNull();
                    var hostAdministratorUserId = await context.Users
                        .IgnoreQueryFilters()
                        .Where(item => item.TenantId == null &&
                                       item.UserName == AbpUserBase.AdminUserName)
                        .Select(item => item.Id)
                        .SingleAsync();
                    var authorityReference = Guid.NewGuid();
                    var attribution = AQGreenRecruitmentAttribution.Create(
                        TenantId,
                        participation.Id,
                        null,
                        AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                        AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                        authorityReference,
                        now.AddSeconds(5),
                        hostAdministratorUserId,
                        "Fresh AQGreen V2 continuous E2E root authority",
                        AQGreenRecruitmentAttributionRules.CurrentVersion);
                    context.AQGreenRecruitmentAttributions.Add(attribution);
                    await context.SaveChangesAsync();
                    context.AQGreenRecruitmentAttributionConfirmations.Add(
                        AQGreenRecruitmentAttributionConfirmation.Confirm(
                            attribution,
                            now.AddSeconds(10),
                            hostAdministratorUserId,
                            AQGreenAttributionConfirmationMethod
                                .AuthorisedProspectiveRootConfirmation,
                            authorityReference,
                            AQGreenRecruitmentAttributionRules.CurrentVersion));
                }
                else
                {
                    sponsor.ShouldNotBeNull();
                    var invitation = await context.ProgrammeInvitations
                        .SingleOrDefaultAsync(item =>
                            item.TenantId == TenantId &&
                            item.ProgrammeKey == "AQGREEN" &&
                            item.ProgrammeParticipationId == sponsorParticipation.Id);
                    if (invitation == null)
                    {
                        invitation = ProgrammeInvitation.Create(
                            TenantId,
                            "AQGREEN",
                            sponsorParticipation.Id);
                        context.ProgrammeInvitations.Add(invitation);
                        await context.SaveChangesAsync();
                    }
                    var attribution = AQGreenRecruitmentAttribution.Create(
                        TenantId,
                        participation.Id,
                        sponsorParticipation.Id,
                        AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                        AQGreenAcquisitionSource.MemberInvitation,
                        invitation.Id,
                        now.AddSeconds(5),
                        null,
                        null,
                        AQGreenRecruitmentAttributionRules.CurrentVersion);
                    context.AQGreenRecruitmentAttributions.Add(attribution);
                    await context.SaveChangesAsync();
                    context.AQGreenRecruitmentAttributionConfirmations.Add(
                        AQGreenRecruitmentAttributionConfirmation.Confirm(
                            attribution,
                            now.AddSeconds(10),
                            user.Id,
                            AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                            invitation.Id,
                            AQGreenRecruitmentAttributionRules.CurrentVersion));
                }
                await context.SaveChangesAsync();
                return new ParticipantFixture(
                    user.Id,
                    userName,
                    customer.Id,
                    participation.Id,
                    sponsor?.ParticipationId,
                    area.Id,
                    label);
            });
        }

        private async Task ConfirmJoiningPaymentAsync(ParticipantFixture participant)
        {
            var initiatedAt = DateTime.UtcNow.AddSeconds(-2);
            var result = await Resolve<ProgrammePaymentConfirmationProcessor>()
                .ProcessAsync(new ConfirmedProgrammePayment(
                    TenantId,
                    participant.CustomerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m,
                    "ZAR",
                    "Test",
                    $"continuous-{participant.ParticipationId:N}",
                    initiatedAt,
                    initiatedAt.AddSeconds(1)));
            result.ParticipationId.ShouldBe(participant.ParticipationId);
            result.ParticipationKind.ShouldBe(ProgrammeParticipationKind.Entry);
            result.WasAlreadyProcessed.ShouldBeFalse();
            result.AwaitingAdministrativeApproval.ShouldBeTrue();
        }

        private async Task AssertAwaitingApprovalAsync(Guid participationId)
        {
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == participationId);
                participation.Status.ShouldBe(
                    EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
                participation.ActivatedAt.ShouldBeNull();
                (await context.AQGreenNetworkPlacements.CountAsync(item =>
                    item.ParticipantId == participationId)).ShouldBe(0);
            });
        }

        private Task ApproveAsync(Guid participationId) =>
            Resolve<AdminProgrammeParticipationAppService>()
                .ApproveProgrammeParticipationAsync(new ApproveProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ParticipationId = participationId
                });

        private async Task AssertActiveApprovalAsync(
            ParticipantFixture participant,
            bool isRoot)
        {
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == participant.ParticipationId);
                participation.Status.ShouldBe(EntryParticipationStatus.Active);
                participation.ActivatedAt.ShouldNotBeNull();
                var placement = await context.AQGreenNetworkPlacements
                    .SingleAsync(item => item.ParticipantId == participant.ParticipationId);
                placement.RulesVersion.ShouldBe(AQGreenPlacementRules.CurrentVersion);
                if (isRoot)
                {
                    participation.ActivatedAt.ShouldBe(placement.PlacedAt);
                    placement.PlacementParentParticipantId.ShouldBeNull();
                    placement.PlacementSlot.ShouldBeNull();
                    placement.CanonicalPath.ShouldBe(string.Empty);
                }
                else
                {
                    placement.PlacedAt.ShouldBeLessThanOrEqualTo(
                        participation.ActivatedAt.Value);
                    placement.PlacementParentParticipantId.ShouldNotBeNull();
                    placement.PlacementSlot.ShouldNotBeNull();
                    placement.CanonicalPath.ShouldNotBeNullOrWhiteSpace();
                }
                (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") ==
                        participant.ParticipationId)).ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(item =>
                    item.IdempotencyKey ==
                        $"Entry:{participant.ParticipationId}:approved")).ShouldBe(1);
                (await context.Users.SingleAsync(item => item.Id == participant.UserId))
                    .Role.ShouldBe(AquaUserRole.Member);
            });
        }

        private async Task<TopologySnapshot> AssertTopologyAsync(
            ParticipantFixture root,
            IReadOnlyList<ParticipantFixture> depthOne,
            IReadOnlyList<ParticipantFixture> depthTwo,
            DateTime cutoff)
        {
            return await UsingDbContextAsync(async context =>
            {
                var placements = await context.AQGreenNetworkPlacements
                    .AsNoTracking()
                    .OrderBy(item => item.CanonicalPath)
                    .ToListAsync();
                placements.Count.ShouldBe(31);
                placements.Select(item => item.PlacementTreeScopeId)
                    .Distinct().ShouldHaveSingleItem();
                placements.ShouldAllBe(item =>
                    item.TenantId == TenantId &&
                    item.RulesVersion == AQGreenPlacementRules.CurrentVersion &&
                    item.PlacedAt <= cutoff);
                var rootPlacement = placements.Single(item =>
                    item.ParticipantId == root.ParticipationId);
                rootPlacement.CanonicalPath.ShouldBe(string.Empty);
                rootPlacement.PlacementParentParticipantId.ShouldBeNull();
                rootPlacement.PlacementSlot.ShouldBeNull();

                var orderedDepthOne = placements
                    .Where(item => item.PlacementParentParticipantId ==
                        root.ParticipationId)
                    .OrderBy(item => item.PlacementSlot)
                    .ToList();
                orderedDepthOne.Count.ShouldBe(5);
                orderedDepthOne.Select(item => item.PlacementSlot)
                    .ShouldBe(new int?[] { 1, 2, 3, 4, 5 });
                orderedDepthOne.Select(item => item.CanonicalPath)
                    .ShouldBe(new[] { "1", "2", "3", "4", "5" });
                orderedDepthOne.Select(item => item.ParticipantId)
                    .ShouldBe(depthOne.Select(item => item.ParticipationId));

                var expectedDepthTwo = new List<Guid>();
                foreach (var parentPlacement in orderedDepthOne)
                {
                    var children = placements
                        .Where(item => item.PlacementParentParticipantId ==
                            parentPlacement.ParticipantId)
                        .OrderBy(item => item.PlacementSlot)
                        .ToList();
                    children.Count.ShouldBe(5);
                    children.Select(item => item.PlacementSlot)
                        .ShouldBe(new int?[] { 1, 2, 3, 4, 5 });
                    children.Select(item => item.CanonicalPath).ShouldBe(
                        Enumerable.Range(1, 5)
                            .Select(slot => $"{parentPlacement.CanonicalPath}{slot}"));
                    expectedDepthTwo.AddRange(children.Select(item => item.ParticipantId));
                }
                expectedDepthTwo.ShouldBe(
                    depthTwo.Select(item => item.ParticipationId));

                var participantIds = placements.Select(item => item.ParticipantId).ToList();
                var active = await context.EntryParticipations
                    .Where(item => participantIds.Contains(item.Id))
                    .ToListAsync();
                active.Count.ShouldBe(31);
                active.ShouldAllBe(item =>
                    item.TenantId == TenantId &&
                    item.Status == EntryParticipationStatus.Active &&
                    item.ActivatedAt.HasValue &&
                    item.ActivatedAt.Value <= cutoff);

                return new TopologySnapshot(
                    rootPlacement.Id,
                    rootPlacement.PlacementTreeScopeId,
                    placements.Select(item => new PlacementSnapshot(
                        item.Id,
                        item.ParticipantId,
                        item.PlacementParentParticipantId,
                        item.PlacementSlot,
                        item.CanonicalPath,
                        item.PlacedAt)).ToList());
            });
        }

        private static void AssertCanonicalClosedWeek(
            LatestClosedCommissionWeekResolver resolver,
            ClosedCommissionWeek week,
            DateTime asOf)
        {
            resolver.IsCanonicalCycle(
                week.PeriodStartUtc,
                week.PeriodEndUtc,
                week.TimeZoneId).ShouldBeTrue();
            week.TimeZoneId.ShouldBe(
                LatestClosedCommissionWeekResolver.CommissionTimeZoneId);
            week.PeriodEndUtc.ShouldBeLessThan(asOf);
            AQGreenCommissionWeek.FromStartUtc(week.PeriodStartUtc)
                .EndExclusiveUtc.AddTicks(-1).ShouldBe(week.PeriodEndUtc);
        }

        private async Task<Guid> CreateAcceptedLoanWithSatisfiedInitialRequirementsAsync(
            ParticipantFixture root,
            IReadOnlyCollection<ParticipantFixture> allParticipants)
        {
            return await UsingDbContextAsync(async context =>
            {
                var participations = await context.EntryParticipations
                    .Where(item => allParticipants.Select(fixture => fixture.ParticipationId)
                        .Contains(item.Id))
                    .ToListAsync();
                var rootParticipation = participations.Single(item =>
                    item.Id == root.ParticipationId);
                if (!await context.Memberships.AnyAsync(item =>
                        item.MembershipType == MembershipType.Onyx && item.IsActive))
                {
                    context.Memberships.Add(Membership.Create(
                        TenantId,
                        "Continuous E2E Onyx",
                        "Dummy Onyx membership for continuous backend E2E",
                        MembershipType.Onyx));
                }

                var now = DateTime.UtcNow.AddMinutes(-1);
                var loanTerms = OnyxLoanTerms.Create(
                    $"continuous-loan-{Guid.NewGuid():N}"[..32],
                    now.AddDays(-1),
                    6120m,
                    30m,
                    3,
                    4,
                    200m);
                var loan = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                    rootParticipation,
                    participations,
                    new EntryNetworkQualificationEvaluator(),
                    loanTerms,
                    now);
                loan.AcceptByMember(
                    root.UserId,
                    "Accepted continuous E2E loan agreement terms",
                    now.AddSeconds(1));
                loan.ApproveByAdministrator(
                    AbpSession.UserId.Value,
                    now.AddSeconds(2));

                for (var requirement = 1; requirement <= 4; requirement++)
                {
                    var payment = MemberPayment.CreatePending(
                        TenantId,
                        root.CustomerId,
                        MemberPaymentPurpose.OnyxLoanRepayment,
                        200m,
                        "Test",
                        $"continuous-loan-{loan.Id:N}-{requirement}",
                        now.AddSeconds(2 + requirement),
                        "ZAR");
                    payment.Confirm(now.AddSeconds(3 + requirement));
                    loan.ApplyConfirmedRepayment(payment, requirement);
                    context.MemberPayments.Add(payment);
                }
                context.OnyxLoanAgreements.Add(loan);
                await context.SaveChangesAsync();
                loan.WeeklyRequirements.ShouldAllBe(item =>
                    item.Status == OnyxLoanWeeklyRequirementStatus.Satisfied);
                return loan.Id;
            });
        }

        private async Task AssertGraduationAsync(
            ParticipantFixture root,
            OnyxGraduationDecisionDto result,
            TopologySnapshot topology)
        {
            await UsingDbContextAsync(async context =>
            {
                var decision = await context.OnyxGraduationDecisions
                    .SingleAsync(item => item.Id == result.DecisionId);
                decision.EntryParticipationId.ShouldBe(root.ParticipationId);
                decision.CustomerId.ShouldBe(root.CustomerId);
                decision.OnyxParticipationId.ShouldBe(result.OnyxParticipationId);
                decision.StructuralModel.ShouldBe(
                    AQGreenGraduationStructuralModel.PlacementV2);
                decision.GraduationRulesVersion.ShouldBe(
                    OnyxGraduationRules.CurrentVersion);
                decision.LoanAgreementId.ShouldBe(result.LoanAgreementId);
                decision.LoanWasAccepted.ShouldBeTrue();
                decision.LoanWasAdministratorApproved.ShouldBeTrue();
                var loan = await context.OnyxLoanAgreements.SingleAsync(item =>
                    item.Id == result.LoanAgreementId);
                loan.EntryParticipationId.ShouldBe(root.ParticipationId);
                loan.CustomerId.ShouldBe(root.CustomerId);
                loan.Status.ShouldBe(OnyxLoanAgreementStatus.Active);
                loan.MemberAcceptedAt.ShouldNotBeNull();
                loan.MemberAcceptedByUserId.ShouldBe(root.UserId);
                loan.ApprovedAt.ShouldNotBeNull();
                loan.ApprovedByAdministratorUserId.ShouldNotBeNull();
                loan.EffectiveAt.ShouldNotBeNull();
                decision.EvaluatedLoanTermsVersion.ShouldBe(loan.TermsVersion);
                var onyx = await context.OnyxParticipations.SingleAsync(item =>
                    item.Id == result.OnyxParticipationId);
                onyx.AdmissionRoute.ShouldBe(OnyxAdmissionRoute.EntryGraduation);
                onyx.EntryParticipationId.ShouldBe(root.ParticipationId);
                onyx.LoanAgreementId.ShouldBe(loan.Id);
                onyx.TermsVersion.ShouldBe(loan.TermsVersion);
                onyx.TermsEffectiveFrom.ShouldBe(loan.EffectiveAt.Value);
                onyx.DirectEntryAmount.ShouldBe(loan.PrincipalAmount);
                onyx.Currency.ShouldBe(loan.Currency);
                var evidence = await context.AQGreenV2GraduationEvidence
                    .Include(item => item.Nodes)
                    .SingleAsync(item => item.Id == result.DecisionId);
                evidence.EvaluatedStructuralCompletionLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level2);
                evidence.QualifyingDepth1Count.ShouldBe(5);
                evidence.QualifyingDepth2Count.ShouldBe(25);
                evidence.EvidenceNodeCount.ShouldBe(31);
                evidence.Nodes.Count.ShouldBe(31);
                evidence.StructuralQualificationRulesVersion.ShouldBe(
                    AQGreenStructuralQualificationRules.CurrentVersion);
                evidence.Nodes.OrderBy(item => item.CanonicalOrdinal)
                    .Select(item => item.SourcePlacementId)
                    .ShouldBe(topology.Placements
                        .OrderBy(item => CanonicalOrdinal(item.CanonicalPath))
                        .Select(item => item.Id));
                (await context.OnyxParticipations.CountAsync(item =>
                    item.CustomerId == root.CustomerId)).ShouldBe(1);
                (await context.OnyxGraduationDecisions.CountAsync(item =>
                    item.EntryParticipationId == root.ParticipationId)).ShouldBe(1);
            });
        }

        private async Task SeedCommissionPrerequisitesAsync(
            string suffix,
            ClosedCommissionWeek primaryWeek,
            ClosedCommissionWeek controlWeek)
        {
            await UsingDbContextAsync(async context =>
            {
                var recordedAt = DateTime.UtcNow;
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        TenantId,
                        true,
                        recordedAt.AddMinutes(-1),
                        recordedAt,
                        null,
                        "AQGreen V2 continuous E2E active baseline",
                        AreaActivationStateRecordKind.ObservedBaseline));
                context.EntryCommissionTermsVersions.AddRange(
                    EntryCommissionTermsVersion.Create(
                        $"continuous-primary-{suffix}",
                        primaryWeek.PeriodStartUtc,
                        150m,
                        250m,
                        1250m),
                    EntryCommissionTermsVersion.Create(
                        $"continuous-control-{suffix}",
                        controlWeek.PeriodStartUtc,
                        150m,
                        250m,
                        1250m));
                await context.SaveChangesAsync();
            });
        }

        private async Task<AQGreenWeeklySalesEligibilityDecisionDto> ReviewSalesAsync(
            Guid participantId,
            ClosedCommissionWeek week,
            int spray,
            int oneLitre,
            int fiveLitre,
            string evidenceReference,
            bool requireHeldTransition)
        {
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();
            if (requireHeldTransition)
            {
                var held = await service.BeginReviewAsync(
                    new BeginAQGreenWeeklySalesReviewInput
                    {
                        TenantId = TenantId,
                        ParticipantId = participantId,
                        CommissionWeekStartUtc = week.PeriodStartUtc
                    });
                held.ReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.HeldForEvidence);
                held.ThresholdResult.ShouldBeNull();
                held.ReviewedAt.ShouldBeNull();
            }

            using (SalesClock.Set(week.PeriodEndUtc.AddMinutes(5)))
            {
                var confirmed = await service.ConfirmAsync(
                    new ConfirmAQGreenWeeklySalesEligibilityInput
                    {
                        TenantId = TenantId,
                        ParticipantId = participantId,
                        CommissionWeekStartUtc = week.PeriodStartUtc,
                        SprayQuantity = spray,
                        OneLitreQuantity = oneLitre,
                        FiveLitreQuantity = fiveLitre,
                        EvidenceReferences = new List<string> { evidenceReference }
                    });
                confirmed.ReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.Confirmed);
                confirmed.ReviewedSprayQuantity.ShouldBe(spray);
                confirmed.ReviewedOneLitreQuantity.ShouldBe(oneLitre);
                confirmed.ReviewedFiveLitreQuantity.ShouldBe(fiveLitre);
                confirmed.ReviewedAt.ShouldBe(week.PeriodEndUtc.AddMinutes(5));
                confirmed.ReviewedByUserId.ShouldBe(AbpSession.UserId);
                confirmed.SalesEligibilityRulesVersion.ShouldBe(
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                return confirmed;
            }
        }

        private async Task<CommissionCalculationResultDto> CalculateAsync(
            ClosedCommissionWeek week)
        {
            using (CommissionSelector.Enable(TenantId, week.PeriodEndUtc))
            using (var unitOfWork = Resolve<IUnitOfWorkManager>().Begin(
                       new UnitOfWorkOptions { IsTransactional = true }))
            using (Resolve<IUnitOfWorkManager>().Current.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
                await Resolve<IWeeklyCommissionCalculationLock>().AcquireAsync();
                var result = await Resolve<IWeeklyCommissionCalculator>()
                    .CalculateEntryAsync(
                        TenantId,
                        week,
                        week.PeriodEndUtc.AddMinutes(10));
                await unitOfWork.CompleteAsync();
                return result;
            }
        }

        private async Task<LedgerSnapshot> LoadAndAssertPrimaryLedgerAsync(
            ParticipantFixture root,
            ClosedCommissionWeek week,
            AQGreenWeeklySalesEligibilityDecisionDto sales,
            TopologySnapshot topology)
        {
            return await UsingDbContextAsync(async context =>
            {
                var period = await context.EntryCommissionPeriods.SingleAsync(item =>
                    item.TenantId == TenantId &&
                    item.PeriodStart == week.PeriodStartUtc &&
                    item.PeriodEnd == week.PeriodEndUtc);
                var ledger = await context.EntryWeeklyCommissions
                    .Include(item => item.Components)
                    .SingleAsync(item =>
                        item.EntryParticipationId == root.ParticipationId &&
                        item.CommissionPeriodId == period.Id);
                ledger.CustomerId.ShouldBe(root.CustomerId);
                ledger.StructuralModel.ShouldBe(
                    AQGreenCommissionStructuralModel.PlacementV2);
                ledger.HighestQualifiedNetworkLevel.ShouldBe(2);
                ledger.HighestCommissionedLevel.ShouldBe(2);
                ledger.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.Earned);
                ledger.HoldReason.ShouldBeNull();
                ledger.TotalAmount.ShouldBe(400m);
                ledger.Currency.ShouldBe("ZAR");
                ledger.CommissionDecisionRulesVersion.ShouldBe(
                    AQGreenCommissionDecisionRules.CurrentVersion);
                ledger.Components.OrderBy(item => item.Level)
                    .Select(item => (item.Level, item.Amount))
                    .ShouldBe(new[] { (1, 150m), (2, 250m) });

                var evidence = await context.AQGreenV2WeeklyCommissionEvidence
                    .Include(item => item.Nodes)
                    .SingleAsync(item => item.Id == ledger.Id);
                evidence.EntryParticipationId.ShouldBe(root.ParticipationId);
                evidence.PlacementTreeScopeId.ShouldBe(topology.ScopeId);
                evidence.WeeklySalesEligibilityDecisionId.ShouldBe(sales.Id);
                evidence.QualifiedStructuralLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level2);
                evidence.CommissionedLevel.ShouldBe(2);
                evidence.QualifyingDepth1Count.ShouldBe(5);
                evidence.QualifyingDepth2Count.ShouldBe(25);
                evidence.QualifyingDepth3Count.ShouldBe(0);
                evidence.EvidenceNodeCount.ShouldBe(31);
                evidence.Nodes.Count.ShouldBe(31);
                evidence.Cutoff.ShouldBe(period.PeriodEnd);
                (week.PeriodEndUtc - evidence.Cutoff).ShouldBeLessThan(
                    TimeSpan.FromTicks(10),
                    "PostgreSQL persists timestamps at microsecond precision");
                evidence.PlacementRulesVersion.ShouldBe(
                    AQGreenPlacementRules.CurrentVersion);
                evidence.StructuralQualificationRulesVersion.ShouldBe(
                    AQGreenStructuralQualificationRules.CurrentVersion);
                evidence.SalesEligibilityRulesVersion.ShouldBe(
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                evidence.CommissionDecisionRulesVersion.ShouldBe(
                    AQGreenCommissionDecisionRules.CurrentVersion);
                evidence.SalesReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.Confirmed);
                evidence.SalesThresholdResult.ShouldBe(
                    AQGreenWeeklySalesThresholdResult.Met);
                evidence.SalesReviewedByUserId.ShouldBe(sales.ReviewedByUserId);

                var durableSales = await context.AQGreenWeeklySalesEligibilityDecisions
                    .Include(item => item.EvidenceReferences)
                    .SingleAsync(item => item.Id == sales.Id);
                durableSales.ParticipantId.ShouldBe(root.ParticipationId);
                durableSales.CommissionWeekStartUtc.ShouldBe(week.PeriodStartUtc);
                durableSales.ReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.Confirmed);
                durableSales.ThresholdResult.ShouldBe(
                    AQGreenWeeklySalesThresholdResult.Met);
                durableSales.ReviewedSprayQuantity.ShouldBe(5);
                durableSales.ReviewedOneLitreQuantity.ShouldBe(5);
                durableSales.ReviewedFiveLitreQuantity.ShouldBe(5);
                durableSales.ReviewedAt.ShouldNotBeNull();
                sales.ReviewedAt.ShouldNotBeNull();
                (durableSales.ReviewedAt.Value - sales.ReviewedAt.Value)
                    .Duration().ShouldBeLessThan(TimeSpan.FromTicks(10),
                        "PostgreSQL persists timestamps at microsecond precision");
                durableSales.ReviewedByUserId.ShouldBe(sales.ReviewedByUserId);
                durableSales.SalesEligibilityRulesVersion.ShouldBe(
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                durableSales.EvidenceReferences.ShouldHaveSingleItem()
                    .TechnicalReference.ShouldBe("continuous:root:primary");
                var terms = await context.EntryCommissionTermsVersions
                    .SingleAsync(item => item.Version == ledger.RulesVersion);
                terms.LevelOneComponentAmount.ShouldBe(150m);
                terms.LevelTwoComponentAmount.ShouldBe(250m);
                terms.EffectiveAt.ShouldBe(week.PeriodStartUtc);
                ledger.Components.Single(item => item.Level == 1).Amount
                    .ShouldBe(terms.LevelOneComponentAmount);
                ledger.Components.Single(item => item.Level == 2).Amount
                    .ShouldBe(terms.LevelTwoComponentAmount);

                return new LedgerSnapshot(
                    period.Id,
                    ledger.Id,
                    ledger.RulesVersion,
                    ledger.PayoutStatus,
                    ledger.HighestQualifiedNetworkLevel,
                    ledger.HighestCommissionedLevel,
                    ledger.TotalAmount,
                    ledger.Components.OrderBy(item => item.Level)
                        .Select(item => item.Amount).ToArray());
            });
        }

        private static void AssertPrimaryReplay(
            AQGreenV2WeeklyCommissionEvidenceReplayResult replay)
        {
            replay.QualifiedStructuralLevel.ShouldBe(
                AQGreenStructuralCompletionLevel.Level2);
            replay.CommissionedLevel.ShouldBe(2);
            replay.TotalAmount.ShouldBe(400m);
            replay.SalesReviewStatus.ShouldBe(
                AQGreenWeeklySalesReviewStatus.Confirmed);
            replay.SalesThresholdResult.ShouldBe(
                AQGreenWeeklySalesThresholdResult.Met);
            replay.EvidenceNodeCount.ShouldBe(31);
        }

        private async Task AssertNoDuplicatePrimaryLedgerAsync(
            Guid participantId,
            LedgerSnapshot expected)
        {
            await UsingDbContextAsync(async context =>
            {
                var ledgers = await context.EntryWeeklyCommissions
                    .Include(item => item.Components)
                    .Where(item => item.CommissionPeriodId == expected.PeriodId &&
                                   item.EntryParticipationId == participantId)
                    .ToListAsync();
                var ledger = ledgers.ShouldHaveSingleItem();
                ledger.Id.ShouldBe(expected.LedgerId);
                ledger.Components.Count.ShouldBe(2);
                (await context.AQGreenV2WeeklyCommissionEvidence.CountAsync(item =>
                    item.Id == expected.LedgerId)).ShouldBe(1);
                (await context.AQGreenV2WeeklyCommissionEvidenceNodes.CountAsync(item =>
                    item.EvidenceId == expected.LedgerId)).ShouldBe(31);
            });
        }

        private async Task<LedgerSnapshot> LoadAndAssertControlLedgerAsync(
            ParticipantFixture root,
            ClosedCommissionWeek week,
            AQGreenWeeklySalesEligibilityDecisionDto sales,
            TopologySnapshot topology)
        {
            return await UsingDbContextAsync(async context =>
            {
                var period = await context.EntryCommissionPeriods.SingleAsync(item =>
                    item.PeriodStart == week.PeriodStartUtc &&
                    item.PeriodEnd == week.PeriodEndUtc);
                var ledger = await context.EntryWeeklyCommissions
                    .Include(item => item.Components)
                    .SingleAsync(item =>
                        item.CommissionPeriodId == period.Id &&
                        item.EntryParticipationId == root.ParticipationId);
                ledger.StructuralModel.ShouldBe(
                    AQGreenCommissionStructuralModel.PlacementV2);
                ledger.HighestQualifiedNetworkLevel.ShouldBe(2);
                ledger.HighestCommissionedLevel.ShouldBe(0);
                ledger.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.NotEarned);
                ledger.TotalAmount.ShouldBe(0m);
                ledger.Components.ShouldBeEmpty();
                ledger.HoldReason.ShouldBeNull();
                var evidence = await context.AQGreenV2WeeklyCommissionEvidence
                    .SingleAsync(item => item.Id == ledger.Id);
                evidence.EntryParticipationId.ShouldBe(root.ParticipationId);
                evidence.PlacementTreeScopeId.ShouldBe(topology.ScopeId);
                evidence.WeeklySalesEligibilityDecisionId.ShouldBe(sales.Id);
                evidence.QualifiedStructuralLevel.ShouldBe(
                    AQGreenStructuralCompletionLevel.Level2);
                evidence.CommissionedLevel.ShouldBe(0);
                evidence.SalesReviewStatus.ShouldBe(
                    AQGreenWeeklySalesReviewStatus.Confirmed);
                evidence.SalesThresholdResult.ShouldBe(
                    AQGreenWeeklySalesThresholdResult.NotMet);
                return new LedgerSnapshot(
                    period.Id,
                    ledger.Id,
                    ledger.RulesVersion,
                    ledger.PayoutStatus,
                    ledger.HighestQualifiedNetworkLevel,
                    ledger.HighestCommissionedLevel,
                    ledger.TotalAmount,
                    Array.Empty<decimal>());
            });
        }

        private async Task ProveReplayIgnoresMutableCurrentUserAsync(
            long userId,
            Guid ledgerId)
        {
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == userId);
                user.IsActive = false;
                await context.SaveChangesAsync();
            });
            var replay = await InUnitOfWorkAsync(() =>
                Resolve<IAQGreenV2WeeklyCommissionEvidenceReplayValidator>()
                    .ValidateAsync(ledgerId));
            AssertPrimaryReplay(replay);
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == userId);
                user.IsActive = true;
                await context.SaveChangesAsync();
            });
        }

        private async Task AssertTopologyUnchangedAsync(TopologySnapshot expected)
        {
            await UsingDbContextAsync(async context =>
            {
                var current = await context.AQGreenNetworkPlacements
                    .AsNoTracking()
                    .Where(item => item.PlacementTreeScopeId == expected.ScopeId)
                    .ToListAsync();
                current.Count.ShouldBe(31);
                foreach (var placement in expected.Placements)
                {
                    var actual = current.Single(item => item.Id == placement.Id);
                    actual.ParticipantId.ShouldBe(placement.ParticipantId);
                    actual.PlacementParentParticipantId.ShouldBe(
                        placement.ParentParticipantId);
                    actual.PlacementSlot.ShouldBe(placement.Slot);
                    actual.CanonicalPath.ShouldBe(placement.CanonicalPath);
                    actual.PlacedAt.ShouldBe(placement.PlacedAt);
                }
            });
        }

        private async Task WriteSnapshotAsync(
            string postgresVersion,
            ParticipantFixture root,
            TopologySnapshot topology,
            ClosedCommissionWeek primaryWeek,
            AQGreenStructuralCompletionResult b4,
            OnyxGraduationDecisionDto graduation,
            AQGreenWeeklySalesEligibilityDecisionDto sales,
            LedgerSnapshot ledger,
            AQGreenV2WeeklyCommissionEvidenceReplayResult replay,
            LedgerSnapshot control)
        {
            var markerDirectory = Environment.GetEnvironmentVariable("REPRO_MARKER_DIR");
            if (string.IsNullOrWhiteSpace(markerDirectory))
                throw new InvalidOperationException(
                    "REPRO_MARKER_DIR is required for the continuous E2E evidence snapshot.");
            Directory.CreateDirectory(markerDirectory);
            var snapshot = new
            {
                PostgreSQL = postgresVersion,
                RootUserName = root.UserName,
                root.UserId,
                root.CustomerId,
                AQGreenParticipationId = root.ParticipationId,
                PlacementId = topology.RootPlacementId,
                PlacementTreeScopeId = topology.ScopeId,
                AQGreenStatus = "Active",
                Depth1 = 5,
                Depth2 = 25,
                StructuralObservations = 31,
                B4Level = (int)b4.StructuralCompletionLevel,
                B4RulesVersion = b4.RulesVersion,
                B4CutoffUtc = b4.Cutoff,
                B5_1 = "Level 2 progress projection",
                B5_2DecisionId = graduation.DecisionId,
                graduation.OnyxParticipationId,
                B5_3DecisionId = sales.Id,
                B5_3 = $"{sales.ReviewStatus} + {sales.ThresholdResult}",
                CommissionWeek = new
                {
                    primaryWeek.PeriodStartUtc,
                    primaryWeek.PeriodEndUtc,
                    primaryWeek.TimeZoneId
                },
                Sales = new[]
                {
                    sales.ReviewedSprayQuantity,
                    sales.ReviewedOneLitreQuantity,
                    sales.ReviewedFiveLitreQuantity
                },
                SalesReviewedAtUtc = sales.ReviewedAt,
                SalesReviewerUserId = sales.ReviewedByUserId,
                SalesEligibilityRulesVersion = sales.SalesEligibilityRulesVersion,
                CommissionLedgerId = ledger.LedgerId,
                QualifiedLevel = ledger.QualifiedLevel,
                CommissionedLevel = ledger.CommissionedLevel,
                Components = ledger.Components,
                Total = ledger.Total,
                LedgerCount = 1,
                FinancialTermsVersion = ledger.RulesVersion,
                Replay = new
                {
                    Result = "PASS",
                    QualifiedLevel = (int)replay.QualifiedStructuralLevel,
                    replay.CommissionedLevel,
                    replay.TotalAmount,
                    replay.EvidenceNodeCount
                },
                Control = new
                {
                    QualifiedLevel = control.QualifiedLevel,
                    CommissionedLevel = control.CommissionedLevel,
                    control.Total,
                    Status = control.Status.ToString()
                },
                AllSameParticipant = true,
                DirectSqlBusinessFabrication = false,
                AdminUiUsed = false
            };
            await File.WriteAllTextAsync(
                Path.Combine(markerDirectory, "aqgreen-v2-continuous-e2e.json"),
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        private async Task<T> InUnitOfWorkAsync<T>(Func<Task<T>> action)
        {
            using var unitOfWork = Resolve<IUnitOfWorkManager>().Begin();
            var result = await action();
            await unitOfWork.CompleteAsync();
            return result;
        }

        private static long CanonicalOrdinal(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            var parts = path.Select(character => character - '0').ToArray();
            return parts.Length switch
            {
                1 => parts[0],
                2 => 5 + (parts[0] - 1) * 5 + parts[1],
                _ => throw new InvalidOperationException(
                    "The Level-2 graduation manifest cannot contain a deeper path.")
            };
        }

        private static bool IsPostgreSqlRegressionMode() =>
            string.Equals(
                Environment.GetEnvironmentVariable("REPRO_PG"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        private AQGreenPlacementV2TestApprovalGate ApprovalGate =>
            (AQGreenPlacementV2TestApprovalGate)
                Resolve<IAQGreenPlacementV2ApprovalGate>();

        private AQGreenPlacementV2TestProgressGate ProgressGate =>
            (AQGreenPlacementV2TestProgressGate)
                Resolve<IAQGreenPlacementV2ProgressGate>();

        private AQGreenPlacementV2TestStructuralEvaluator StructuralEvaluator =>
            (AQGreenPlacementV2TestStructuralEvaluator)
                Resolve<IAQGreenStructuralCompletionEvaluator>();

        private AQGreenV2ContinuousGraduationSelector GraduationSelector =>
            (AQGreenV2ContinuousGraduationSelector)
                Resolve<IAQGreenGraduationStructuralModelSelector>();

        private AQGreenV2ContinuousCommissionSelector CommissionSelector =>
            (AQGreenV2ContinuousCommissionSelector)
                Resolve<IAQGreenCommissionStructuralModelSelector>();

        private AQGreenV2ContinuousSalesReviewGate SalesReviewGate =>
            (AQGreenV2ContinuousSalesReviewGate)
                Resolve<IAQGreenWeeklySalesReviewGate>();

        private AQGreenV2ContinuousSalesClock SalesClock =>
            (AQGreenV2ContinuousSalesClock)
                Resolve<IAQGreenWeeklySalesEligibilityClock>();

        private sealed record ParticipantFixture(
            long UserId,
            string UserName,
            int CustomerId,
            Guid ParticipationId,
            Guid? SponsorParticipationId,
            Guid AreaId,
            string Label);

        private sealed record PlacementSnapshot(
            Guid Id,
            Guid ParticipantId,
            Guid? ParentParticipantId,
            int? Slot,
            string CanonicalPath,
            DateTime PlacedAt);

        private sealed record TopologySnapshot(
            Guid RootPlacementId,
            Guid ScopeId,
            IReadOnlyList<PlacementSnapshot> Placements);

        private sealed record LedgerSnapshot(
            Guid PeriodId,
            Guid LedgerId,
            string RulesVersion,
            WeeklyCommissionPayoutStatus Status,
            int QualifiedLevel,
            int CommissionedLevel,
            decimal Total,
            IReadOnlyList<decimal> Components);
    }
}
