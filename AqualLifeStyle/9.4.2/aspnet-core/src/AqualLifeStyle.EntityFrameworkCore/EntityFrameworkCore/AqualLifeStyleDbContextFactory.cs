using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using DotNetEnv;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Web;

namespace AqualLifeStyle.EntityFrameworkCore
{
    /* This class is needed to run "dotnet ef ..." commands from command line on development. Not used anywhere else */
    public class AqualLifeStyleDbContextFactory : IDesignTimeDbContextFactory<AqualLifeStyleDbContext>
    {
        public AqualLifeStyleDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
            var contentRootFolder = WebContentDirectoryFinder.CalculateContentRootFolder();

            LoadEnvFile(contentRootFolder);

            var configuration = AppConfigurations.Get(contentRootFolder);

            AqualLifeStyleDbContextConfigurer.Configure(builder, configuration.GetConnectionString(AqualLifeStyleConsts.ConnectionStringName));

            return new AqualLifeStyleDbContext(builder.Options);
        }

        private static void LoadEnvFile(string startingDirectory)
        {
            var directory = startingDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                var envPath = Path.Combine(directory, ".env");
                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    return;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }
        }
    }
}
