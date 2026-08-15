namespace FinancialPlanningApp.Web.Data.Models;

public sealed class TenantInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public bool IsActive { get; set; }
}
