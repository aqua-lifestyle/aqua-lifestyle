using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
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
using AqualLifeStyle.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    [CollectionDefinition("AQGreen B3.2 PostgreSQL application path", DisableParallelization = true)]
    public sealed class AQGreenPlacementV2ApprovalPostgreSqlCollection
    {
    }

    [Collection("AQGreen B3.2 PostgreSQL application path")]
    public sealed class AQGreenPlacementV2ApprovalPostgreSqlTests
        : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task EnabledV2Approval_CommitsPlacementActivationDecisionRoleAndOutbox_AndRetryIsExact()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            using (((AQGreenPlacementV2TestApprovalGate)IocManager
                       .Resolve<IAQGreenPlacementV2ApprovalGate>())
                       .Enable(fixture.ParticipantId))
            {
                var service = IocManager.Resolve<AdminProgrammeParticipationAppService>();
                await service.ApproveProgrammeParticipationAsync(new ApproveProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ParticipationId = fixture.ParticipantId
                });
                await service.ApproveProgrammeParticipationAsync(new ApproveProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ParticipationId = fixture.ParticipantId
                });
            }

            await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == fixture.ParticipantId);
                participation.Status.ShouldBe(EntryParticipationStatus.Active);
                participation.ActivatedAt.ShouldNotBeNull();

                var placement = await context.AQGreenNetworkPlacements
                    .SingleAsync(item => item.ParticipantId == fixture.ParticipantId);
                placement.PlacementTreeScopeId.ShouldBe(fixture.ScopeId);
                placement.PlacementParentParticipantId.ShouldBe(fixture.SponsorParticipantId);
                placement.PlacementSlot.ShouldBe(1);
                placement.CanonicalPath.ShouldBe("1");

                (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") == fixture.ParticipantId))
                    .ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(item =>
                    item.IdempotencyKey == $"Entry:{fixture.ParticipantId}:approved"))
                    .ShouldBe(1);

                var user = await context.Users.SingleAsync(item =>
                    item.Id == fixture.UserId && item.TenantId == 1);
                user.Role.ShouldBe(AquaUserRole.Member);
                var roleNames = await (
                        from userRole in context.UserRoles
                        join role in context.Roles on userRole.RoleId equals role.Id
                        where userRole.UserId == fixture.UserId && userRole.TenantId == 1
                        select role.Name)
                    .ToListAsync();
                roleNames.ShouldBe(new[] { "Member" });
            });
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Fact]
        public async Task EnabledV2Approval_MissingAttributionFailsClosedWithoutV1Fallback()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(includeAttribution: false);
            using (((AQGreenPlacementV2TestApprovalGate)IocManager
                       .Resolve<IAQGreenPlacementV2ApprovalGate>())
                       .Enable(fixture.ParticipantId))
            {
                var service = IocManager.Resolve<AdminProgrammeParticipationAppService>();
                await Should.ThrowAsync<AQGreenPlacementAllocationNotFoundException>(() =>
                    service.ApproveProgrammeParticipationAsync(
                        new ApproveProgrammeParticipationInput
                        {
                            Programme = AdminProgrammeType.Entry,
                            ParticipationId = fixture.ParticipantId
                        }));
            }

            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Fact]
        public async Task ApprovalUserSessionLock_UsesSameOuterBackendAcrossRequiresNewTransaction()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            var userId = NewLockTestUserId();
            var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
            var contextProvider = IocManager.Resolve<
                IDbContextProvider<AqualLifeStyleDbContext>>();
            var approvalLock = IocManager.Resolve<IHostedPaymentCheckoutLock>();

            using (var outer = unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = false
            }))
            {
                var outerContext = contextProvider.GetDbContext();
                await approvalLock.AcquireProgrammeApprovalUserSessionAsync(userId);
                var ownerConnection = (NpgsqlConnection)outerContext.Database.GetDbConnection();
                var acquireBackendPid = await ReadBackendPidAsync(ownerConnection);
                ownerConnection.State.ShouldBe(System.Data.ConnectionState.Open);

                try
                {
                    using (var inner = unitOfWorkManager.Begin(new UnitOfWorkOptions
                    {
                        Scope = TransactionScopeOption.RequiresNew,
                        IsTransactional = true,
                        IsolationLevel = IsolationLevel.ReadCommitted
                    }))
                    {
                        var innerContext = contextProvider.GetDbContext();
                        var innerConnection =
                            (NpgsqlConnection)innerContext.Database.GetDbConnection();
                        var innerBackendPid = await ReadBackendPidAsync(innerConnection);
                        innerBackendPid.ShouldNotBe(acquireBackendPid);
                        await inner.CompleteAsync();
                    }
                }
                finally
                {
                    var releaseContext = contextProvider.GetDbContext();
                    ReferenceEquals(releaseContext, outerContext).ShouldBeTrue();
                    var releaseConnection =
                        (NpgsqlConnection)releaseContext.Database.GetDbConnection();
                    var releaseBackendPid = await ReadBackendPidAsync(releaseConnection);
                    releaseBackendPid.ShouldBe(acquireBackendPid);
                    await approvalLock.ReleaseProgrammeApprovalUserSessionAsync(userId);
                }

                ownerConnection.State.ShouldBe(System.Data.ConnectionState.Closed);
                await outer.CompleteAsync();
            }

            await AssertApprovalUserLockAvailableAsync(userId);
        }

        [Fact]
        public async Task ApprovalUserSessionLock_ReleasesAfterPreTransactionFailure()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            var userId = NewLockTestUserId();
            await Should.ThrowAsync<InvalidOperationException>(() =>
                ExecuteApprovalUserLockLifecycleAsync(
                    userId,
                    () => throw new InvalidOperationException(
                        "Injected failure before approval transaction creation.")));
            await AssertApprovalUserLockAvailableAsync(userId);
        }

        [Fact]
        public async Task ApprovalUserSessionLock_ReleasesAfterCancellationInsideTransaction()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            var userId = NewLockTestUserId();
            await Should.ThrowAsync<OperationCanceledException>(() =>
                ExecuteApprovalUserLockLifecycleAsync(userId, async () =>
                {
                    var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
                    using (unitOfWorkManager.Begin(new UnitOfWorkOptions
                    {
                        Scope = TransactionScopeOption.RequiresNew,
                        IsTransactional = true,
                        IsolationLevel = IsolationLevel.ReadCommitted
                    }))
                    {
                        await Task.FromCanceled(new CancellationToken(canceled: true));
                    }
                }));
            await AssertApprovalUserLockAvailableAsync(userId);
        }

        [Fact]
        public async Task EnabledV2Approval_DatabaseCancellationReleasesUserSessionLock()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            Exception outcome;
            await using (await InstallRoleSleepTriggerAsync(fixture))
            using (EnableV2(fixture.ParticipantId))
            {
                var approval = CaptureAsync(() => ApproveAsync(fixture.ParticipantId));
                var backendPid = await WaitForSleepingRoleUpdateAsync();
                await CancelBackendAsync(backendPid);
                outcome = await approval;
            }

            outcome.ShouldNotBeNull();
            FindInnermostException(outcome).ShouldBeOfType<PostgresException>()
                .SqlState.ShouldBe("57014");
            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Fact]
        public async Task EnabledV2Approval_ActiveWithoutPlacementIsVisibleIntegrityFailure()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == fixture.ParticipantId);
                participation.ApproveByAdministrator(
                    AbpSession.UserId.Value,
                    DateTime.UtcNow);
                await context.SaveChangesAsync();
            });

            using (((AQGreenPlacementV2TestApprovalGate)IocManager
                       .Resolve<IAQGreenPlacementV2ApprovalGate>())
                       .Enable(fixture.ParticipantId))
            {
                var service = IocManager.Resolve<AdminProgrammeParticipationAppService>();
                var exception = await Should.ThrowAsync<AQGreenPlacementConflictException>(() =>
                    service.ApproveProgrammeParticipationAsync(
                        new ApproveProgrammeParticipationInput
                        {
                            Programme = AdminProgrammeType.Entry,
                            ParticipationId = fixture.ParticipantId
                        }));
                exception.Message.ShouldContain("missing its permanent placement");
            }
        }

        [Fact]
        public async Task EnabledV2Approval_ActiveWithPlacementOutsideSponsorSubtreeFailsIntegrity()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            var otherTree = await SeedSponsoredAwaitingApprovalAsync();
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(item =>
                    item.Id == fixture.ParticipantId);
                participation.ApproveByAdministrator(AbpSession.UserId.Value, DateTime.UtcNow);
                var otherRoot = await context.AQGreenNetworkPlacements.SingleAsync(item =>
                    item.ParticipantId == otherTree.SponsorParticipantId);
                context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateChild(
                    otherRoot,
                    fixture.ParticipantId,
                    1,
                    DateTime.UtcNow,
                    AQGreenPlacementRules.CurrentVersion));
                await context.SaveChangesAsync();
            });

            using (EnableV2(fixture.ParticipantId))
            {
                await Should.ThrowAsync<AQGreenPlacementConflictException>(() =>
                    ApproveAsync(fixture.ParticipantId));
            }
        }

        [Fact]
        public async Task EnabledV2Approval_AwaitingWithPlacementRequiresReconciliation()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            await UsingDbContextAsync(async context =>
            {
                var root = await context.AQGreenNetworkPlacements.SingleAsync(item =>
                    item.ParticipantId == fixture.SponsorParticipantId);
                context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateChild(
                    root,
                    fixture.ParticipantId,
                    1,
                    DateTime.UtcNow,
                    AQGreenPlacementRules.CurrentVersion));
                await context.SaveChangesAsync();
            });

            using (((AQGreenPlacementV2TestApprovalGate)IocManager
                       .Resolve<IAQGreenPlacementV2ApprovalGate>())
                       .Enable(fixture.ParticipantId))
            {
                var service = IocManager.Resolve<AdminProgrammeParticipationAppService>();
                var exception = await Should.ThrowAsync<AQGreenPlacementConflictException>(() =>
                    service.ApproveProgrammeParticipationAsync(
                        new ApproveProgrammeParticipationInput
                        {
                            Programme = AdminProgrammeType.Entry,
                            ParticipationId = fixture.ParticipantId
                        }));
                exception.Message.ShouldContain("already has a placement");
            }
        }

        [Fact]
        public async Task DefaultDisabledGate_PreservesV1ApprovalWithoutAttributionOrPlacement()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(includeAttribution: false);
            await ApproveAsync(fixture.ParticipantId);

            await UsingDbContextAsync(async context =>
            {
                (await context.EntryParticipations.SingleAsync(item =>
                    item.Id == fixture.ParticipantId)).Status.ShouldBe(
                    EntryParticipationStatus.Active);
                (await context.AQGreenNetworkPlacements.CountAsync(item =>
                    item.ParticipantId == fixture.ParticipantId)).ShouldBe(0);
            });
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Fact]
        public async Task EnabledV2Approval_AuthorisedProspectiveRootRequiresBootstrap()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(prospectiveRoot: true);
            using (EnableV2(fixture.ParticipantId))
            {
                await Should.ThrowAsync<AQGreenPlacementUnsupportedAttributionException>(() =>
                    ApproveAsync(fixture.ParticipantId));
            }

            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Theory]
        [InlineData("AbpUsers", "UPDATE")]
        [InlineData("EntryParticipationApprovalDecisions", "INSERT")]
        [InlineData("TransactionalEmailOutboxMessages", "INSERT")]
        public async Task EnabledV2Approval_FailureAfterAllocationRollsBackEveryEffect(
            string table,
            string operation)
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            await using (await InstallFailureTriggerAsync(table, operation, fixture))
            using (EnableV2(fixture.ParticipantId))
            {
                await Should.ThrowAsync<Exception>(() => ApproveAsync(fixture.ParticipantId));
            }

            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Fact]
        public async Task EnabledV2Approval_ConcurrentApproveApproveIsOneExactMutation()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            using (EnableV2(fixture.ParticipantId))
            {
                var outcomes = await Task.WhenAll(
                    CaptureAsync(() => ApproveAsync(fixture.ParticipantId)),
                    CaptureAsync(() => ApproveAsync(fixture.ParticipantId)));
                outcomes.ShouldAllBe(outcome => outcome == null);
            }

            await AssertCommittedApprovalCountsAsync(fixture.ParticipantId, 1, 1, 1);
        }

        [Fact]
        public async Task EnabledV2Approval_ApproveRejectRaceCommitsExactlyOneOutcome()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            using (EnableV2(fixture.ParticipantId))
            {
                var outcomes = await Task.WhenAll(
                    CaptureAsync(() => ApproveAsync(fixture.ParticipantId)),
                    CaptureAsync(() => RejectAsync(fixture.ParticipantId)));
                outcomes.Count(outcome => outcome == null).ShouldBe(1);
                outcomes.Count(outcome => outcome != null).ShouldBe(1);
            }

            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(item =>
                    item.Id == fixture.ParticipantId);
                participation.Status.ShouldBeOneOf(
                    EntryParticipationStatus.Active,
                    EntryParticipationStatus.Rejected);
                (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") == fixture.ParticipantId))
                    .ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(item =>
                    item.IdempotencyKey.StartsWith($"Entry:{fixture.ParticipantId}:")))
                    .ShouldBe(1);
                (await context.AQGreenNetworkPlacements.CountAsync(item =>
                    item.ParticipantId == fixture.ParticipantId)).ShouldBe(
                    participation.Status == EntryParticipationStatus.Active ? 1 : 0);
            });
        }

        [Fact]
        public async Task EnabledV2Approval_ConcurrentSameScopeParticipantsReceiveConsecutiveSlots()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var first = await SeedSponsoredAwaitingApprovalAsync();
            var second = await SeedAdditionalTargetAsync(first);
            using (EnableV2(first.ParticipantId))
            using (EnableV2(second.ParticipantId))
            {
                var outcomes = await Task.WhenAll(
                    CaptureAsync(() => ApproveAsync(first.ParticipantId)),
                    CaptureAsync(() => ApproveAsync(second.ParticipantId)));
                outcomes.ShouldAllBe(outcome => outcome == null);
            }

            await UsingDbContextAsync(async context =>
            {
                var placements = await context.AQGreenNetworkPlacements
                    .Where(item => item.ParticipantId == first.ParticipantId ||
                                   item.ParticipantId == second.ParticipantId)
                    .OrderBy(item => item.PlacementSlot)
                    .ToListAsync();
                placements.Select(item => item.PlacementSlot).ShouldBe(new int?[] { 1, 2 });
                placements.Select(item => item.CanonicalPath).ShouldBe(new[] { "1", "2" });
            });
        }

        [Theory]
        [InlineData("assignment")]
        [InlineData("customer")]
        [InlineData("area")]
        public async Task EnabledV2Approval_StabilizesAuthorityRowsUntilCommit(
            string authorityFact)
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            await using var blockerConnection = new NpgsqlConnection(ConnectionString);
            await blockerConnection.OpenAsync();
            await using var blockerTransaction =
                await blockerConnection.BeginTransactionAsync();
            await ExecuteAsync(
                blockerConnection,
                blockerTransaction,
                "SELECT pg_advisory_xact_lock(hashtextextended(@resource, 0))",
                new NpgsqlParameter("resource", $"aqgreen-placement-v2:{fixture.ScopeId:N}"));

            Exception approvalOutcome;
            using (EnableV2(fixture.ParticipantId))
            {
                var approval = CaptureAsync(() => ApproveAsync(fixture.ParticipantId));
                await WaitForAdvisoryWaiterAsync();
                var mutation = await CaptureAuthorityMutationAsync(authorityFact, fixture);
                mutation.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("55P03");
                await blockerTransaction.CommitAsync();
                approvalOutcome = await approval;
            }

            approvalOutcome.ShouldBeNull();
        }

        [Fact]
        public async Task EnabledV2Approval_UsesParticipantAreaAuthorityAcrossSponsorAreaTopology()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(
                sponsorInDifferentArea: true);
            fixture.AreaId.ShouldNotBe(fixture.SponsorAreaId);
            using (EnableV2(fixture.ParticipantId))
            {
                await ApproveAsync(fixture.ParticipantId);
            }

            await AssertCommittedApprovalCountsAsync(fixture.ParticipantId, 1, 1, 1);
        }

        [Fact]
        public async Task EnabledV2Approval_SponsorAreaAdministratorCannotApproveParticipantArea()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var originalAdministrator = AbpSession.UserId.Value;
            var fixture = await SeedSponsoredAwaitingApprovalAsync(
                sponsorInDifferentArea: true);
            var sponsorAreaAdministrator =
                await CreateSponsorAreaOnlyAdministratorAsync(fixture);
            AbpSession.UserId = sponsorAreaAdministrator;
            try
            {
                using (EnableV2(fixture.ParticipantId))
                {
                    await Should.ThrowAsync<AbpAuthorizationException>(() =>
                        ApproveAsync(fixture.ParticipantId));
                }
            }
            finally
            {
                AbpSession.UserId = originalAdministrator;
            }

            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
        }

        [Fact]
        public async Task ConcurrentAQGreenAndOnyxApprovalsForSameUserSerializeRolePromotion()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync();
            var onyxParticipantId = await SeedOnyxAwaitingApprovalAsync(fixture);
            using (EnableV2(fixture.ParticipantId))
            {
                var outcomes = await Task.WhenAll(
                    CaptureAsync(() => ApproveAsync(fixture.ParticipantId)),
                    CaptureAsync(() => ApproveAsync(
                        AdminProgrammeType.Onyx,
                        onyxParticipantId)));
                outcomes.ShouldAllBe(outcome => outcome == null);
            }

            await UsingDbContextAsync(async context =>
            {
                (await context.EntryParticipations.SingleAsync(item =>
                    item.Id == fixture.ParticipantId)).Status.ShouldBe(
                    EntryParticipationStatus.Active);
                (await context.OnyxParticipations.SingleAsync(item =>
                    item.Id == onyxParticipantId)).Status.ShouldBe(
                    OnyxParticipationStatus.Active);
                (await context.Users.SingleAsync(item => item.Id == fixture.UserId)).Role
                    .ShouldBe(AquaUserRole.Member);
                var memberRoleId = await context.Roles
                    .Where(role => role.TenantId == 1 && role.Name == "Member")
                    .Select(role => role.Id)
                    .SingleAsync();
                (await context.UserRoles.CountAsync(item =>
                    item.TenantId == 1 && item.UserId == fixture.UserId &&
                    item.RoleId == memberRoleId)).ShouldBe(1);
            });
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [Fact]
        public async Task PaymentConfirmationAndEnabledV2ApprovalCoordinateOnDecisionLock()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(
                targetPaymentConfirmed: false);
            await using var blockerConnection = new NpgsqlConnection(ConnectionString);
            await blockerConnection.OpenAsync();
            await using var blockerTransaction =
                await blockerConnection.BeginTransactionAsync();
            await ExecuteAsync(
                blockerConnection,
                blockerTransaction,
                "SELECT pg_advisory_xact_lock(hashtextextended(@resource, 0))",
                new NpgsqlParameter(
                    "resource",
                    $"programme-participation-decision:{fixture.ParticipantId:N}"));

            var now = DateTime.UtcNow;
            using (EnableV2(fixture.ParticipantId))
            {
                var payment = CaptureAsync(() =>
                    IocManager.Resolve<ProgrammePaymentConfirmationProcessor>()
                        .ProcessAsync(new ConfirmedProgrammePayment(
                            1,
                            fixture.CustomerId,
                            MemberPaymentPurpose.AQGreenJoining,
                            1200m,
                            "ZAR",
                            "Test",
                            $"b32-race-{Guid.NewGuid():N}",
                            now,
                            now.AddMinutes(1))));
                var approval = CaptureAsync(() => ApproveAsync(fixture.ParticipantId));
                await WaitForAdvisoryWaiterAsync(2);
                await blockerTransaction.CommitAsync();
                (await payment).ShouldBeNull();
                var approvalOutcome = await approval;

                var status = await UsingDbContextAsync(async context =>
                    (await context.EntryParticipations.SingleAsync(item =>
                        item.Id == fixture.ParticipantId)).Status);
                status.ShouldBeOneOf(
                    EntryParticipationStatus.Active,
                    EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
                if (status == EntryParticipationStatus.PaymentConfirmedAwaitingApproval)
                {
                    approvalOutcome.ShouldNotBeNull();
                    await ApproveAsync(fixture.ParticipantId);
                }
            }

            await AssertCommittedApprovalCountsAsync(fixture.ParticipantId, 1, 1, 1);
        }

        private async Task<ApprovalFixture> SeedSponsoredAwaitingApprovalAsync(
            bool includeAttribution = true,
            bool prospectiveRoot = false,
            bool sponsorInDifferentArea = false,
            bool targetPaymentConfirmed = true)
        {
            var fixture = new ApprovalFixture();
            await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var now = DateTime.UtcNow.AddMinutes(-10);
                var area = await (
                        from assignment in context.AreaAdminAssignments
                        join candidate in context.Areas on assignment.AreaId equals candidate.Id
                        where assignment.TenantId == 1 &&
                              assignment.UserId == AbpSession.UserId.Value &&
                              !assignment.RevokedAt.HasValue &&
                              candidate.IsActive
                        select candidate)
                    .FirstAsync();
                var sponsorArea = area;
                if (sponsorInDifferentArea)
                {
                    var suffix = Guid.NewGuid().ToString("N")[..8];
                    sponsorArea = Area.Create(1, $"B32{suffix}", $"B3.2 Sponsor {suffix}");
                    context.Areas.Add(sponsorArea);
                    await context.SaveChangesAsync();
                }
                var guestRole = await context.Roles.SingleAsync(role =>
                    role.TenantId == 1 && role.Name == "Guest");

                var sponsor = await CreateCustomerAsync(
                    context,
                    sponsorArea,
                    AquaUserRole.Member,
                    now,
                    "sponsor");
                var target = await CreateCustomerAsync(
                    context,
                    area,
                    AquaUserRole.Guest,
                    now,
                    "target");
                context.UserRoles.Add(new UserRole(1, target.User.Id, guestRole.Id));

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    $"b32{Guid.NewGuid():N}"[..32],
                    now.AddDays(-1),
                    1200m,
                    600m,
                    7);
                var sponsorParticipation = EntryParticipation.StartIndependently(
                    1,
                    sponsor.Customer.Id,
                    terms,
                    now);
                var sponsorPayment = CreateConfirmedJoiningPayment(
                    sponsor.Customer.Id,
                    now.AddMinutes(1),
                    "sponsor");
                sponsorParticipation.ApplyConfirmedJoiningPayment(sponsorPayment);
                sponsorParticipation.ApproveByAdministrator(
                    AbpSession.UserId.Value,
                    now.AddMinutes(2));

                var participant = EntryParticipation.StartUnderRecruiter(
                    1,
                    target.Customer.Id,
                    sponsorParticipation,
                    terms,
                    now.AddMinutes(3));
                MemberPayment payment = null;
                if (targetPaymentConfirmed)
                {
                    payment = CreateConfirmedJoiningPayment(
                        target.Customer.Id,
                        now.AddMinutes(4),
                        "target");
                    participant.ApplyConfirmedJoiningPayment(payment);
                }

                context.MemberPayments.Add(sponsorPayment);
                if (payment != null) context.MemberPayments.Add(payment);
                context.EntryParticipations.AddRange(sponsorParticipation, participant);
                await context.SaveChangesAsync();

                var scope = AQGreenPlacementTreeScope.Create(1);
                var root = AQGreenNetworkPlacement.CreateRoot(
                    scope,
                    sponsorParticipation.Id,
                    now.AddMinutes(2),
                    AQGreenPlacementRules.CurrentVersion);
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(root);

                if (includeAttribution)
                {
                    ProgrammeInvitation invitation = null;
                    var sourceReferenceId = Guid.NewGuid();
                    if (!prospectiveRoot)
                    {
                        invitation = ProgrammeInvitation.Create(
                            1,
                            "AQGREEN",
                            sponsorParticipation.Id);
                        sourceReferenceId = invitation.Id;
                        context.ProgrammeInvitations.Add(invitation);
                        await context.SaveChangesAsync();
                    }
                    var attribution = AQGreenRecruitmentAttribution.Create(
                        1,
                        participant.Id,
                        prospectiveRoot ? null : sponsorParticipation.Id,
                        prospectiveRoot
                            ? AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot
                            : AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                        prospectiveRoot
                            ? AQGreenAcquisitionSource.AuthorisedDirectAdmission
                            : AQGreenAcquisitionSource.MemberInvitation,
                        sourceReferenceId,
                        now.AddMinutes(5),
                        prospectiveRoot ? AbpSession.UserId : null,
                        prospectiveRoot ? "B3.2 root bootstrap remains unsupported" : null,
                        AQGreenRecruitmentAttributionRules.CurrentVersion);
                    context.AQGreenRecruitmentAttributions.Add(attribution);
                    await context.SaveChangesAsync();
                    var confirmation = AQGreenRecruitmentAttributionConfirmation.Confirm(
                        attribution,
                        now.AddMinutes(6),
                        prospectiveRoot ? AbpSession.UserId : target.User.Id,
                        prospectiveRoot
                            ? AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation
                            : AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                        sourceReferenceId,
                        AQGreenRecruitmentAttributionRules.CurrentVersion);
                    context.AQGreenRecruitmentAttributionConfirmations.Add(confirmation);
                }

                await context.SaveChangesAsync();
                fixture.ParticipantId = participant.Id;
                fixture.SponsorParticipantId = sponsorParticipation.Id;
                fixture.ScopeId = scope.Id;
                fixture.UserId = target.User.Id;
                fixture.CustomerId = target.Customer.Id;
                fixture.AreaId = area.Id;
                fixture.SponsorAreaId = sponsorArea.Id;
                fixture.AdministratorUserId = AbpSession.UserId.Value;
            });
            return fixture;
        }

        private async Task<ApprovalFixture> SeedAdditionalTargetAsync(ApprovalFixture sponsorFixture)
        {
            var fixture = new ApprovalFixture
            {
                SponsorParticipantId = sponsorFixture.SponsorParticipantId,
                ScopeId = sponsorFixture.ScopeId,
                AreaId = sponsorFixture.AreaId,
                SponsorAreaId = sponsorFixture.SponsorAreaId,
                AdministratorUserId = sponsorFixture.AdministratorUserId
            };
            await UsingDbContextAsync(async context =>
            {
                var now = DateTime.UtcNow.AddMinutes(-5);
                var area = await context.Areas.SingleAsync(item => item.Id == sponsorFixture.AreaId);
                var sponsor = await context.EntryParticipations.SingleAsync(item =>
                    item.Id == sponsorFixture.SponsorParticipantId);
                var guestRole = await context.Roles.SingleAsync(role =>
                    role.TenantId == 1 && role.Name == "Guest");
                var target = await CreateCustomerAsync(
                    context,
                    area,
                    AquaUserRole.Guest,
                    now,
                    "second-target");
                context.UserRoles.Add(new UserRole(1, target.User.Id, guestRole.Id));
                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    $"b32{Guid.NewGuid():N}"[..32],
                    now.AddDays(-1),
                    1200m,
                    600m,
                    7);
                var participant = EntryParticipation.StartUnderRecruiter(
                    1,
                    target.Customer.Id,
                    sponsor,
                    terms,
                    now);
                var payment = CreateConfirmedJoiningPayment(
                    target.Customer.Id,
                    now.AddMinutes(1),
                    "second-target");
                participant.ApplyConfirmedJoiningPayment(payment);
                context.MemberPayments.Add(payment);
                context.EntryParticipations.Add(participant);
                await context.SaveChangesAsync();

                var sourceReferenceId = await context.AQGreenRecruitmentAttributions
                    .Where(item => item.ParticipantId == sponsorFixture.ParticipantId)
                    .Select(item => item.SourceReferenceId)
                    .SingleAsync();
                var attribution = AQGreenRecruitmentAttribution.Create(
                    1,
                    participant.Id,
                    sponsor.Id,
                    AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                    AQGreenAcquisitionSource.MemberInvitation,
                    sourceReferenceId,
                    now.AddMinutes(2),
                    null,
                    null,
                    AQGreenRecruitmentAttributionRules.CurrentVersion);
                context.AQGreenRecruitmentAttributions.Add(attribution);
                await context.SaveChangesAsync();
                context.AQGreenRecruitmentAttributionConfirmations.Add(
                    AQGreenRecruitmentAttributionConfirmation.Confirm(
                        attribution,
                        now.AddMinutes(3),
                        target.User.Id,
                        AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance,
                        sourceReferenceId,
                        AQGreenRecruitmentAttributionRules.CurrentVersion));
                await context.SaveChangesAsync();

                fixture.ParticipantId = participant.Id;
                fixture.UserId = target.User.Id;
                fixture.CustomerId = target.Customer.Id;
            });
            return fixture;
        }

        private async Task<Guid> SeedOnyxAwaitingApprovalAsync(ApprovalFixture fixture)
        {
            return await UsingDbContextAsync(async context =>
            {
                var now = DateTime.UtcNow.AddMinutes(-4);
                var suffix = Guid.NewGuid().ToString("N");
                var membership = Membership.Create(
                    1,
                    $"B3.2 Onyx {suffix}",
                    "Concurrent role-promotion test",
                    MembershipType.Onyx);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();
                var onyx = OnyxParticipation.StartDirectIndependently(
                    1,
                    fixture.CustomerId,
                    membership.Id,
                    OnyxPlanTerms.Create($"b32{suffix}"[..32], now.AddDays(-1), 6120m),
                    now);
                var payment = MemberPayment.CreatePending(
                    1,
                    fixture.CustomerId,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    "Test",
                    $"b32-onyx-{suffix}",
                    now.AddMinutes(1),
                    "ZAR");
                payment.Confirm(now.AddMinutes(2));
                onyx.ApplyConfirmedDirectEntryPayment(payment);
                context.MemberPayments.Add(payment);
                context.OnyxParticipations.Add(onyx);
                await context.SaveChangesAsync();
                return onyx.Id;
            });
        }

        private async Task<long> CreateSponsorAreaOnlyAdministratorAsync(
            ApprovalFixture fixture)
        {
            return await UsingDbContextAsync(async context =>
            {
                var now = DateTime.UtcNow;
                var suffix = Guid.NewGuid().ToString("N");
                var original = await context.Users.SingleAsync(item =>
                    item.Id == fixture.AdministratorUserId && item.TenantId == 1);
                var user = new User
                {
                    TenantId = 1,
                    UserName = $"b32-sponsor-admin-{suffix}",
                    EmailAddress = $"b32-sponsor-admin-{suffix}@example.test",
                    Name = "Sponsor",
                    Surname = "Area Admin",
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                user.SetRole(original.Role);
                user.SetNormalizedNames();
                user.Password = new PasswordHasher<User>(
                        new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
                    .HashPassword(user, User.DefaultPassword);
                context.Users.Add(user);
                await context.SaveChangesAsync();
                var roleIds = await context.UserRoles
                    .Where(item => item.TenantId == 1 &&
                                   item.UserId == fixture.AdministratorUserId)
                    .Select(item => item.RoleId)
                    .ToListAsync();
                context.UserRoles.AddRange(roleIds.Select(roleId =>
                    new UserRole(1, user.Id, roleId)));
                var sponsorArea = await context.Areas.SingleAsync(item =>
                    item.Id == fixture.SponsorAreaId);
                context.AreaAdminAssignments.Add(AreaAdminAssignment.Assign(
                    sponsorArea,
                    user.Id,
                    1,
                    now));
                await context.SaveChangesAsync();
                return user.Id;
            });
        }

        private static async Task<(User User, Customer Customer)> CreateCustomerAsync(
            AqualLifeStyle.EntityFrameworkCore.AqualLifeStyleDbContext context,
            Area area,
            AquaUserRole role,
            DateTime now,
            string label)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var user = new User
            {
                TenantId = 1,
                UserName = $"b32-{label}-{suffix}",
                EmailAddress = $"b32-{label}-{suffix}@example.test",
                Name = "AQGreen",
                Surname = "B32",
                IsEmailConfirmed = true,
                IsActive = true
            };
            user.SetRole(role);
            user.SetNormalizedNames();
            user.Password = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
                .HashPassword(user, User.DefaultPassword);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var customer = Customer.Create(
                1,
                user.Id,
                $"AQGreen {label} {suffix}",
                new EmailAddress(user.EmailAddress),
                user: user);
            customer.AssignInitialArea(area, now, "B3.2 PostgreSQL application test");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            return (user, customer);
        }

        private static MemberPayment CreateConfirmedJoiningPayment(
            int customerId,
            DateTime initiatedAt,
            string label)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "Test",
                $"b32-{label}-{Guid.NewGuid():N}",
                initiatedAt,
                "ZAR");
            payment.Confirm(initiatedAt.AddMinutes(1));
            return payment;
        }

        private IDisposable EnableV2(Guid participantId) =>
            ((AQGreenPlacementV2TestApprovalGate)IocManager
                .Resolve<IAQGreenPlacementV2ApprovalGate>())
            .Enable(participantId);

        private Task ApproveAsync(Guid participantId) =>
            ApproveAsync(AdminProgrammeType.Entry, participantId);

        private Task ApproveAsync(AdminProgrammeType programme, Guid participantId) =>
            IocManager.Resolve<AdminProgrammeParticipationAppService>()
                .ApproveProgrammeParticipationAsync(new ApproveProgrammeParticipationInput
                {
                    Programme = programme,
                    ParticipationId = participantId
                });

        private Task RejectAsync(Guid participantId) =>
            IocManager.Resolve<AdminProgrammeParticipationAppService>()
                .RejectProgrammeParticipationAsync(new RejectProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ParticipationId = participantId,
                    Reason = "B3.2 deterministic approval/rejection race"
                });

        private static async Task<Exception> CaptureAsync(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private async Task AssertCommittedApprovalCountsAsync(
            Guid participantId,
            int placements,
            int decisions,
            int outboxMessages)
        {
            await UsingDbContextAsync(async context =>
            {
                (await context.AQGreenNetworkPlacements.CountAsync(item =>
                    item.ParticipantId == participantId)).ShouldBe(placements);
                (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") == participantId))
                    .ShouldBe(decisions);
                (await context.TransactionalEmailOutboxMessages.CountAsync(item =>
                    item.IdempotencyKey == $"Entry:{participantId}:approved"))
                    .ShouldBe(outboxMessages);
            });
        }

        private async Task<FailureTrigger> InstallFailureTriggerAsync(
            string table,
            string operation,
            ApprovalFixture fixture)
        {
            var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AbpUsers"] = "UPDATE",
                ["EntryParticipationApprovalDecisions"] = "INSERT",
                ["TransactionalEmailOutboxMessages"] = "INSERT"
            };
            if (!allowed.TryGetValue(table, out var expectedOperation) ||
                !string.Equals(operation, expectedOperation, StringComparison.Ordinal))
                throw new ArgumentException("Unsupported B3.2 failure trigger.");

            var suffix = Guid.NewGuid().ToString("N");
            var function = $"b32_fail_fn_{suffix}";
            var trigger = $"b32_fail_trg_{suffix}";
            var condition = table switch
            {
                "AbpUsers" => $"NEW.\"Id\" = {fixture.UserId}",
                "EntryParticipationApprovalDecisions" =>
                    $"NEW.\"EntryParticipationId\" = '{fixture.ParticipantId}'::uuid",
                "TransactionalEmailOutboxMessages" =>
                    $"NEW.\"IdempotencyKey\" = 'Entry:{fixture.ParticipantId}:approved'",
                _ => throw new ArgumentOutOfRangeException(nameof(table))
            };
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE FUNCTION public."{function}"() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'B3.2 injected transactional failure';
                END
                $$;
                CREATE TRIGGER "{trigger}"
                BEFORE {operation} ON public."{table}"
                FOR EACH ROW WHEN ({condition})
                EXECUTE FUNCTION public."{function}"();
                """;
            await command.ExecuteNonQueryAsync();
            return new FailureTrigger(ConnectionString, table, trigger, function);
        }

        private async Task<FailureTrigger> InstallRoleSleepTriggerAsync(
            ApprovalFixture fixture)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var function = $"b32_sleep_fn_{suffix}";
            var trigger = $"b32_sleep_trg_{suffix}";
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE FUNCTION public."{function}"() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_sleep(30);
                    RETURN NEW;
                END
                $$;
                CREATE TRIGGER "{trigger}"
                BEFORE UPDATE ON public."AbpUsers"
                FOR EACH ROW WHEN (NEW."Id" = {fixture.UserId})
                EXECUTE FUNCTION public."{function}"();
                """;
            await command.ExecuteNonQueryAsync();
            return new FailureTrigger(
                ConnectionString,
                "AbpUsers",
                trigger,
                function);
        }

        private static async Task<int> WaitForSleepingRoleUpdateAsync()
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT pid
                    FROM pg_catalog.pg_stat_activity
                    WHERE datname = current_database()
                      AND wait_event_type = 'Timeout'
                      AND wait_event = 'PgSleep'
                      AND query LIKE '%UPDATE "AbpUsers"%'
                    LIMIT 1;
                    """;
                var result = await command.ExecuteScalarAsync();
                if (result != null) return Convert.ToInt32(result);
                await Task.Delay(50);
            }
            throw new TimeoutException(
                "The approval did not reach the sleeping role update.");
        }

        private static async Task CancelBackendAsync(int backendPid)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_cancel_backend(@pid)";
            command.Parameters.AddWithValue("pid", backendPid);
            ((bool)await command.ExecuteScalarAsync()).ShouldBeTrue();
        }

        private static Exception FindInnermostException(Exception exception)
        {
            while (exception.InnerException != null)
                exception = exception.InnerException;
            return exception;
        }

        private async Task WaitForAdvisoryWaiterAsync(int expectedCount = 1)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM pg_catalog.pg_stat_activity
                    WHERE datname = current_database()
                      AND wait_event_type = 'Lock'
                      AND wait_event = 'advisory';
                    """;
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) >= expectedCount) return;
                await Task.Delay(50);
            }
            throw new TimeoutException("The approval did not reach the placement-scope lock.");
        }

        private async Task<Exception> CaptureAuthorityMutationAsync(
            string authorityFact,
            ApprovalFixture fixture)
        {
            var sql = authorityFact switch
            {
                "assignment" =>
                    "UPDATE public.\"AreaAdminAssignments\" SET \"RevokedAt\" = NOW() " +
                    "WHERE \"TenantId\" = 1 AND \"AreaId\" = @areaId AND \"UserId\" = @userId " +
                    "AND \"RevokedAt\" IS NULL",
                "customer" =>
                    "UPDATE public.\"Customers\" SET \"AreaId\" = NULL WHERE \"Id\" = @customerId",
                "area" =>
                    "UPDATE public.\"Areas\" SET \"IsActive\" = FALSE WHERE \"Id\" = @areaId",
                _ => throw new ArgumentOutOfRangeException(nameof(authorityFact))
            };

            return await CaptureAsync(async () =>
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await ExecuteAsync(connection, transaction, "SET LOCAL lock_timeout = '300ms'");
                await ExecuteAsync(
                    connection,
                    transaction,
                    sql,
                    new NpgsqlParameter("areaId", fixture.AreaId),
                    new NpgsqlParameter("userId", fixture.AdministratorUserId),
                    new NpgsqlParameter("customerId", fixture.CustomerId));
                await transaction.CommitAsync();
            });
        }

        private static async Task ExecuteAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            string sql,
            params NpgsqlParameter[] parameters)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            if (parameters != null && parameters.Length > 0)
                command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        private async Task AssertNoApprovalSideEffectsAsync(Guid participantId, long userId)
        {
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == participantId);
                participation.Status.ShouldBe(
                    EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
                (await context.AQGreenNetworkPlacements.CountAsync(item =>
                    item.ParticipantId == participantId)).ShouldBe(0);
                (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") == participantId))
                    .ShouldBe(0);
                (await context.TransactionalEmailOutboxMessages.CountAsync(item =>
                    item.IdempotencyKey == $"Entry:{participantId}:approved")).ShouldBe(0);
                (await context.Users.SingleAsync(item => item.Id == userId)).Role
                    .ShouldBe(AquaUserRole.Guest);
                var roleNames = await (
                        from userRole in context.UserRoles
                        join role in context.Roles on userRole.RoleId equals role.Id
                        where userRole.UserId == userId && userRole.TenantId == 1
                        select role.Name)
                    .ToListAsync();
                roleNames.ShouldBe(new[] { "Guest" });
            });
        }

        private async Task ExecuteApprovalUserLockLifecycleAsync(
            long userId,
            Func<Task> protectedWork)
        {
            var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
            var approvalLock = IocManager.Resolve<IHostedPaymentCheckoutLock>();
            using (var outer = unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = false
            }))
            {
                await approvalLock.AcquireProgrammeApprovalUserSessionAsync(userId);
                try
                {
                    await protectedWork();
                }
                finally
                {
                    await approvalLock.ReleaseProgrammeApprovalUserSessionAsync(userId);
                }
                await outer.CompleteAsync();
            }
        }

        private static async Task<int> ReadBackendPidAsync(NpgsqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_backend_pid()";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private static async Task AssertApprovalUserLockAvailableAsync(long userId)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT pg_try_advisory_lock(hashtextextended(@resource, 0))";
            command.Parameters.AddWithValue(
                "resource",
                $"programme-approval-user:{userId}");
            var acquired = (bool)await command.ExecuteScalarAsync();
            acquired.ShouldBeTrue();
            command.CommandText =
                "SELECT pg_advisory_unlock(hashtextextended(@resource, 0))";
            ((bool)await command.ExecuteScalarAsync()).ShouldBeTrue();
        }

        private static long NewLockTestUserId() =>
            Math.Abs((long)Guid.NewGuid().GetHashCode()) + 1L;

        private static bool IsPostgreSqlRegressionMode() =>
            string.Equals(
                Environment.GetEnvironmentVariable("REPRO_PG"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        private static string ConnectionString =>
            Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION") ??
            throw new InvalidOperationException(
                "REPRO_PG_CONNECTION is required for B3.2 PostgreSQL tests.");

        private sealed class ApprovalFixture
        {
            public Guid ParticipantId { get; set; }
            public Guid SponsorParticipantId { get; set; }
            public Guid ScopeId { get; set; }
            public long UserId { get; set; }
            public int CustomerId { get; set; }
            public Guid AreaId { get; set; }
            public Guid SponsorAreaId { get; set; }
            public long AdministratorUserId { get; set; }
        }

        private sealed class FailureTrigger : IAsyncDisposable
        {
            private readonly string _connectionString;
            private readonly string _table;
            private readonly string _trigger;
            private readonly string _function;

            public FailureTrigger(
                string connectionString,
                string table,
                string trigger,
                string function)
            {
                _connectionString = connectionString;
                _table = table;
                _trigger = trigger;
                _function = function;
            }

            public async ValueTask DisposeAsync()
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    DROP TRIGGER IF EXISTS "{_trigger}" ON public."{_table}";
                    DROP FUNCTION IF EXISTS public."{_function}"();
                    """;
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
