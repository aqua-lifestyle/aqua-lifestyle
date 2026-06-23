using System.Reflection;
using AqualLifeStyle.Web.Host.Startup;
using Shouldly;

namespace AqualLifeStyle.Tests.WebHost
{
    /// <summary>
    /// Covers <c>AqualLifeStyleWebHostModule.NormalizeRedisConfiguration</c>, which converts a
    /// <c>redis://</c>/<c>rediss://</c> connection URL (as commonly supplied by hosting
    /// providers) into the comma-separated options string expected by StackExchange.Redis /
    /// ABP's Redis cache configuration. Non-URL values are passed through unchanged. The method
    /// is a private implementation detail, exercised through reflection.
    /// </summary>
    public class AqualLifeStyleWebHostModuleNormalizeRedisConfigurationTests
    {
        private static string InvokeNormalizeRedisConfiguration(string configuration)
        {
            var method = typeof(AqualLifeStyleWebHostModule).GetMethod(
                "NormalizeRedisConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.ShouldNotBeNull();

            return (string)method.Invoke(null, new object[] { configuration });
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithPlainStackExchangeStyleValue_ReturnsUnchanged()
        {
            InvokeNormalizeRedisConfiguration("redis-server:6379,abortConnect=false")
                .ShouldBe("redis-server:6379,abortConnect=false");
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithRedisUri_ConvertsToStackExchangeFormat()
        {
            var result = InvokeNormalizeRedisConfiguration("redis://myredis:6380");

            result.ShouldBe("myredis:6380,abortConnect=false");
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithRedissUri_AddsSslOption()
        {
            var result = InvokeNormalizeRedisConfiguration("rediss://myredis:6380");

            result.ShouldBe("myredis:6380,abortConnect=false,ssl=true");
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithUserAndPassword_IncludesBoth()
        {
            var result = InvokeNormalizeRedisConfiguration("redis://user:secret@myredis:6380");

            result.ShouldBe("myredis:6380,abortConnect=false,user=user,password=secret");
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithPasswordOnly_OmitsEmptyUser()
        {
            var result = InvokeNormalizeRedisConfiguration("redis://:secret@myredis:6380");

            result.ShouldBe("myredis:6380,abortConnect=false,password=secret");
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithEncodedCredentials_UnescapesValues()
        {
            var result = InvokeNormalizeRedisConfiguration("redis://us%40er:sec%3Aret@myredis:6380");

            result.ShouldBe("myredis:6380,abortConnect=false,user=us@er,password=sec:ret");
        }

        [Fact]
        public void NormalizeRedisConfiguration_WithEmptyString_ReturnsEmptyString()
        {
            InvokeNormalizeRedisConfiguration(string.Empty).ShouldBe(string.Empty);
        }
    }
}