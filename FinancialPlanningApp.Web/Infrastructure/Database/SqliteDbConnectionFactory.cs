using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Data;

namespace FinancialPlanningApp.Web.Infrastructure.Database;

public sealed class SqliteDbConnectionFactory(IOptions<DatabaseOptions> options)
{
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = ExpandConnectionString(options.Value.ConnectionString);
        var builder = new SqliteConnectionStringBuilder(connectionString);
        EnsureDatabaseDirectory(builder.DataSource);

        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static string ExpandConnectionString(string connectionString)
        => Environment.ExpandEnvironmentVariables(connectionString);

    private static void EnsureDatabaseDirectory(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource)
            || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fullPath = Path.GetFullPath(dataSource);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
