using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Members.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Members
{
    [Audited]
    public class AdminMemberAppService : AdminAppServiceBase, IAdminMemberAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IAdminCustomerProfileUpdater _customerProfileUpdater;

        public AdminMemberAppService(
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IAdminCustomerProfileUpdater customerProfileUpdater)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _customerProfileUpdater = customerProfileUpdater;
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.View)]
        public async Task<PagedResultDto<AdminMemberDto>> GetAllAsync(AdminMemberListInput input)
        {
            input ??= new AdminMemberListInput();
            ValidateRequestedTenant(input.TenantId, "Member");
            using (DisableTenantFilterForHost())
            {
                var query = _customerRepository.GetAllIncluding(customer => customer.User)
                    .Where(customer => customer.TenantId.HasValue && customer.MembershipId.HasValue);
                if (AbpSession.TenantId.HasValue) query = query.Where(customer => customer.TenantId == AbpSession.TenantId.Value);
                else if (input.TenantId.HasValue) query = query.Where(customer => customer.TenantId == input.TenantId.Value);
                if (input.IsActive.HasValue) query = query.Where(customer => customer.IsActive == input.IsActive.Value);
                if (input.MembershipId.HasValue) query = query.Where(customer => customer.MembershipId == input.MembershipId.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    query = query.Where(customer => customer.Name.ToLower().Contains(keyword) || customer.Email.Value.ToLower().Contains(keyword));
                }
                var total = await query.CountAsync();
                var members = await query.OrderByDescending(customer => customer.CreationTime)
                    .Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
                var membershipIds = members.Select(member => member.MembershipId.Value).Distinct().ToArray();
                List<Membership> memberships;
                using (DisableTenantDataFilter())
                {
                    memberships = await _membershipRepository.GetAll()
                        .Where(plan => membershipIds.Contains(plan.Id))
                        .ToListAsync();
                }
                var membershipById = memberships.ToDictionary(tier => tier.Id);
                return new PagedResultDto<AdminMemberDto>(total, members
                    .Where(member => membershipById.ContainsKey(member.MembershipId.Value))
                    .Select(member => Map(member, membershipById[member.MembershipId.Value])).ToList());
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
            var member = await GetMemberAsync(input.Id);
            await UpdateMemberAsync(member, input.FirstName, input.LastName, input.Email, member.MembershipId.Value, member.IsActive);
            LogAdminMutation("Member", "profile updated", member.Id, member.TenantId, input.Justification);
            return Map(member, await GetMembershipAsync(member.MembershipId.Value, member.TenantId.Value));
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.Suspend)]
        public async Task<AdminMemberDto> SuspendAsync(SuspendMemberInput input)
        {
            if (input == null) throw Failed("Member suspension", "The request body was empty.");
            ValidatePositiveId(input.Id, "Member");
            var member = await GetMemberAsync(input.Id);
            await UpdateMemberAsync(member, member.User.Name, member.User.Surname, member.User.EmailAddress, member.MembershipId.Value, false);
            LogAdminMutation("Member", "suspended", member.Id, member.TenantId, input.Justification);
            return Map(member, await GetMembershipAsync(member.MembershipId.Value, member.TenantId.Value));
        }

        [AbpAuthorize(AquaPermissions.Admin.Members.ChangeTier)]
        public async Task<AdminMemberDto> ChangeTierAsync(ChangeMemberTierInput input)
        {
            if (input == null) throw Failed("Member tier change", "The request body was empty.");
            ValidatePositiveId(input.Id, "Member");
            ValidatePositiveId(input.MembershipId, "Membership");
            var member = await GetMemberAsync(input.Id);
            await UpdateMemberAsync(member, member.User.Name, member.User.Surname, member.User.EmailAddress, input.MembershipId, member.IsActive);
            LogAdminMutation("Member", $"tier changed to {input.MembershipId}", member.Id, member.TenantId, input.Justification);
            return Map(member, await GetMembershipAsync(input.MembershipId, member.TenantId.Value));
        }

        private Task UpdateMemberAsync(Customer member, string firstName, string lastName, string email, int membershipId, bool isActive) =>
            _customerProfileUpdater.UpdateAsync(member, new AdminCustomerProfileUpdate
            {
                FirstName = firstName, LastName = lastName, Email = email,
                MembershipId = membershipId, IsActive = isActive
            });

        private async Task<Customer> GetMemberAsync(int id)
        {
            using (DisableTenantFilterForHost())
            {
                var query = _customerRepository.GetAllIncluding(customer => customer.User)
                    .Where(customer => customer.Id == id && customer.TenantId.HasValue && customer.MembershipId.HasValue);
                if (AbpSession.TenantId.HasValue) query = query.Where(customer => customer.TenantId == AbpSession.TenantId.Value);
                var member = await query.SingleOrDefaultAsync();
                if (member == null) throw Failed("Member lookup", "The member was not found.");
                return member;
            }
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
            FirstName = member.User.Name, LastName = member.User.Surname, Email = member.Email.Value,
            MembershipId = membership.Id, MembershipName = membership.Name, MembershipType = (int)membership.MembershipType,
            IsActive = member.IsActive, CreationTime = member.CreationTime
        };
    }
}
