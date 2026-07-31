using System;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Authorization.Users;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public interface IUserPasswordSetupLinkGenerator
    {
        Task<string> GenerateAsync(User user, string areaName);
    }

    public class UserPasswordSetupLinkGenerator : IUserPasswordSetupLinkGenerator, ITransientDependency
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager _userManager;

        public UserPasswordSetupLinkGenerator(IConfiguration configuration, UserManager userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<string> GenerateAsync(User user, string areaName)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(areaName)) throw new ArgumentException("Area name is required.", nameof(areaName));

            var clientRootAddress = _configuration["App:ClientRootAddress"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(clientRootAddress))
                throw new InvalidOperationException("The customer application address is not configured.");

            await _userManager.InitializeOptionsAsync(user.TenantId);
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            return $"{clientRootAddress}/reset-password?area={Uri.EscapeDataString(areaName)}&userId={user.Id}#token={Uri.EscapeDataString(resetToken)}";
        }
    }
}
