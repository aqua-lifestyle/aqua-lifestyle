using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public static class AqualLifeStyleDbContextConfigurer
    {
        public static void Configure(DbContextOptionsBuilder<AqualLifeStyleDbContext> builder, string connectionString)
        {
            builder.UseNpgsql(connectionString);
        }

        public static void Configure(DbContextOptionsBuilder<AqualLifeStyleDbContext> builder, DbConnection connection)
        {
            builder.UseNpgsql(connection);
        }
    }
}
