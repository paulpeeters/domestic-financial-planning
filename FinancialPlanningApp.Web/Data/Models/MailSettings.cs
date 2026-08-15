namespace FinancialPlanningApp.Web.Data.Models;

public sealed class MailSettings
{
    public long Id { get; init; }
    public long? TenantId { get; init; }
    public string ScopeKey { get; init; } = "global";
    public bool IsEnabled { get; set; }
    public string Provider { get; set; } = "Disabled";
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpUseSsl { get; set; } = true;
    public DateTime UpdatedUtc { get; init; }
}
