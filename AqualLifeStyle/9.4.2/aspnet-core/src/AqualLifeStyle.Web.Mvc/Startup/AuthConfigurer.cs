using System;
using System.Text;
using Abp.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using AqualLifeStyle.Authentication.JwtBearer;

namespace AqualLifeStyle.Web.Startup
{
    public static class AuthConfigurer
    {
        public static void Configure(IServiceCollection services, IConfiguration configuration)
        {
            services.ConfigureApplicationCookie(options =>
            {
                options.AccessDeniedPath = "/Account/Forbidden";
            });
            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.Zero;
            });
            
            if (bool.Parse(configuration["Authentication:JwtBearer:IsEnabled"]))
            {
                services.AddAuthentication()
                    .AddJwtBearer(options =>
                    {
                        options.Audience = configuration["Authentication:JwtBearer:Audience"];
                        
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            // The signing key must match!
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["Authentication:JwtBearer:SecurityKey"])),

                            // Validate the JWT Issuer (iss) claim
                            ValidateIssuer = true,
                            ValidIssuer = configuration["Authentication:JwtBearer:Issuer"],

                            // Validate the JWT Audience (aud) claim
                            ValidateAudience = true,
                            ValidAudience = configuration["Authentication:JwtBearer:Audience"],

                            // Validate the token expiry
                            ValidateLifetime = true,

                            // If you want to allow a certain amount of clock drift, set that here
                            ClockSkew = TimeSpan.Zero
                        };
                        options.Events = new JwtBearerEvents
                        {
                            OnTokenValidated = JwtSessionSecurityStampValidator.ValidateAsync
                        };
                    });
                
            }
        }
    }
}
