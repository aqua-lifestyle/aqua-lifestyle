using System;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    internal static class DatabaseUniqueConstraintDetector
    {
        public static bool Matches(DbUpdateException exception, params string[] identifiers)
        {
            if (exception?.InnerException is PostgresException postgres)
            {
                return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                       ContainsIdentifier(postgres.ConstraintName, identifiers);
            }

            if (exception?.InnerException is SqlException sqlServer)
            {
                return (sqlServer.Number == 2601 || sqlServer.Number == 2627) &&
                       ContainsIdentifier(sqlServer.Message, identifiers);
            }

            var inner = exception?.InnerException;
            if (inner?.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException")
            {
                var errorCode = inner.GetType().GetProperty("SqliteErrorCode")?.GetValue(inner);
                return errorCode is int code && code == 19 &&
                       ContainsIdentifier(inner.Message, identifiers);
            }

            return false;
        }

        private static bool ContainsIdentifier(string value, string[] identifiers)
            => !string.IsNullOrWhiteSpace(value) &&
               identifiers.Any(identifier =>
                   !string.IsNullOrWhiteSpace(identifier) &&
                   value.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
