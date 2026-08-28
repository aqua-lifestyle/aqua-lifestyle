using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    [CollectionDefinition("AQGreen B5.1 progress", DisableParallelization = true)]
    public sealed class AQGreenPlacementV2ProgressCollection
    {
    }

    [Collection("AQGreen B5.1 progress")]
    public sealed class AQGreenPlacementV2ProgressTests : AqualLifeStyleWebTestBase
    {
        [Theory]
        [InlineData(AQGreenStructuralCompletionLevel.Level0, 4, 18, 0, 1, 4, 5, 1, 80)]
        [InlineData(AQGreenStructuralCompletionLevel.Level1, 5, 18, 0, 2, 18, 25, 7, 72)]
        [InlineData(AQGreenStructuralCompletionLevel.Level2, 5, 25, 77, 3, 77, 125, 48, 62)]
        public async Task EnabledV2Progress_TargetsTheNextIncompleteStructuralLevel(
            AQGreenStructuralCompletionLevel completedLevel,
            int depth1,
            int depth2,
            int depth3,
            int targetLevel,
            int achieved,
            int required,
            int remaining,
            int percent)
        {
            var fixture = await SeedMemberAsync(activeParticipation: true);
            var gate = ResolveGate();
            var evaluator = ResolveEvaluator();
            using (gate.Enable(fixture.ParticipantId))
            using (evaluator.Return(
                       fixture.ParticipantId,
                       completedLevel,
                       depth1,
                       depth2,
                       depth3))
            {
                LoginAs(fixture.UserId);
                var result = await InUnitOfWorkAsync(() =>
                    Resolve<IClubMemberProgrammeProgressAppService>()
                        .GetMyProgressAsync());

                result.QualifiedLevel.ShouldBe((int)completedLevel);
                result.StructuralProgress.ShouldNotBeNull();
                result.StructuralProgress.CompletedLevel.ShouldBe((int)completedLevel);
                result.StructuralProgress.TargetLevel.ShouldBe(targetLevel);
                result.StructuralProgress.AchievedCount.ShouldBe(achieved);
                result.StructuralProgress.RequiredCount.ShouldBe(required);
                result.StructuralProgress.RemainingCount.ShouldBe(remaining);
                result.StructuralProgress.ProgressPercent.ShouldBe(percent);
                result.StructuralProgress.MeasureLabel
                    .ShouldBe("Qualifying placement occupants");
                result.DirectRecruits.ShouldBe(0);
                gate.GetCheckCount(fixture.ParticipantId).ShouldBe(1);
                evaluator.GetCallCount(fixture.ParticipantId).ShouldBe(1);
            }
        }

        [Fact]
        public async Task EnabledV2Progress_LevelThreeIsCompleteAndDoesNotInventLevelFour()
        {
            var fixture = await SeedMemberAsync(activeParticipation: true);
            var gate = ResolveGate();
            var evaluator = ResolveEvaluator();
            using (gate.Enable(fixture.ParticipantId))
            using (evaluator.Return(
                       fixture.ParticipantId,
                       AQGreenStructuralCompletionLevel.Level3,
                       5,
                       25,
                       125))
            {
                LoginAs(fixture.UserId);
                var result = await InUnitOfWorkAsync(() =>
                    Resolve<IClubMemberProgrammeProgressAppService>()
                        .GetMyProgressAsync());

                result.QualifiedLevel.ShouldBe(3);
                result.NextLevelLabel.ShouldBeNull();
                result.StructuralProgress.TargetLevel.ShouldBeNull();
                result.StructuralProgress.AchievedCount.ShouldBe(125);
                result.StructuralProgress.RequiredCount.ShouldBe(125);
                result.StructuralProgress.RemainingCount.ShouldBe(0);
                result.StructuralProgress.ProgressPercent.ShouldBe(100);
                result.Education.ShouldNotContain(item =>
                    item.Body.Contains("Level 4", StringComparison.OrdinalIgnoreCase));
                gate.GetCheckCount(fixture.ParticipantId).ShouldBe(1);
                evaluator.GetCallCount(fixture.ParticipantId).ShouldBe(1);
            }
        }

        [Fact]
        public async Task EnabledV2Journey_UsesB4CountsAndChecksTheGateOnce()
        {
            var fixture = await SeedMemberAsync(activeParticipation: true);
            var gate = ResolveGate();
            var evaluator = ResolveEvaluator();
            using (gate.Enable(fixture.ParticipantId))
            using (evaluator.Return(
                       fixture.ParticipantId,
                       AQGreenStructuralCompletionLevel.Level0,
                       4,
                       18,
                       0))
            {
                LoginAs(fixture.UserId);
                var journey = await InUnitOfWorkAsync(() =>
                    Resolve<IClubMemberProgrammeProgressAppService>()
                        .GetMyJourneyAsync());
                var aqGreen = journey.Programmes.Single(
                    item => item.ProgrammeCode == "AQGREEN");

                aqGreen.QualifiedLevel.ShouldBe(0);
                aqGreen.MaximumLevel.ShouldBe(3);
                aqGreen.Levels[0].State.ShouldBe("Current");
                aqGreen.Levels[0].AchievedCount.ShouldBe(4);
                aqGreen.Levels[0].RequiredCount.ShouldBe(5);
                aqGreen.Levels[0].ProgressPercent.ShouldBe(80);
                aqGreen.Levels[0].MeasureLabel
                    .ShouldBe("Qualifying placement occupants");
                aqGreen.Levels[1].AchievedCount.ShouldBe(18);
                gate.GetCheckCount(fixture.ParticipantId).ShouldBe(1);
                evaluator.GetCallCount(fixture.ParticipantId).ShouldBe(1);
            }
        }

        [Theory]
        [InlineData("missing", "authoritative V2 placement")]
        [InlineData("d08", "AQG-V2-D08")]
        [InlineData("topology", "injected topology corruption")]
        public async Task EnabledV2Progress_FailsClosedWithoutV1Fallback(
            string failure,
            string expectedDetail)
        {
            var fixture = await SeedMemberAsync(activeParticipation: true);
            var gate = ResolveGate();
            var evaluator = ResolveEvaluator();
            using (gate.Enable(fixture.ParticipantId))
            using (evaluator.Fail(fixture.ParticipantId, (participantId, cutoff) =>
                       failure switch
                       {
                           "missing" => new AQGreenStructuralEvaluationNotPlacedException(
                               participantId,
                               cutoff),
                           "d08" => new AQGreenStructuralContributionPolicyRequiredException(
                               participantId),
                           _ => new AQGreenPlacementTopologyIntegrityException(
                               "injected topology corruption")
                       }))
            {
                LoginAs(fixture.UserId);
                var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                    InUnitOfWorkAsync(() =>
                        Resolve<IClubMemberProgrammeProgressAppService>()
                            .GetMyProgressAsync()));

                exception.Details.ShouldContain(expectedDetail);
                gate.GetCheckCount(fixture.ParticipantId).ShouldBe(1);
                evaluator.GetCallCount(fixture.ParticipantId).ShouldBe(1);
            }
        }

        [Fact]
        public async Task EnabledV2Progress_NonActiveParticipationIsNotMaskedAsLevelZero()
        {
            var fixture = await SeedMemberAsync(activeParticipation: false);
            var gate = ResolveGate();
            var evaluator = ResolveEvaluator();
            using (gate.Enable(fixture.ParticipantId))
            using (evaluator.Fail(fixture.ParticipantId, (participantId, cutoff) =>
                       new AQGreenStructuralEvaluationNotPlacedException(
                           participantId,
                           cutoff)))
            {
                LoginAs(fixture.UserId);
                await Should.ThrowAsync<UserFriendlyException>(() =>
                    InUnitOfWorkAsync(() =>
                        Resolve<IClubMemberProgrammeProgressAppService>()
                            .GetMyProgressAsync()));
                evaluator.GetCallCount(fixture.ParticipantId).ShouldBe(1);
            }
        }

        [Fact]
        public async Task DisabledGate_PreservesV1ShapeAndDoesNotCallB4()
        {
            var fixture = await SeedMemberAsync(activeParticipation: true);
            LoginAs(fixture.UserId);

            var result = await InUnitOfWorkAsync(() =>
                Resolve<IClubMemberProgrammeProgressAppService>()
                    .GetMyProgressAsync());

            result.StructuralProgress.ShouldBeNull();
            result.QualifiedLevel.ShouldBe(0);
            ResolveGate().GetCheckCount(fixture.ParticipantId).ShouldBe(1);
            ResolveEvaluator().GetCallCount(fixture.ParticipantId).ShouldBe(0);
        }

        [Fact]
        public async Task ProgressRequiresViewSelfAuthorization()
        {
            AbpSession.TenantId = 1;
            AbpSession.UserId = null;

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                InUnitOfWorkAsync(() =>
                    Resolve<IClubMemberProgrammeProgressAppService>()
                        .GetMyProgressAsync()));
        }

        [Fact]
        public void V2StructuralProgressContractContainsAggregatesOnly()
        {
            var propertyNames = typeof(AQGreenStructuralProgressDto)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            propertyNames.ShouldBe(new[]
            {
                "CompletedLevel",
                "TargetLevel",
                "AchievedCount",
                "RequiredCount",
                "RemainingCount",
                "ProgressPercent",
                "MeasureLabel",
                "Cutoff",
                "RulesVersion"
            }, ignoreOrder: true);
        }

        [Fact]
        public async Task EnabledV2Progress_RealPostgreSqlB4MapsSpilloverOccupancySeparatelyFromRecruitment()
        {
            if (!IsPostgreSqlRegressionMode()) return;

            var fixture = await SeedMemberAsync(activeParticipation: true);
            await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var now = DateTime.UtcNow.AddMinutes(-5);
                var rootParticipation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == fixture.ParticipantId);
                var scope = AQGreenPlacementTreeScope.Create(1);
                var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                    scope,
                    rootParticipation.Id,
                    now,
                    AQGreenPlacementRules.CurrentVersion);
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(rootPlacement);

                for (var slot = 1; slot <= 5; slot++)
                {
                    var child = await CreateActiveChildAsync(
                        context,
                        rootParticipation,
                        now,
                        slot);
                    context.AQGreenNetworkPlacements.Add(
                        AQGreenNetworkPlacement.CreateChild(
                            rootPlacement,
                            child.Id,
                            slot,
                            now.AddMinutes(3),
                            AQGreenPlacementRules.CurrentVersion));
                }
                await context.SaveChangesAsync();
            });

            var gate = ResolveGate();
            using (gate.Enable(fixture.ParticipantId))
            {
                LoginAs(fixture.UserId);
                var result = await InUnitOfWorkAsync(() =>
                    Resolve<IClubMemberProgrammeProgressAppService>()
                        .GetMyProgressAsync());

                result.QualifiedLevel.ShouldBe(1);
                result.StructuralProgress.CompletedLevel.ShouldBe(1);
                result.StructuralProgress.TargetLevel.ShouldBe(2);
                result.StructuralProgress.AchievedCount.ShouldBe(0);
                result.StructuralProgress.RequiredCount.ShouldBe(25);
                result.DirectRecruits.ShouldBe(0);
                gate.GetCheckCount(fixture.ParticipantId).ShouldBe(1);
                ResolveEvaluator().GetCallCount(fixture.ParticipantId).ShouldBe(1);
            }

            WritePostgreSqlMarker();
        }

        private async Task<MemberFixture> SeedMemberAsync(bool activeParticipation)
        {
            LoginAsDefaultTenantAdmin();
            return await UsingDbContextAsync(async context =>
            {
                var now = DateTime.UtcNow.AddMinutes(-10);
                var suffix = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    TenantId = 1,
                    UserName = $"b51-member-{suffix}",
                    EmailAddress = $"b51-member-{suffix}@example.test",
                    Name = "AQGreen",
                    Surname = "B51",
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                user.SetRole(AquaUserRole.Member);
                user.SetNormalizedNames();
                user.Password = new PasswordHasher<User>(
                        new OptionsWrapper<PasswordHasherOptions>(
                            new PasswordHasherOptions()))
                    .HashPassword(user, User.DefaultPassword);
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var memberRole = await context.Roles.SingleAsync(role =>
                    role.TenantId == 1 && role.Name == "Member");
                context.UserRoles.Add(new UserRole(1, user.Id, memberRole.Id));
                var customer = Customer.Create(
                    1,
                    user.Id,
                    $"AQGreen B5.1 {suffix}",
                    new EmailAddress(user.EmailAddress),
                    user: user);
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                    $"b51{suffix}"[..32],
                    now.AddDays(-1),
                    1200m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    now);
                if (activeParticipation)
                {
                    var payment = MemberPayment.CreatePending(
                        1,
                        customer.Id,
                        MemberPaymentPurpose.AQGreenJoining,
                        1200m,
                        "Test",
                        $"b51-{suffix}",
                        now.AddMinutes(1),
                        "ZAR");
                    payment.Confirm(now.AddMinutes(2));
                    participation.ApplyConfirmedJoiningPayment(payment);
                    participation.ApproveByAdministrator(
                        AbpSession.UserId.Value,
                        now.AddMinutes(3));
                    context.MemberPayments.Add(payment);
                }
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return new MemberFixture(user.Id, participation.Id);
            });
        }

        private static async Task<EntryParticipation> CreateActiveChildAsync(
            AqualLifeStyle.EntityFrameworkCore.AqualLifeStyleDbContext context,
            EntryParticipation rootParticipation,
            DateTime now,
            int slot)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var user = new User
            {
                TenantId = 1,
                UserName = $"b51-spillover-{suffix}",
                EmailAddress = $"b51-spillover-{suffix}@example.test",
                Name = "AQGreen",
                Surname = "Spillover",
                IsEmailConfirmed = true,
                IsActive = true
            };
            user.SetRole(AquaUserRole.Member);
            user.SetNormalizedNames();
            user.Password = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(
                        new PasswordHasherOptions()))
                .HashPassword(user, User.DefaultPassword);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var customer = Customer.Create(
                1,
                user.Id,
                $"AQGreen spillover {suffix}",
                new EmailAddress(user.EmailAddress),
                user: user);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                $"b51{suffix}"[..32],
                now.AddDays(-1),
                1200m,
                600m,
                7);
            // Recruiting evidence intentionally points nowhere: placement alone
            // makes these five occupants structurally visible to B4.
            var participation = EntryParticipation.StartIndependently(
                1,
                customer.Id,
                terms,
                now);
            var payment = MemberPayment.CreatePending(
                1,
                customer.Id,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "Test",
                $"b51-spillover-{slot}-{suffix}",
                now.AddMinutes(1),
                "ZAR");
            payment.Confirm(now.AddMinutes(2));
            participation.ApplyConfirmedJoiningPayment(payment);
            participation.ApproveByAdministrator(1L, now.AddMinutes(3));
            context.EntryParticipations.Add(participation);
            context.MemberPayments.Add(payment);
            return participation;
        }

        private static bool IsPostgreSqlRegressionMode() =>
            string.Equals(
                Environment.GetEnvironmentVariable("REPRO_PG"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        private static void WritePostgreSqlMarker()
        {
            var markerDirectory = Environment.GetEnvironmentVariable("REPRO_MARKER_DIR");
            if (string.IsNullOrWhiteSpace(markerDirectory))
            {
                throw new InvalidOperationException(
                    "REPRO_MARKER_DIR is required for the B5.1 PostgreSQL regression.");
            }
            Directory.CreateDirectory(markerDirectory);
            File.WriteAllText(
                Path.Combine(markerDirectory, "aqgreen-v2-progress-pg.ran"),
                "B5.1 PostgreSQL body executed");
        }

        private void LoginAs(long userId)
        {
            AbpSession.TenantId = 1;
            AbpSession.UserId = userId;
        }

        private async Task<T> InUnitOfWorkAsync<T>(Func<Task<T>> action)
        {
            using var unitOfWork = Resolve<IUnitOfWorkManager>().Begin();
            var result = await action();
            await unitOfWork.CompleteAsync();
            return result;
        }

        private AQGreenPlacementV2TestProgressGate ResolveGate() =>
            (AQGreenPlacementV2TestProgressGate)Resolve<IAQGreenPlacementV2ProgressGate>();

        private AQGreenPlacementV2TestStructuralEvaluator ResolveEvaluator() =>
            (AQGreenPlacementV2TestStructuralEvaluator)
                Resolve<IAQGreenStructuralCompletionEvaluator>();

        private sealed record MemberFixture(long UserId, Guid ParticipantId);
    }
}
