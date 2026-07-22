using System;
using AqualLifeStyle.Authorization.Users;

namespace AqualLifeStyle.EntityFrameworkCore.Seed
{
    public static class AdministratorBootstrapPasswordProvider
    {
        public const string SharedAdministratorPasswordVariable = "AQUA_INITIAL_ADMIN_PASSWORD";

        public static string GetHostAdministratorPassword() =>
            ResolvePassword(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                Environment.GetEnvironmentVariable(SharedAdministratorPasswordVariable),
                SharedAdministratorPasswordVariable);

        public static string GetAreaAdministratorPassword(int tenantId)
        {
            var areaPasswordVariable = GetAreaAdministratorPasswordVariable(tenantId);
            var areaPassword = Environment.GetEnvironmentVariable(areaPasswordVariable);
            return ResolvePassword(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                string.IsNullOrWhiteSpace(areaPassword)
                    ? Environment.GetEnvironmentVariable(SharedAdministratorPasswordVariable)
                    : areaPassword,
                $"{areaPasswordVariable} or {SharedAdministratorPasswordVariable}");
        }

        public static string GetAreaAdministratorPasswordVariable(int tenantId) =>
            $"AQUA_INITIAL_TENANT_{tenantId}_ADMIN_PASSWORD";

        public static string ResolvePassword(
            string environmentName,
            string configuredPassword,
            string environmentVariableName)
        {
            if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(configuredPassword)
                    ? User.DefaultPassword
                    : configuredPassword;
            }

            if (!IsStrongPassword(configuredPassword))
            {
                throw new InvalidOperationException(
                    $"Set {environmentVariableName} to a secure administrator bootstrap password: " +
                    "at least 16 characters with uppercase, lowercase, number, and special characters.");
            }

            return configuredPassword;
        }

        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 16 || password == User.DefaultPassword)
            {
                return false;
            }

            var hasUppercase = false;
            var hasLowercase = false;
            var hasNumber = false;
            var hasSpecialCharacter = false;

            foreach (var character in password)
            {
                hasUppercase |= char.IsUpper(character);
                hasLowercase |= char.IsLower(character);
                hasNumber |= char.IsDigit(character);
                hasSpecialCharacter |= !char.IsLetterOrDigit(character);
            }

            return hasUppercase && hasLowercase && hasNumber && hasSpecialCharacter;
        }
    }
}
