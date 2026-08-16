using FinancialPlanningApp.Web.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Services;

public sealed class DesktopDataInfo
{
    public string DatabasePath { get; init; } = string.Empty;
    public string DatabaseDirectory { get; init; } = string.Empty;
    public string BackupDirectory { get; init; } = string.Empty;
    public bool DatabaseExists { get; init; }
}

public interface IDesktopDataService
{
    bool IsAvailable { get; }
    DesktopDataInfo GetInfo();
    Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);
}

public sealed class DesktopDataService(
    IOptions<ApplicationModeOptions> applicationMode,
    IOptions<DatabaseOptions> databaseOptions) : IDesktopDataService
{
    public bool IsAvailable
        => applicationMode.Value.IsSingleUserDesktop
           && ProviderDbConnectionFactory.NormalizeProvider(databaseOptions.Value.Provider) == DatabaseProviders.Sqlite;

    public DesktopDataInfo GetInfo()
    {
        var databasePath = GetDatabasePath();
        var databaseDirectory = Path.GetDirectoryName(databasePath) ?? string.Empty;
        return new DesktopDataInfo
        {
            DatabasePath = databasePath,
            DatabaseDirectory = databaseDirectory,
            BackupDirectory = Path.Combine(databaseDirectory, "Backups"),
            DatabaseExists = File.Exists(databasePath)
        };
    }

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Backups zijn alleen beschikbaar in lokale desktopmodus met SQLite.");
        }

        var info = GetInfo();
        if (!info.DatabaseExists)
        {
            throw new FileNotFoundException("De lokale SQLite database bestaat nog niet.", info.DatabasePath);
        }

        Directory.CreateDirectory(info.BackupDirectory);
        var backupPath = Path.Combine(info.BackupDirectory, $"financial-planning-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = info.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        await source.OpenAsync(cancellationToken);

        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        await destination.OpenAsync(cancellationToken);

        source.BackupDatabase(destination);
        return backupPath;
    }

    private string GetDatabasePath()
    {
        var connectionString = Environment.ExpandEnvironmentVariables(databaseOptions.Value.ConnectionString);
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("De SQLite database heeft geen vast bestandspad.");
        }

        return Path.GetFullPath(builder.DataSource);
    }
}
