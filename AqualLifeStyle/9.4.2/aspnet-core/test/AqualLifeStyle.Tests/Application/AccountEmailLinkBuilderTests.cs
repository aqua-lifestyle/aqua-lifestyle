using System;
using System.Collections.Generic;
using AqualLifeStyle.Authorization.Accounts;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AccountEmailLinkBuilderTests
    {
        [Fact]
        public void Build_KeepsTheOneTimeTokenOutOfTheRequestUrl()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["App:ClientRootAddress"] = "https://app.example.test/"
                })
                .Build();

            var link = new Uri(new AccountEmailLinkBuilder(configuration).Build(
                "/verify-email", 1, 42, "one-time+token", "Default", "/profile"));

            link.Query.ShouldNotContain("token");
            QueryHelpers.ParseQuery(link.Query)["tenantId"].ToString().ShouldBe("1");
            QueryHelpers.ParseQuery(link.Fragment.TrimStart('#'))["token"].ToString().ShouldBe("one-time+token");
        }
    }
}
