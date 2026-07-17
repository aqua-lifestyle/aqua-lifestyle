using System;
using System.Reflection;
using AqualLifeStyle.Configuration;
using Shouldly;

namespace AqualLifeStyle.Tests.Configuration
{
    /// <summary>
    /// Covers the DATABASE_URL to Npgsql connection-string conversion logic added to
    /// <see cref="AppConfigurations"/>. The methods under test are private implementation
    /// details, so they are exercised through reflection (consistent with the reflection-based
    /// pattern already used for private members elsewhere in this test project).
    /// </summary>
    public class AppConfigurationsTests
    {
        private static string InvokeConvertPostgresUrlToConnectionString(string databaseUrl)
        {
            var method = typeof(AppConfigurations).GetMethod(
                "ConvertPostgresUrlToConnectionString",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.ShouldNotBeNull();

            try
            {
                return (string)method.Invoke(null, new object[] { databaseUrl });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static string InvokeQuoteConnectionStringValue(string value)
        {
            var method = typeof(AppConfigurations).GetMethod(
                "QuoteConnectionStringValue",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.ShouldNotBeNull();

            return (string)method.Invoke(null, new object[] { value });
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithValidPostgresUrl_BuildsExpectedConnectionString()
        {
            var result = InvokeConvertPostgresUrlToConnectionString(
                "postgres://myuser:mypassword@myhost:5433/mydb");

            result.ShouldBe(
                "Host=\"myhost\";Port=5433;Database=\"mydb\";Username=\"myuser\";Password=\"mypassword\";SSL Mode=Prefer;Trust Server Certificate=true");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithPostgresqlScheme_IsAccepted()
        {
            var result = InvokeConvertPostgresUrlToConnectionString("postgresql://user:pass@host/db");

            result.ShouldContain("Host=\"host\"");
            result.ShouldContain("Database=\"db\"");
            result.ShouldContain("Username=\"user\"");
            result.ShouldContain("Password=\"pass\"");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithoutExplicitPort_UsesDefaultPort()
        {
            var result = InvokeConvertPostgresUrlToConnectionString("postgres://user:pass@host/db");

            result.ShouldContain("Port=5432");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithExplicitPort_UsesThatPort()
        {
            var result = InvokeConvertPostgresUrlToConnectionString("postgres://user:pass@host:6000/db");

            result.ShouldContain("Port=6000");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_AlwaysAppendsSslAndTrustServerCertificate()
        {
            var result = InvokeConvertPostgresUrlToConnectionString("postgres://user:pass@host/db");

            result.ShouldEndWith("SSL Mode=Prefer;Trust Server Certificate=true");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithNonPostgresScheme_Throws()
        {
            var ex = Should.Throw<InvalidOperationException>(() =>
                InvokeConvertPostgresUrlToConnectionString("mysql://user:pass@host/db"));

            ex.Message.ShouldBe("DATABASE_URL must be a valid PostgreSQL URL.");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithUnparsableUrl_Throws()
        {
            Should.Throw<InvalidOperationException>(() =>
                InvokeConvertPostgresUrlToConnectionString("not-a-valid-url"));
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithoutPassword_Throws()
        {
            var ex = Should.Throw<InvalidOperationException>(() =>
                InvokeConvertPostgresUrlToConnectionString("postgres://useronly@host/db"));

            ex.Message.ShouldBe("DATABASE_URL must include a username and password.");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithEncodedCredentials_UnescapesValues()
        {
            var result = InvokeConvertPostgresUrlToConnectionString(
                "postgres://my%40user:p%40ss%3Aword@host/db");

            result.ShouldContain("Username=\"my@user\"");
            result.ShouldContain("Password=\"p@ss:word\"");
        }

        [Fact]
        public void ConvertPostgresUrlToConnectionString_WithLeadingSlashInPath_TrimsSlashFromDatabaseName()
        {
            var result = InvokeConvertPostgresUrlToConnectionString("postgres://user:pass@host:5432/mydatabase");

            result.ShouldContain("Database=\"mydatabase\"");
        }

        [Theory]
        [InlineData("value", "\"value\"")]
        [InlineData("", "\"\"")]
        [InlineData("has\"quote", "\"has\"\"quote\"")]
        public void QuoteConnectionStringValue_QuotesAndEscapesEmbeddedQuotes(string input, string expected)
        {
            InvokeQuoteConnectionStringValue(input).ShouldBe(expected);
        }

        [Fact]
        public void QuoteConnectionStringValue_WithNull_ReturnsEmptyQuotedString()
        {
            InvokeQuoteConnectionStringValue(null).ShouldBe("\"\"");
        }
    }
}