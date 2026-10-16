using System;
using System.Linq;
using System.Transactions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.Timing;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Members.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Members
{
    [Audited]
    public class AdminMemberAppService : AdminAppServiceBase, IAdminMemberAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<Area, Guid> _areaRepository;
        private readonly IRepository<AreaAdminAssignment, Guid> _areaAdminAssignmentRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IAQGreenApprovalAuthorityStabilizer _areaAuthorityStabilizer;
        private readonly IRepository<Membership> _membershipRepository;
        private readonly IAdminCustomerProfileUpdater _customerProfileUpdater;
        private readonly AccountEmailVerificationScheduler _emailVerificationScheduler;

        public AdminMemberAppService(
            ICustomerRepository customerRepository,
            IRepository<Membership> membershipRepository,
            IAdminCustomerProfileUpdater customerProfileUpdater,
            AccountEmailVerificationScheduler emailVerificationScheduler,
            IRepository<Area, Guid> areaRepository,
            IRepository<AreaAdminAssignment, Guid> areaAdminAssignmentRepository,
            IUnitOfWorkManager unitOfWorkManager,
            IAQGreenApprovalAuthorityStabilizer areaAuthorityStabilizer)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _customerProfileUpdater = customerProfileUpdater;
            _emailVerificationScheduler = emailVerificationScheduler;
            _areaRepository = areaRepository;
            _areaAdminAssignmentRepository = areaAdminAssignmentRepository;
            _unitOfWorkManager = unitOfWorkManager;
            _areaAuthorityStabilizer = areaAuthorityStabilizer;
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.View)]
        public async Task<PagedResultDto<AdminMemberDto>> GetAllAsync(AdminMemberListInput input)
        {
            input ??= new AdminMemberListInput();
            ValidateRequestedTenant(input.TenantId, "Member");
            await EnsureHostAccessAsync();
            using (DisableAllTenantDataFiltersForHost())
            {
                var query = MemberQuery();
                if (input.TenantId.HasValue) query = query.Where(row => row.Customer.TenantId == input.TenantId.Value);
                if (input.AreaId.HasValue) query = query.Where(row => row.Customer.AreaId == input.AreaId.Value);
                if (input.IsActive.HasValue) query = query.Where(row => row.Customer.IsActive == input.IsActive.Value);
                if (input.MembershipId.HasValue) query = query.Where(row => row.Membership.Id == input.MembershipId.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    query = query.Where(row => row.Customer.Name.ToLower().Contains(keyword) ||
                        row.Customer.Email.Value.ToLower().Contains(keyword) ||
                        (row.Customer.User.PhoneNumber != null && row.Customer.User.PhoneNumber.ToLower().Contains(keyword)) ||
                        (row.Customer.User.HomeAddress != null && row.Customer.User.HomeAddress.ToLower().Contains(keyword)) ||
                        row.Membership.Name.ToLower().Contains(keyword));
                }
                // Scope and membership validity precede both the total and paging.
                var total = await query.CountAsync();
                var rows = await query.OrderByDescending(row => row.Customer.CreationTime)
                    .ThenByDescending(row => row.Customer.Id)
                    .Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
                return new PagedResultDto<AdminMemberDto>(total,
                    rows.Select(row => Map(row.Customer, row.Membership)).ToList());
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.ChangeTier)]
        public async Task<List<AdminMembershipOptionDto>> GetMembershipOptionsAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Member");
            var member = await GetMemberAsync(input.Id);
            using (DisableTenantDataFilter())
            {
                return await _membershipRepository.GetAll()
                    .Where(plan =>
                        plan.IsActive &&
                        plan.MembershipType != MembershipType.AQGreen &&
                        plan.MembershipType != MembershipType.Onyx &&
                        (!plan.TenantId.HasValue || plan.TenantId == member.TenantId.Value))
                    .OrderBy(plan => plan.MembershipType).ThenBy(plan => plan.Name)
                    .Select(plan => new AdminMembershipOptionDto { Id = plan.Id, Name = plan.Name, MembershipType = (int)plan.MembershipType })
                    .ToListAsync();
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.View)]
        public async Task<AdminMemberDto> GetAsync(EntityDto<int> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "Member");
            var member = await GetMemberAsync(input.Id);
            return Map(member, await GetMembershipAsync(member.MembershipId.Value, member.TenantId.Value));
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.Edit)]
        public async Task<AdminMemberDto> EditProfileAsync(EditMemberProfileInput input)
        {
            if (input == null) throw Failed("Member profile update", "The request body was empty.");
            ValidatePositiveId(input.Id, "Member");
            return await MutateMemberAsync(input.Id, async member =>
            {
                await UpdateMemberAsync(member, input.FirstName, input.LastName, input.Email, input.ContactNumber, input.HomeAddress, member.MembershipId.Value, member.IsActive);
                LogAdminMutation("Member", "profile updated", member.Id, member.TenantId, input.Justification);
                return Map(member, await GetMembershipAsync(member.MembershipId.Value, member.TenantId.Value));
            });
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.Suspend)]
        public async Task<AdminMemberDto> SuspendAsync(SuspendMemberInput input)
        {
            if (input == null) throw Failed("Member suspension", "The request body was empty.");
            ValidatePositiveId(input.Id, "Member");
            return await MutateMemberAsync(input.Id, async member =>
            {
                await UpdateMemberAsync(member, member.User.Name, member.User.Surname, member.User.EmailAddress, member.User.PhoneNumber, member.User.HomeAddress, member.MembershipId.Value, false);
                LogAdminMutation("Member", "suspended", member.Id, member.TenantId, input.Justification);
                return Map(member, await GetMembershipAsync(member.MembershipId.Value, member.TenantId.Value));
            });
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.ChangeTier)]
        public async Task<AdminMemberDto> ChangeTierAsync(ChangeMemberTierInput input)
        {
            if (input == null) throw Failed("Member tier change", "The request body was empty.");
            ValidatePositiveId(input.Id, "Member");
            ValidatePositiveId(input.MembershipId, "Membership");
            return await MutateMemberAsync(input.Id, async member =>
            {
                await UpdateMemberAsync(member, member.User.Name, member.User.Surname, member.User.EmailAddress, member.User.PhoneNumber, member.User.HomeAddress, input.MembershipId, member.IsActive);
                LogAdminMutation("Member", $"tier changed to {input.MembershipId}", member.Id, member.TenantId, input.Justification);
                return Map(member, await GetMembershipAsync(input.MembershipId, member.TenantId.Value));
            });
        }

        private async Task UpdateMemberAsync(Customer member, string firstName, string lastName, string email, string contactNumber, string homeAddress, int membershipId, bool isActive)
        {
            var originalSecurityStamp = member.User.SecurityStamp;
            await _customerProfileUpdater.UpdateAsync(member, new AdminCustomerProfileUpdate
            {
                FirstName = firstName, LastName = lastName, Email = email,
                ContactNumber = contactNumber, HomeAddress = homeAddress,
                MembershipId = membershipId, IsActive = isActive
            });
            if (member.User.IsActive &&
                !member.User.IsEmailConfirmed &&
                originalSecurityStamp != member.User.SecurityStamp)
            {
                await _emailVerificationScheduler.ScheduleAsync(
                    member.User,
                    $"email-verification:{member.TenantId}:{member.UserId}:admin-profile-change:{member.User.SecurityStamp}");
            }
        }

        private async Task EnsureHostAccessAsync()
        {
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException("Host member administration requires AllTenants permission.");
        }

        private IQueryable<MemberQueryRow> MemberQuery()
        {
            var customers = _customerRepository.GetAllIncluding(customer => customer.User, customer => customer.Area)
                .Where(customer => !customer.IsDeleted && customer.TenantId.HasValue && customer.MembershipId.HasValue &&
                    customer.User != null && !customer.User.IsDeleted && customer.User.TenantId == customer.TenantId);
            if (AbpSession.TenantId.HasValue)
            {
                var tenantId = AbpSession.TenantId.Value;
                var now = Clock.Now.ToUniversalTime();
                var assignments = ActiveAreaAdministratorAssignments(
                    _areaAdminAssignmentRepository, _areaRepository)
                    .Where(assignment => assignment.EffectiveFrom <= now);
                customers = customers.Where(customer => customer.TenantId == tenantId &&
                    customer.AreaId.HasValue && assignments.Any(assignment => assignment.AreaId == customer.AreaId.Value));
            }
            // Global plans remain available; missing/foreign-Tenant plans cannot
            // turn into a short page with an inflated total.
            // EF applies IgnoreQueryFilters to the whole composed query. Keep
            // Customer/User soft-delete predicates explicit above as well as scope.
            var plans = _membershipRepository.GetAll().IgnoreQueryFilters();
            return from customer in customers
                join membership in plans on customer.MembershipId equals (int?)membership.Id
                where !membership.TenantId.HasValue || membership.TenantId == customer.TenantId
                select new MemberQueryRow { Customer = customer, Membership = membership };
        }

        private async Task<Customer> GetMemberAsync(int id)
        {
            await EnsureHostAccessAsync();
            using (DisableAllTenantDataFiltersForHost())
            {
                var row = await MemberQuery().SingleOrDefaultAsync(item => item.Customer.Id == id);
                if (row == null) throw MemberOutsideScope();
                return row.Customer;
            }
        }

        private async Task<AdminMemberDto> MutateMemberAsync(int id, Func<Customer, Task<AdminMemberDto>> mutation)
        {
            await EnsureHostAccessAsync();
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true,
                IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted
            }))
            using (DisableAllTenantDataFiltersForHost())
            {
                if (AbpSession.TenantId.HasValue)
                {
                    var hint = await MemberQuery().AsNoTracking()
                        .Where(row => row.Customer.Id == id)
                        .Select(row => new { row.Customer.Id, TenantId = row.Customer.TenantId.Value })
                        .SingleOrDefaultAsync();
                    if (hint == null) throw MemberOutsideScope();
                    // Reuse existing Customer/Area/assignment row locks until commit.
                    // The subsequent query re-reads scope after any lock wait.
                    await _areaAuthorityStabilizer.StabilizeAsync(hint.TenantId, hint.Id, AbpSession.GetUserId());
                }
                var member = await GetMemberAsync(id);
                var result = await mutation(member);
                await uow.CompleteAsync();
                return result;
            }
        }

        private static AbpAuthorizationException MemberOutsideScope() =>
            new AbpAuthorizationException("The member was not found in your authorized Tenant/Area scope.");

        private sealed class MemberQueryRow
        {
            public Customer Customer { get; set; }
            public Membership Membership { get; set; }
        }

        private async Task<Membership> GetMembershipAsync(int membershipId, int tenantId)
        {
            using (DisableTenantDataFilter())
            {
                var membership = await _membershipRepository.FirstOrDefaultAsync(plan => plan.Id == membershipId &&
                    (!plan.TenantId.HasValue || plan.TenantId == tenantId));
                if (membership == null) throw Failed("Member lookup", "The membership plan was not found.");
                return membership;
            }
        }

        private static AdminMemberDto Map(Customer member, Membership membership) => new AdminMemberDto
        {
            Id = member.Id, TenantId = member.TenantId.Value, UserId = member.UserId,
            AreaId = member.AreaId, AreaName = member.Area == null || member.Area.IsDeleted ? null : member.Area.Name,
            FirstName = member.User.Name, LastName = member.User.Surname, Email = member.Email.Value,
            ContactNumber = member.User.PhoneNumber, HomeAddress = member.User.HomeAddress,
            MembershipId = membership.Id, MembershipName = membership.Name, MembershipType = (int)membership.MembershipType,
            IsActive = member.IsActive, CreationTime = member.CreationTime
        };
    }
}
