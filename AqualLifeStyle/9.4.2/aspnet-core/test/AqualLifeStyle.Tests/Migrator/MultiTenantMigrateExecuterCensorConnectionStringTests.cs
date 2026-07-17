using System.Reflection;
using AqualLifeStyle.Migrator;
using Shouldly;

namespace AqualLifeStyle.Tests.Migrator
{
    /// <summary>
    /// Covers the connection-string masking logic added to <see cref="MultiTenantMigrateExecuter"/>
    /// so that migration logs no longer leak database credentials. The method under test is a
    /// private implementation detail, exercised through reflection.
    /// </summary>
    public class MultiTenantMigrateExecuterCensorConnectionStringTests
    {
        private static string InvokeCensorConnectionString(string connectionString)
        {
            var method = typeof(MultiTenantMigrateExecuter).GetMethod(
                "CensorConnectionString",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.ShouldNotBeNull();

            return (string)method.Invoke(null, new object[] { connectionString });
        }

        [Fact]
        public void CensorConnectionString_MasksPasswordKey()
        {
            var result = InvokeCensorConnectionString(
                "Host=localhost;Database=db;Username=admin;Password=SuperSecret;");

            result.ShouldContain("Password=*****");
            result.ShouldNotContain("SuperSecret");
        }

        [Fact]
        public void CensorConnectionString_MasksUserIdAndPwdKeys()
        {
            var result = InvokeCensorConnectionString("Server=host;Database=db;User Id=sa;Pwd=hunter2;");

            result.ShouldNotContain("hunter2");
            result.ShouldContain("*****");
        }

        [Fact]
        public void CensorConnectionString_MasksUidKey()
        {
            var result = InvokeCensorConnectionString("Server=host;Database=db;Uid=root;Pwd=hunter2;");

            result.ShouldNotContain("root");
            result.ShouldNotContain("hunter2");
        }

        [Fact]
        public void CensorConnectionString_PreservesNonSensitiveValues()
        {
            var result = InvokeCensorConnectionString(
                "Host=myhost;Port=5432;Database=mydb;Username=admin;Password=secret;");

            result.ShouldContain("myhost");
            result.ShouldContain("5432");
            result.ShouldContain("mydb");
        }

        [Fact]
        public void CensorConnectionString_WithoutSensitiveKeys_DoesNotAddMasking()
        {
            var result = InvokeCensorConnectionString("Host=myhost;Database=mydb;");

            result.ShouldContain("myhost");
            result.ShouldContain("mydb");
            result.ShouldNotContain("*****");
        }

        [Fact]
        public void CensorConnectionString_IsCaseInsensitiveForKeys()
        {
            var result = InvokeCensorConnectionString("Host=myhost;PASSWORD=topsecret;");

            result.ShouldNotContain("topsecret");
            result.ShouldContain("*****");
        }

        [Fact]
        public void CensorConnectionString_WithEmptyString_ReturnsEmpty()
        {
            var result = InvokeCensorConnectionString(string.Empty);

            result.ShouldBe(string.Empty);
        }
    }
}