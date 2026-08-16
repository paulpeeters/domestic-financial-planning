namespace FinancialPlanningApp.Web.Services;

public sealed class UpdateCheckOptions
{
    public const string SectionName = "Updates";

    public bool Enabled { get; set; }
    public string LatestReleaseUrl { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = string.Empty;
    public int CacheMinutes { get; set; } = 360;
    public int TimeoutSeconds { get; set; } = 5;
}

public sealed class UpdateCheckResult
{
    public bool IsEnabled { get; init; }
    public bool IsAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? LatestVersion { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ReleaseNotesUrl { get; init; }
    public string? Sha256 { get; init; }
    public DateTime CheckedUtc { get; init; }

    public static UpdateCheckResult Disabled(string currentVersion)
        => new()
        {
            CurrentVersion = currentVersion,
            CheckedUtc = DateTime.UtcNow
        };
}
