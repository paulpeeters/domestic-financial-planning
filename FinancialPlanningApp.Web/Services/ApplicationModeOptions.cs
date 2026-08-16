namespace FinancialPlanningApp.Web.Services;

public sealed class ApplicationModeOptions
{
    public const string SectionName = "Application";

    public string Mode { get; set; } = ApplicationModes.MultiTenant;
    public int PreferredDesktopPort { get; set; } = 5196;
    public bool OpenBrowserOnStart { get; set; } = true;

    public bool IsSingleUserDesktop
        => string.Equals(Mode, ApplicationModes.SingleUserDesktop, StringComparison.OrdinalIgnoreCase);
}

public static class ApplicationModes
{
    public const string MultiTenant = "MultiTenant";
    public const string SingleUserDesktop = "SingleUserDesktop";
}
