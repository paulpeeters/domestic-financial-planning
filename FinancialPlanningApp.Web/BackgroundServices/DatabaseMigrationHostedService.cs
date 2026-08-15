using Dapper;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.BackgroundServices;

public sealed class DatabaseMigrationHostedService(
    IDbConnectionFactory connectionFactory,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await EnsureMigrationsTableAsync(connection);

            var applied = (await connection.QueryAsync<string>("SELECT script_name FROM schema_migrations;"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var scripts = LoadMigrationScripts();
            foreach (var script in scripts)
            {
                if (applied.Contains(script.Name))
                {
                    continue;
                }

                using var tx = connection.BeginTransaction();
                await connection.ExecuteAsync(script.Sql, transaction: tx);
                await connection.ExecuteAsync(
                    "INSERT INTO schema_migrations(script_name, applied_utc) VALUES (@name, UTC_TIMESTAMP(6));",
                    new { name = script.Name },
                    tx);
                tx.Commit();

                logger.LogInformation("Applied migration script {MigrationScript}", script.Name);
            }

            logger.LogInformation("Database migrations completed successfully using MySqlConnector.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureMigrationsTableAsync(System.Data.IDbConnection connection)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            script_name VARCHAR(255) NOT NULL PRIMARY KEY,
            applied_utc DATETIME(6) NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """;

        await connection.ExecuteAsync(sql);
    }

    private static IReadOnlyList<(string Name, string Sql)> LoadMigrationScripts()
    {
        var assembly = typeof(DatabaseMigrationHostedService).Assembly;
        const string marker = ".Database.Migrations.";

        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(marker, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
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
