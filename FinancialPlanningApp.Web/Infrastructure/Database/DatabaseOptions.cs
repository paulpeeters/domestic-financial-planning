namespace FinancialPlanningApp.Web.Infrastructure.Database;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string Provider { get; set; } = DatabaseProviders.MySql;
    public string ConnectionString { get; set; } = string.Empty;
}

public static class DatabaseProviders
{
    public const string MySql = "MySql";
    public const string Sqlite = "Sqlite";
}
