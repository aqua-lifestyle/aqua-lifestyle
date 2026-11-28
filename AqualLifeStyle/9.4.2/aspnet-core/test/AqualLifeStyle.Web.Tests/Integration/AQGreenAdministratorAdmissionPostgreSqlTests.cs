using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Payments.Yoco;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed partial class AQGreenV2ContinuousFreshNetworkE2ETests
    {
        [IsolatedPostgreSqlFact]
        public async Task AdministratorAdmission_HistoricalHoldAndExactPlacement_ThenCompanyPaymentApprovalAndSpillover()
        {
            await AssertFreshPostgreSqlDatabaseAsync();
            Resolve<IConfiguration>()["App:FinancialOperations:Enabled"] = "true";
            IocManager.IocContainer.Register(Component.For<IYocoCheckoutGateway>()
                .Instance(new LeanMvpCheckoutGateway()).IsDefault());
            var joiningGate = (AQGreenV2TestJoiningGate)Resolve<IAQGreenPlacementV2JoiningGate>();
            using var joining = joiningGate.Enable(TenantId);
            LoginAsDefaultTenantAdmin();
            var root = await SeedAwaitingPaymentParticipantAsync(
                EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-admin-root", DateTime.UtcNow.AddDays(-1), 1200m, 600m, 7),
                null, "admin-root", authorisedProspectiveRoot: true);
            await ConfirmJoiningPaymentAsync(root);
            var tenantAdminId = AbpSession.GetUserId();
            LoginAsHostAdmin();
            using (ApprovalGate.Enable(root.ParticipationId)) await ApproveAsync(root.ParticipationId);
            var scope = await UsingDbContextAsync(c => c.AQGreenNetworkPlacements.SingleAsync(p => p.ParticipantId == root.ParticipationId));

            // The operator could transfer only this already active, verified paid record.
            LoginAsDefaultTenantAdmin();
            var historical = await SeedAwaitingPaymentParticipantAsync(
                EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-historical", DateTime.UtcNow.AddDays(-1), 1200m, 600m, 7),
                root, "historical", authorisedProspectiveRoot: false);
            await ConfirmJoiningPaymentAsync(historical);
            var dates = await UsingDbContextAsync(async c =>
            {
                var p = await c.EntryParticipations.SingleAsync(e => e.Id == historical.ParticipationId);
                p.ApproveByAdministrator(tenantAdminId, DateTime.UtcNow);
                await c.SaveChangesAsync();
                return new AQGreenHistoricalPlacementInput { CustomerId = p.CustomerId, ParticipantId = p.Id,
                    OriginalReference = "00042", ParentParticipantId = root.ParticipationId, Slot = 3,
                    StartedAtUtc = p.StartedAt, ActivatedAtUtc = p.ActivatedAt.Value, PlacedAtUtc = p.ActivatedAt.Value };
            });
            dates.ActivatedAtUtc = dates.ActivatedAtUtc.AddTicks(-(dates.ActivatedAtUtc.Ticks % 10));
            dates.PlacedAtUtc = dates.ActivatedAtUtc;
            var open = new OpenAQGreenHistoricalReviewInput { TenantId = TenantId, ScopeId = scope.PlacementTreeScopeId,
                EvidenceReference = "synthetic-reviewed-register-v1", ExpectedMembers = 1,
                OriginalReferences = new[] { "00042" }, AllowPrecautionaryCancellation = true };
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<IAdminProgrammeParticipationAppService>().OpenAQGreenHistoricalReviewAsync(open));
            LoginAsHostAdmin();
            await UsingDbContextAsync(async c =>
            {
                var grant = new Abp.Authorization.Users.UserPermissionSetting { UserId = AbpSession.GetUserId(),
                    Name = AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence, IsGranted = true };
                c.Set<Abp.Authorization.Users.UserPermissionSetting>().Add(grant);
                await c.SaveChangesAsync();
            });
            var admin = Resolve<IAdminProgrammeParticipationAppService>();
            var precautionary = new OpenAQGreenHistoricalReviewInput { TenantId = TenantId, ScopeId = open.ScopeId,
                EvidenceReference = "synthetic-preliminary-screen", ExpectedMembers = 1, AllowPrecautionaryCancellation = true };
            var cancelledId = await admin.OpenAQGreenHistoricalReviewAsync(precautionary);
            var cancel = new CancelAQGreenHistoricalReviewInput { TenantId = TenantId, ScopeId = open.ScopeId,
                EvidenceReference = precautionary.EvidenceReference, NoConflictEvidenceReference = "synthetic-no-conflict-check",
                Reason = "Preliminary import withdrawn after checking the source register", NoOutstandingConflictConfirmed = true };
            using (AbpSession.Use(TenantId, tenantAdminId))
                await Should.ThrowAsync<AbpAuthorizationException>(() => admin.CancelAQGreenHistoricalReviewAsync(cancel));
            cancel.NoOutstandingConflictConfirmed = false;
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CancelAQGreenHistoricalReviewAsync(cancel));
            cancel.NoOutstandingConflictConfirmed = true;

            var invited = await RegisterSyntheticInviteAsync(root, "invited-during-hold");
            await PaySyntheticAdministratorJoinAsync(invited, acceptCompany: false);
            using (ApprovalGate.Enable(invited.ParticipationId))
                await Should.ThrowAsync<AQGreenPlacementConflictException>(() => ApproveAsync(invited.ParticipationId));
            await admin.CancelAQGreenHistoricalReviewAsync(cancel);
            await admin.CancelAQGreenHistoricalReviewAsync(cancel);
            await Should.ThrowAsync<UserFriendlyException>(() => admin.OpenAQGreenHistoricalReviewAsync(precautionary));
            await UsingDbContextAsync(async c =>
            {
                var cancelled = await c.AQGreenHistoricalTreeReviews.SingleAsync(r => r.Id == cancelledId);
                cancelled.CancelledAt.ShouldNotBeNull();
                cancelled.CancelledBy.ShouldBe(AbpSession.GetUserId());
                cancelled.CancellationReason.ShouldBe(cancel.Reason);
                cancelled.CancellationEvidenceReference.ShouldBe(cancel.NoConflictEvidenceReference);
                cancelled.ClearedAt.ShouldBeNull();
                (await c.AQGreenNetworkPlacements.Where(p => p.PlacementTreeScopeId == open.ScopeId).Select(p => p.Id).ToArrayAsync())
                    .ShouldBe(new[] { scope.Id });
                (await c.AQGreenHistoricalMemberReferences.AnyAsync(r => r.ReviewId == cancelledId)).ShouldBeFalse();
            });
            using (ApprovalGate.Enable(invited.ParticipationId)) await ApproveAsync(invited.ParticipationId);

            var reviewId = await admin.OpenAQGreenHistoricalReviewAsync(open);
            (await admin.OpenAQGreenHistoricalReviewAsync(open)).ShouldBe(reviewId);

            var complete = new CompleteAQGreenHistoricalReviewInput { TenantId = TenantId, ScopeId = scope.PlacementTreeScopeId,
                EvidenceReference = open.EvidenceReference, Members = new[] { dates } };
            var correctDate = dates.PlacedAtUtc;
            dates.PlacedAtUtc = default;
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CompleteAQGreenHistoricalReviewAsync(complete));
            dates.PlacedAtUtc = correctDate;
            dates.ParentParticipantId = Guid.NewGuid();
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CompleteAQGreenHistoricalReviewAsync(complete));
            cancel.EvidenceReference = open.EvidenceReference;
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CancelAQGreenHistoricalReviewAsync(cancel));
            dates.ParentParticipantId = root.ParticipationId;
            dates.Slot = 1;
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CompleteAQGreenHistoricalReviewAsync(complete));
            dates.Slot = 3;
            dates.OriginalReference = "00043";
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CompleteAQGreenHistoricalReviewAsync(complete));
            dates.OriginalReference = "00042";
            await admin.CompleteAQGreenHistoricalReviewAsync(complete);
            await admin.CompleteAQGreenHistoricalReviewAsync(complete);
            dates.Slot = 2;
            await Should.ThrowAsync<UserFriendlyException>(() => admin.CompleteAQGreenHistoricalReviewAsync(complete));
            await UsingDbContextAsync(async c =>
            {
                var p = await c.AQGreenNetworkPlacements.SingleAsync(p => p.ParticipantId == historical.ParticipationId);
                p.CanonicalPath.ShouldBe("3");
                p.PlacedAt.ShouldBe(dates.ActivatedAtUtc);
                (await c.AQGreenHistoricalMemberReferences.SingleAsync()).OriginalReference.ShouldBe("00042");
                (await c.AQGreenHistoricalTreeReviews.SingleAsync(r => r.Id == reviewId)).ClearedAt.ShouldNotBeNull();
                (await c.MemberPayments.CountAsync(p => p.CustomerId == historical.CustomerId)).ShouldBe(1);
                (await c.EntryParticipations.SingleAsync(e => e.Id == historical.ParticipationId)).RecruiterCustomerId.ShouldBe(root.CustomerId);
            });
            using (ApprovalGate.Enable(invited.ParticipationId)) await ApproveAsync(invited.ParticipationId);

            var company = await SeedLeanMemberIdentityAsync(root.AreaId, "company-registration");
            var registration = new RegisterAQGreenCompanyAdmissionInput { TenantId = TenantId, ScopeId = scope.PlacementTreeScopeId,
                CustomerId = company.CustomerId, AcquisitionEvidence = "synthetic-campaign-registration-01" };
            var differentArea = await UsingDbContextAsync(async c =>
            {
                var area = Area.Create(TenantId, "SYNTHETICOUTSIDE", "Synthetic outside Area");
                c.Areas.Add(area);
                await c.SaveChangesAsync();
                return area.Id;
            });
            var outOfArea = await SeedLeanMemberIdentityAsync(differentArea, "out-of-scope-company");
            using (AbpSession.Use(TenantId, tenantAdminId))
                await Should.ThrowAsync<AbpAuthorizationException>(() => admin.RegisterAQGreenCompanyAdmissionAsync(
                    new RegisterAQGreenCompanyAdmissionInput { TenantId = TenantId, ScopeId = registration.ScopeId,
                        CustomerId = outOfArea.CustomerId, AcquisitionEvidence = registration.AcquisitionEvidence }));
            company = company with { ParticipationId = await admin.RegisterAQGreenCompanyAdmissionAsync(registration) };
            (await admin.RegisterAQGreenCompanyAdmissionAsync(registration)).ShouldBe(company.ParticipationId);
            registration.AcquisitionEvidence = "different-registration";
            await Should.ThrowAsync<UserFriendlyException>(() => admin.RegisterAQGreenCompanyAdmissionAsync(registration));
            using (ApprovalGate.Enable(company.ParticipationId))
                await Should.ThrowAsync<AQGreenPlacementConflictException>(() => ApproveAsync(company.ParticipationId));
            using (AbpSession.Use(TenantId, company.UserId))
            {
                var memberService = Resolve<IClubMemberProgrammeParticipationAppService>();
                (await memberService.GetMyParticipationsAsync()).Entry.RequiresCompanyAcceptance.ShouldBeTrue();
                await Should.ThrowAsync<UserFriendlyException>(() => memberService.CreateAQGreenJoiningCheckoutAsync(
                    new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full }));
            }
            await PaySyntheticAdministratorJoinAsync(company, acceptCompany: true);
            using (ApprovalGate.Enable(company.ParticipationId))
            {
                await ApproveAsync(company.ParticipationId);
                await ApproveAsync(company.ParticipationId);
            }
            await UsingDbContextAsync(async c =>
            {
                var attribution = await c.AQGreenRecruitmentAttributions.SingleAsync(a => a.ParticipantId == company.ParticipationId);
                attribution.AcquisitionSource.ShouldBe(AQGreenAcquisitionSource.CompanyAcquisition);
                attribution.CreditedSponsorParticipantId.ShouldBeNull();
                (await c.AQGreenRecruitmentAttributionConfirmations.SingleAsync(a => a.AttributionId == attribution.Id)).ConfirmedByUserId.ShouldBe(company.UserId);
                (await c.AQGreenNetworkPlacements.SingleAsync(p => p.ParticipantId == company.ParticipationId)).CanonicalPath.ShouldBe("2");
            });

            for (var index = 0; index < 3; index++)
            {
                var child = await RegisterSyntheticInviteAsync(root, $"post-clearance-{index}");
                await PaySyntheticAdministratorJoinAsync(child, acceptCompany: false);
                using (ApprovalGate.Enable(child.ParticipationId)) await ApproveAsync(child.ParticipationId);
                var placement = await UsingDbContextAsync(c => c.AQGreenNetworkPlacements.SingleAsync(p => p.ParticipantId == child.ParticipationId));
                placement.CanonicalPath.ShouldBe(new[] { "4", "5", "11" }[index]);
            }
            var companyInvite = await RegisterSyntheticInviteAsync(company, "company-member-invites-after-activation");
            await PaySyntheticAdministratorJoinAsync(companyInvite, acceptCompany: false);
            using (ApprovalGate.Enable(companyInvite.ParticipationId)) await ApproveAsync(companyInvite.ParticipationId);
            (await UsingDbContextAsync(c => c.AQGreenNetworkPlacements.SingleAsync(p => p.ParticipantId == companyInvite.ParticipationId)))
                .CanonicalPath.ShouldBe("21");
        }

        [IsolatedPostgreSqlFact]
        public async Task AdministratorAdmission_CancellationRejectsAnyCommittedReferenceOrTopologyChange()
        {
            await AssertFreshPostgreSqlDatabaseAsync();
            Resolve<IConfiguration>()["App:FinancialOperations:Enabled"] = "true";
            LoginAsHostAdmin();
            await UsingDbContextAsync(async c =>
            {
                c.Set<Abp.Authorization.Users.UserPermissionSetting>().Add(new Abp.Authorization.Users.UserPermissionSetting
                {
                    UserId = AbpSession.GetUserId(), Name = AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence,
                    IsGranted = true
                });
                await c.SaveChangesAsync();
            });
            var admin = Resolve<IAdminProgrammeParticipationAppService>();
            foreach (var writeReference in new[] { true, false })
            {
                LoginAsDefaultTenantAdmin();
                var terms = EntryProgrammeTerms.CreateSingleJoiningPayment("synthetic-recovery", DateTime.UtcNow.AddDays(-1), 1200m, 600m, 7);
                var root = await SeedAwaitingPaymentParticipantAsync(terms, null, $"recovery-root-{writeReference}", authorisedProspectiveRoot: true);
                await ConfirmJoiningPaymentAsync(root);
                LoginAsHostAdmin();
                using (ApprovalGate.Enable(root.ParticipationId)) await ApproveAsync(root.ParticipationId);
                LoginAsDefaultTenantAdmin();
                var child = await SeedAwaitingPaymentParticipantAsync(terms, root, $"recovery-child-{writeReference}", authorisedProspectiveRoot: false);
                LoginAsHostAdmin();
                var anchor = await UsingDbContextAsync(c => c.AQGreenNetworkPlacements.SingleAsync(p => p.ParticipantId == root.ParticipationId));
                var evidence = $"synthetic-recovery-{writeReference}";
                var reviewId = await admin.OpenAQGreenHistoricalReviewAsync(new OpenAQGreenHistoricalReviewInput
                {
                    TenantId = TenantId, ScopeId = anchor.PlacementTreeScopeId, ExpectedMembers = 1, EvidenceReference = evidence,
                    AllowPrecautionaryCancellation = true
                });
                // Simulate unexpected durable evidence from outside the normal atomic apply path.
                await UsingDbContextAsync(async c =>
                {
                    if (writeReference)
                    {
                        var review = await c.AQGreenHistoricalTreeReviews.SingleAsync(r => r.Id == reviewId);
                        c.AQGreenHistoricalMemberReferences.Add(new AQGreenHistoricalMemberReference(review, child.ParticipationId, "partial-01"));
                    }
                    else
                        c.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateChild(anchor, child.ParticipationId,
                            4, DateTime.UtcNow, AQGreenPlacementRules.CurrentVersion));
                    await c.SaveChangesAsync();
                });

                var cancel = new CancelAQGreenHistoricalReviewInput { TenantId = TenantId, ScopeId = anchor.PlacementTreeScopeId,
                    EvidenceReference = evidence, NoConflictEvidenceReference = "synthetic-no-conflict-check", Reason = "Withdraw preliminary review",
                    NoOutstandingConflictConfirmed = true };
                await Should.ThrowAsync<UserFriendlyException>(() => admin.CancelAQGreenHistoricalReviewAsync(cancel));
                var denied = await Should.ThrowAsync<PostgresException>(() => UsingDbContextAsync(c => c.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "AQGreenHistoricalTreeReviews" SET "CancelledAt" = clock_timestamp(), "CancelledBy" = {AbpSession.GetUserId()},
                        "CancellationReason" = 'synthetic justification', "CancellationEvidenceReference" = 'synthetic other evidence'
                    WHERE "Id" = {reviewId}
                    """)));
                denied.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
                await UsingDbContextAsync(async c =>
                {
                    var review = await c.AQGreenHistoricalTreeReviews.SingleAsync(r => r.Id == reviewId);
                    review.CancelledAt.ShouldBeNull();
                    review.ClearedAt.ShouldBeNull();
                    (await c.AQGreenHistoricalMemberReferences.AnyAsync(r => r.ReviewId == reviewId)).ShouldBe(writeReference);
                    (await c.AQGreenNetworkPlacements.AnyAsync(p => p.ParticipantId == child.ParticipationId)).ShouldBe(!writeReference);
                });
            }
        }

        private async Task<ParticipantFixture> RegisterSyntheticInviteAsync(ParticipantFixture sponsor, string label)
        {
            string code;
            using (AbpSession.Use(TenantId, sponsor.UserId))
                code = (await InUnitOfWorkAsync(() => Resolve<IProgrammeInvitationAppService>().GetMyInvitationsAsync())).Invitations.Single().Code;
            var customer = await SeedLeanMemberIdentityAsync(sponsor.AreaId, label);
            using (AbpSession.Use(TenantId, customer.UserId))
                await InUnitOfWorkAsync(() => StartLeanAsync(new StartEntryParticipationInput { InviteCode = code }));
            return customer with { ParticipationId = await UsingDbContextAsync(c => c.EntryParticipations
                .Where(p => p.CustomerId == customer.CustomerId).Select(p => p.Id).SingleAsync()) };
        }

        private async Task PaySyntheticAdministratorJoinAsync(ParticipantFixture participant, bool acceptCompany)
        {
            using (AbpSession.Use(TenantId, participant.UserId))
            {
                var member = Resolve<IClubMemberProgrammeParticipationAppService>();
                var request = new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full, AcceptCompanyRegistration = acceptCompany };
                var checkoutDto = await member.CreateAQGreenJoiningCheckoutAsync(request);
                (await member.CreateAQGreenJoiningCheckoutAsync(request)).CheckoutId.ShouldBe(checkoutDto.CheckoutId);
                var checkout = await UsingDbContextAsync(c => c.AQGreenJoiningCheckouts.SingleAsync(p => p.Id == checkoutDto.CheckoutId));
                var processor = Resolve<ProgrammePaymentConfirmationProcessor>();
                var now = DateTime.UtcNow;
                var reference = $"synthetic-admin-{checkout.Id:N}";
                (await processor.ProcessAQGreenJoiningCheckoutAsync(checkout.Id, "Test", reference, checkout.ProviderCheckoutId, 1200m,
                    "ZAR", now)).AwaitingAdministrativeApproval.ShouldBeTrue();
                (await processor.ProcessAQGreenJoiningCheckoutAsync(checkout.Id, "Test", reference, checkout.ProviderCheckoutId, 1200m,
                    "ZAR", now)).WasAlreadyProcessed.ShouldBeTrue();
            }
        }
    }
}
