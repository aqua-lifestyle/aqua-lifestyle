using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Castle.Facilities.Logging;
using Abp.AspNetCore;
using Abp.AspNetCore.Mvc.Antiforgery;
using Abp.Extensions;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Identity;
using Abp.AspNetCore.SignalR.Hubs;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Castle.Services.Logging.SerilogIntegration;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Email;
using AqualLifeStyle.Web.Host.Email;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using System.Threading.RateLimiting;

namespace AqualLifeStyle.Web.Host.Startup
{
    public class Startup
    {
        private const string _defaultCorsPolicyName = "localhost";

        private const string _apiVersion = "v1";

        private readonly IConfigurationRoot _appConfiguration;
        private readonly IWebHostEnvironment _environment;
        public Startup(IWebHostEnvironment env)
        {
            _environment = env;
            _appConfiguration = env.GetAppConfiguration();
            // Run this before any other Production-only setup so an accidental
            // demo request fails with the explicit non-production boundary.
            AQGreenV2DemoConfiguration.Validate(_environment, _appConfiguration);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            //MVC
            services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AbpAutoValidateAntiforgeryTokenAttribute());
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

            IdentityRegistrar.Register(services);
            var dataProtection = services.AddAqualLifeStyleDataProtection(_appConfiguration);
            if (_environment.IsProduction())
            {
                dataProtection.ProtectKeysWithCertificate(LoadDataProtectionCertificate(
                    "DataProtection:CertificateBase64",
                    "DataProtection:CertificatePassword",
                    true));
                var previousCertificate = LoadDataProtectionCertificate(
                    "DataProtection:PreviousCertificateBase64",
                    "DataProtection:PreviousCertificatePassword",
                    false);
                if (previousCertificate != null)
                {
                    dataProtection.UnprotectKeysWithAnyCertificate(previousCertificate);
                }
            }
            AuthConfigurer.Configure(services, _appConfiguration);

            services.AddSignalR();
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                if (_appConfiguration.GetValue<bool>("RENDER"))
                {
                    // Render's service port is reachable only through its managed ingress proxy.
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                }
            });
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 10,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(10)
                        }));
            });

            // Configure CORS for angular2 UI
            services.AddCors(
                options => options.AddPolicy(
                    _defaultCorsPolicyName,
                    builder => builder
                        .WithOrigins(
                            // App:CorsOrigins in appsettings.json can contain more than one address separated by comma.
                            _appConfiguration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                )
            );

            // Swagger - Enable this line and the related lines in Configure method to enable swagger UI
            ConfigureSwagger(services);

            // Register IHttpContextAccessor so ABP exception converters can access the current request correlation id.
            services.AddHttpContextAccessor();
            services.AddHttpClient<IYocoCheckoutGateway, YocoCheckoutGateway>();
            services.Configure<BirdOptions>(_appConfiguration.GetSection("Bird"));
            services.AddHttpClient<ITransactionalEmailDeliveryGateway, BirdTransactionalEmailDeliveryGateway>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            // Configure Abp and Dependency Injection
            services.AddAbpWithoutCreatingServiceProvider<AqualLifeStyleWebHostModule>(
                // Route ABP's Castle logger through the same Serilog console pipeline.
                options => options.IocManager.IocContainer.AddFacility<LoggingFacility>(
                    f => f.LogUsing<SerilogFactory>()
                )
            );
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAbp(options => { options.UseAbpRequestLocalization = false; }); // Initializes ABP framework.

            app.UseCors(_defaultCorsPolicyName); // Enable CORS!

            app.UseStaticFiles();

            app.UseRouting();
            app.UseForwardedHeaders();
            app.UseWhen(
                context => IsAccountEmailRequest(context.Request.Path),
                branch => branch.UseRateLimiter());

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAbpRequestLocalization();

            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<AbpCommonHub>("/signalr");
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllerRoute("defaultWithArea", "{area}/{controller=Home}/{action=Index}/{id?}");
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint
            app.UseSwagger(c => { c.RouteTemplate = "swagger/{documentName}/swagger.json"; });

            // Enable middleware to serve swagger-ui assets (HTML, JS, CSS etc.)
            app.UseSwaggerUI(options =>
            {
                // specifying the Swagger JSON endpoint.
                options.SwaggerEndpoint($"/swagger/{_apiVersion}/swagger.json", $"AqualLifeStyle API {_apiVersion}");
                options.IndexStream = () => Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AqualLifeStyle.Web.Host.wwwroot.swagger.ui.index.html");
                options.DisplayRequestDuration(); // Controls the display of the request duration (in milliseconds) for "Try it out" requests.
            }); // URL: /swagger
        }

        private static bool IsAccountEmailRequest(PathString path)
        {
            return path.StartsWithSegments("/api/services/app/Account/ResendEmailVerification") ||
                   path.StartsWithSegments("/api/services/app/Account/RequestPasswordReset") ||
                   path.StartsWithSegments("/api/services/app/Account/Register");
        }

        private X509Certificate2 LoadDataProtectionCertificate(
            string certificateKey,
            string passwordKey,
            bool required)
        {
            var encodedCertificate = _appConfiguration[certificateKey];
            var certificatePassword = _appConfiguration[passwordKey];
            if (string.IsNullOrWhiteSpace(encodedCertificate) &&
                string.IsNullOrWhiteSpace(certificatePassword) && !required)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(encodedCertificate) || string.IsNullOrWhiteSpace(certificatePassword))
            {
                throw new InvalidOperationException(
                    $"Production Data Protection configuration is incomplete. Set {certificateKey.Replace(":", "__")} and {passwordKey.Replace(":", "__")}.");
            }

            try
            {
#pragma warning disable SYSLIB0057 // .NET 8 has no non-obsolete in-memory PKCS#12 loader.
                var certificate = new X509Certificate2(
                    Convert.FromBase64String(encodedCertificate),
                    certificatePassword,
                    X509KeyStorageFlags.EphemeralKeySet);
#pragma warning restore SYSLIB0057
                if (!certificate.HasPrivateKey)
                {
                    certificate.Dispose();
                    throw new CryptographicException("The certificate has no private key.");
                }

                return certificate;
            }
            catch (Exception exception) when (exception is FormatException || exception is CryptographicException)
            {
                throw new InvalidOperationException(
                    $"Production Data Protection certificate is invalid. Check {certificateKey.Replace(":", "__")} and {passwordKey.Replace(":", "__")}.",
                    exception);
            }
        }

        private void ConfigureSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(_apiVersion, new OpenApiInfo
                {
                    Version = _apiVersion,
                    Title = "AqualLifeStyle API",
                    Description = "AqualLifeStyle",
                    // uncomment if needed TermsOfService = new Uri("https://example.com/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "AqualLifeStyle",
                        Email = string.Empty,
                        Url = new Uri("https://twitter.com/aspboilerplate"),
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://github.com/aspnetboilerplate/aspnetboilerplate/blob/dev/LICENSE"),
                    }
                });
                options.DocInclusionPredicate((docName, description) => true);

                // Define the BearerAuth scheme that's in use
                options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme()
                {
                    Description =
                        "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });

                //add summaries to swagger
                bool canShowSummaries = _appConfiguration.GetValue<bool>("Swagger:ShowSummaries");
                if (canShowSummaries)
                {
                    var hostXmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var hostXmlPath = Path.Combine(AppContext.BaseDirectory, hostXmlFile);
                    options.IncludeXmlComments(hostXmlPath);

                    var applicationXml = $"AqualLifeStyle.Application.xml";
                    var applicationXmlPath = Path.Combine(AppContext.BaseDirectory, applicationXml);
                    options.IncludeXmlComments(applicationXmlPath);

                    var webCoreXmlFile = $"AqualLifeStyle.Web.Core.xml";
                    var webCoreXmlPath = Path.Combine(AppContext.BaseDirectory, webCoreXmlFile);
                    options.IncludeXmlComments(webCoreXmlPath);
                }
            });
        }
    }
}
