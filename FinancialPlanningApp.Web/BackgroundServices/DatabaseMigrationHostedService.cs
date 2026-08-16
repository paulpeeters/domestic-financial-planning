using Dapper;
using FinancialPlanningApp.Web.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.BackgroundServices;

public sealed class DatabaseMigrationHostedService(
    IDbConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var provider = ProviderDbConnectionFactory.NormalizeProvider(databaseOptions.Value.Provider);
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await EnsureMigrationsTableAsync(connection, provider);

            var applied = (await connection.QueryAsync<string>("SELECT script_name FROM schema_migrations;"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var scripts = LoadMigrationScripts(provider);
            foreach (var script in scripts)
            {
                if (applied.Contains(script.Name))
                {
                    continue;
                }

                using var tx = connection.BeginTransaction();
                await connection.ExecuteAsync(script.Sql, transaction: tx);
                await connection.ExecuteAsync(
                    GetInsertMigrationSql(provider),
                    new { name = script.Name },
                    tx);
                tx.Commit();

                logger.LogInformation("Applied migration script {MigrationScript}", script.Name);
            }

            logger.LogInformation("Database migrations completed successfully using {DatabaseProvider}.", provider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Task EnsureMigrationsTableAsync(System.Data.IDbConnection connection, string provider)
        => connection.ExecuteAsync(provider == DatabaseProviders.Sqlite
            ? """
              CREATE TABLE IF NOT EXISTS schema_migrations (
                  script_name TEXT NOT NULL PRIMARY KEY,
                  applied_utc TEXT NOT NULL
              );
              """
            : """
              CREATE TABLE IF NOT EXISTS schema_migrations (
                  script_name VARCHAR(255) NOT NULL PRIMARY KEY,
                  applied_utc DATETIME(6) NOT NULL
              ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
              """);

    private static string GetInsertMigrationSql(string provider)
        => provider == DatabaseProviders.Sqlite
            ? "INSERT INTO schema_migrations(script_name, applied_utc) VALUES (@name, STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'));"
            : "INSERT INTO schema_migrations(script_name, applied_utc) VALUES (@name, UTC_TIMESTAMP(6));";

    private static IReadOnlyList<(string Name, string Sql)> LoadMigrationScripts(string provider)
    {
        var assembly = typeof(DatabaseMigrationHostedService).Assembly;
        var marker = provider == DatabaseProviders.Sqlite
            ? ".Database.Migrations.Sqlite."
            : ".Database.Migrations.";

        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(marker, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Where(n => provider == DatabaseProviders.Sqlite || !n.Contains(".Database.Migrations.Sqlite.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Unable to load migration resource {name}");
                using var reader = new StreamReader(stream);
                var sql = reader.ReadToEnd();
                var shortName = name[(name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length)..];
                return (Name: shortName, Sql: sql);
            })
            .ToList();

        return scripts;
    }
}
