namespace FinancialPlanningApp.Web.Data.Models;

public sealed class UserTenantMembership
{
    public long TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string? TenantShortName { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
