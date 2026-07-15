using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.IdentityFramework;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Users
{
    [Audited]
    public class AdminUserAppService : AdminAppServiceBase, IAdminUserAppService
    {
        private readonly IRepository<User, long> _userRepository;
        private readonly UserManager _userManager;

        public AdminUserAppService(IRepository<User, long> userRepository, UserManager userManager)
        {
            _userRepository = userRepository;
            _userManager = userManager;
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.View)]
        public async Task<PagedResultDto<AdminUserDto>> GetAllAsync(AdminUserListInput input)
        {
            input ??= new AdminUserListInput();
            ValidateRequestedTenant(input.TenantId, "User");
            using (DisableTenantFilterForHost())
            {
                var query = _userRepository.GetAll().Where(user => user.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue) query = query.Where(user => user.TenantId == AbpSession.TenantId.Value);
                else if (input.TenantId.HasValue) query = query.Where(user => user.TenantId == input.TenantId.Value);
                if (input.IsActive.HasValue) query = query.Where(user => user.IsActive == input.IsActive.Value);
                if (input.Role.HasValue) query = query.Where(user => user.Role == input.Role.Value);
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    query = query.Where(user => user.UserName.ToLower().Contains(keyword) ||
                        user.Name.ToLower().Contains(keyword) || user.Surname.ToLower().Contains(keyword) ||
                        user.EmailAddress.ToLower().Contains(keyword));
                }
                var total = await query.CountAsync();
                var users = await query.OrderByDescending(user => user.CreationTime)
                    .Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
                return new PagedResultDto<AdminUserDto>(total, users.Select(Map).ToList());
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.View)]
        public async Task<AdminUserDto> GetAsync(EntityDto<long> input)
        {
            ValidatePositiveId(input?.Id ?? 0, "User");
            return Map(await GetUserAsync(input.Id));
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Create)]
        public async Task<AdminUserDto> CreateAsync(AdminCreateUserInput input)
        {
            if (input == null) throw Failed("User creation", "The request body was empty.");
            ValidateRole(input.Role);
            var tenantId = ResolveTargetTenant(input.TenantId, "User", "creation");
            var tenant = await TenantManager.GetByIdAsync(tenantId);
            if (!tenant.IsActive) throw Failed("User creation", "The selected tenant is inactive.");
            User user;
            using (CurrentUnitOfWork.SetTenantId(tenantId))
            {
                await _userManager.InitializeOptionsAsync(tenantId);
                user = new User
                {
                    TenantId = tenantId,
                    UserName = input.Email.Trim(), Name = input.FirstName.Trim(), Surname = input.LastName.Trim(),
                    EmailAddress = input.Email.Trim(), IsActive = input.IsActive, IsEmailConfirmed = true
                };
                user.SetRole(input.Role);
                user.SetNormalizedNames();
                (await _userManager.CreateAsync(user, input.Password)).CheckErrors(LocalizationManager);
                (await _userManager.SetRolesAsync(user, new[] { input.Role.ToString() })).CheckErrors(LocalizationManager);
                await CurrentUnitOfWork.SaveChangesAsync();
            }
            LogAdminMutation("User", "created", user.Id, user.TenantId, input.Justification);
            return Map(user);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Edit)]
        public async Task<AdminUserDto> UpdateAsync(AdminUpdateUserInput input)
        {
            if (input == null) throw Failed("User update", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            if (!input.IsActive && user.Id == AbpSession.GetUserId()) throw Failed("User update", "You cannot deactivate your own account.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                user.Name = input.FirstName.Trim(); user.Surname = input.LastName.Trim();
                user.EmailAddress = input.Email.Trim(); user.UserName = input.Email.Trim(); user.IsActive = input.IsActive;
                user.SetNormalizedNames();
                (await _userManager.UpdateAsync(user)).CheckErrors(LocalizationManager);
            }
            LogAdminMutation("User", "updated", user.Id, user.TenantId, input.Justification);
            return Map(user);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.AssignRole)]
        public async Task<AdminUserDto> AssignRoleAsync(AdminAssignUserRoleInput input)
        {
            if (input == null) throw Failed("Role assignment", "The request body was empty.");
            ValidatePositiveId(input.Id, "User"); ValidateRole(input.Role);
            var user = await GetUserAsync(input.Id);
            if (user.Id == AbpSession.GetUserId() && user.Role != input.Role)
                throw Failed("Role assignment", "You cannot change your own administrator role.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                user.SetRole(input.Role);
                (await _userManager.UpdateAsync(user)).CheckErrors(LocalizationManager);
                (await _userManager.SetRolesAsync(user, new[] { input.Role.ToString() })).CheckErrors(LocalizationManager);
            }
            LogAdminMutation("User", $"assigned role {input.Role}", user.Id, user.TenantId, input.Justification);
            return Map(user);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.ResetPassword)]
        public async Task ResetPasswordAsync(AdminResetUserPasswordInput input)
        {
            if (input == null) throw Failed("Password reset", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                await _userManager.InitializeOptionsAsync(user.TenantId);
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                (await _userManager.ResetPasswordAsync(user, token, input.NewPassword)).CheckErrors(LocalizationManager);
            }
            LogAdminMutation("User", "password reset", user.Id, user.TenantId, input.Justification);
        }

        [AbpAuthorize(AquaPermissions.Admin.Users.Delete)]
        public async Task DeleteAsync(AdminDeleteUserInput input)
        {
            if (input == null) throw Failed("User removal", "The request body was empty.");
            ValidatePositiveId(input.Id, "User");
            var user = await GetUserAsync(input.Id);
            if (user.Id == AbpSession.GetUserId()) throw Failed("User removal", "You cannot remove your own account.");
            using (CurrentUnitOfWork.SetTenantId(user.TenantId.Value))
            {
                user.IsActive = false;
                (await _userManager.UpdateAsync(user)).CheckErrors(LocalizationManager);
                (await _userManager.DeleteAsync(user)).CheckErrors(LocalizationManager);
            }
            LogAdminMutation("User", "removed", user.Id, user.TenantId, input.Justification);
        }

        private async Task<User> GetUserAsync(long id)
        {
            using (DisableTenantFilterForHost())
            {
                var query = _userRepository.GetAll().Where(user => user.Id == id && user.TenantId.HasValue);
                if (AbpSession.TenantId.HasValue) query = query.Where(user => user.TenantId == AbpSession.TenantId.Value);
                var user = await query.SingleOrDefaultAsync();
                if (user == null) throw Failed("User lookup", "The user was not found.");
                return user;
            }
        }

        private static void ValidateRole(AquaUserRole role)
        {
            if (!Enum.IsDefined(typeof(AquaUserRole), role)) throw Failed("Role assignment", "The selected role is invalid.");
        }
        private static AdminUserDto Map(User user) => new AdminUserDto
        {
            Id = user.Id, TenantId = user.TenantId, UserName = user.UserName, FirstName = user.Name,
            LastName = user.Surname, Email = user.EmailAddress, IsActive = user.IsActive, Role = user.Role,
            CreationTime = user.CreationTime
        };
    }
}
