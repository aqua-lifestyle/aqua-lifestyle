using System.Linq;
using System.Reflection;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class MigrationDiscoveryTests
    {
        [Fact]
        public void EveryMigrationShouldDeclareDiscoveryMetadata()
        {
            var migrationTypes = typeof(AqualLifeStyleDbContext)
                .Assembly
                .DefinedTypes
                .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
                .ToList();

            migrationTypes.ShouldNotBeEmpty();

            foreach (var migrationType in migrationTypes)
            {
                migrationType.GetCustomAttribute<MigrationAttribute>()
                    .ShouldNotBeNull($"{migrationType.Name} must declare its migration identifier");
                migrationType.GetCustomAttribute<DbContextAttribute>()
                    .ShouldNotBeNull($"{migrationType.Name} must declare its database context");
            }
        }

        [Fact]
        public void InternalAccountInvitationMigrationShouldBeDiscoverable()
        {
            using var context = new AqualLifeStyleDbContext(
                new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql("Host=localhost;Database=discovery;Username=discovery;Password=discovery")
                    .Options);

            context.Database.GetMigrations()
                .ShouldContain("20260804040549_AddInternalAccountInvitations");
        }

        [Fact]
        public void AQGreenPlacementFoundationMigrationShouldBeDiscoverable()
        {
            using var context = new AqualLifeStyleDbContext(
                new DbContextOptionsBuilder<AqualLifeStyleDbContext>()
                    .UseNpgsql("Host=localhost;Database=discovery;Username=discovery;Password=discovery")
                    .Options);

            context.Database.GetMigrations()
                .ShouldContain("20260825095740_AddAQGreenPlacementV2Foundation");
        }
    }
}
