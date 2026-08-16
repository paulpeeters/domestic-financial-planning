namespace FinancialPlanningApp.Web.Data.Models;

public sealed class AppUser
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsGlobalAdmin { get; set; }
    public bool IsActive { get; set; }
    public long? PreferredTenantId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedUtc { get; set; }
}
