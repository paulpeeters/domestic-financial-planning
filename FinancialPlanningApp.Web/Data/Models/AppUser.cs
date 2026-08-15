namespace FinancialPlanningApp.Web.Data.Models;

public sealed record AppUser(
    long Id,
    string Email,
    string PasswordHash,
    bool IsGlobalAdmin,
    bool IsActive,
    long? PreferredTenantId,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    DateTime CreatedUtc);
