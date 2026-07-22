using System;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.EntityFrameworkCore.Seed;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Security
{
    public class AdministratorBootstrapPasswordProviderTests
    {
        [Fact]
        public void DevelopmentWithoutSecret_UsesLocalDefault()
        {
            AdministratorBootstrapPasswordProvider.ResolvePassword(
                    "Development",
                    null,
                    AdministratorBootstrapPasswordProvider.SharedAdministratorPasswordVariable)
                .ShouldBe(User.DefaultPassword);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("123qwe")]
        [InlineData("MissingSpecial123")]
        [InlineData("missing-uppercase-123!")]
        public void ProductionWithMissingOrWeakSecret_FailsClosed(string configuredPassword)
        {
            var exception = Should.Throw<InvalidOperationException>(() =>
                AdministratorBootstrapPasswordProvider.ResolvePassword(
                    "Production",
                    configuredPassword,
                    AdministratorBootstrapPasswordProvider.SharedAdministratorPasswordVariable));

            exception.Message.ShouldContain(AdministratorBootstrapPasswordProvider.SharedAdministratorPasswordVariable);
            if (!string.IsNullOrEmpty(configuredPassword))
            {
                exception.Message.ShouldNotContain(configuredPassword);
            }
        }

        [Fact]
        public void ProductionWithStrongSecret_UsesConfiguredPassword()
        {
            const string configuredPassword = "OneTimeBootstrap123!";

            AdministratorBootstrapPasswordProvider.ResolvePassword(
                    "Production",
                    configuredPassword,
                    AdministratorBootstrapPasswordProvider.SharedAdministratorPasswordVariable)
                .ShouldBe(configuredPassword);
        }
    }
}
