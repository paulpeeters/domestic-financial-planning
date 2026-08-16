using Microsoft.Extensions.Options;
using System.Data;

namespace FinancialPlanningApp.Web.Infrastructure.Database;

public sealed class ProviderDbConnectionFactory(
    IOptions<DatabaseOptions> options,
    MySqlDbConnectionFactory mySqlFactory,
    SqliteDbConnectionFactory sqliteFactory) : IDbConnectionFactory
{
    public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        => NormalizeProvider(options.Value.Provider) switch
        {
            DatabaseProviders.MySql => mySqlFactory.CreateOpenConnectionAsync(cancellationToken),
            DatabaseProviders.Sqlite => sqliteFactory.CreateOpenConnectionAsync(cancellationToken),
            var provider => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };

    public static string NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)
            || string.Equals(provider, "MariaDb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "MariaDB", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProviders.MySql;
        }

        if (string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProviders.Sqlite;
        }

        return provider.Trim();
    }
}
