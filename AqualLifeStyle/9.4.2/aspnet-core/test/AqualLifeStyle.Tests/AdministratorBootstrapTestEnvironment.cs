using System;
using AqualLifeStyle.EntityFrameworkCore.Seed;

namespace AqualLifeStyle.Tests
{
    public static class AdministratorBootstrapTestEnvironment
    {
        private const string TestAdministratorBootstrapPassword = "AquaTestBootstrap123!";

        public static void Configure()
        {
            Environment.SetEnvironmentVariable(
                AdministratorBootstrapPasswordProvider.SharedAdministratorPasswordVariable,
                TestAdministratorBootstrapPassword);
        }
    }
}
