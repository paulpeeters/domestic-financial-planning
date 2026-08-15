using FinancialPlanningApp.Web.Data.Repositories;
using System.Security.Claims;

namespace FinancialPlanningApp.Web.Services.Auth;

public sealed class HeaderContext
{
    public bool IsAuthenticated { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public long? CurrentTenantId { get; init; }
    public string CurrentTenantDisplay { get; init; } = string.Empty;
    public string? CurrentTenantFullName { get; init; }
    public bool CanManageTenant { get; init; }
    public bool IsGlobalAdmin { get; init; }
    public bool HasMultipleTenants { get; init; }
    public IReadOnlyList<Data.Models.UserTenantMembership> ActiveTenants { get; init; } = [];
}

public interface IHeaderContextService
{
    Task<HeaderContext> GetAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public sealed class HeaderContextService(
    IUserRepository userRepository) : IHeaderContextService
{
    public async Task<HeaderContext> GetAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new HeaderContext { IsAuthenticated = false };
        }

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantIdClaim = principal.FindFirstValue(AuthClaimTypes.TenantId);
        long.TryParse(userIdClaim, out var userId);
        long.TryParse(tenantIdClaim, out var tenantId);

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var memberships = await userRepository.ListTenantMembershipsAsync(userId, cancellationToken);
        var currentMembership = memberships.FirstOrDefault(m => m.TenantId == tenantId);
        var role = await userRepository.GetTenantRoleAsync(userId, tenantId, cancellationToken);
        var currentTenantMember = tenantId > 0
            ? (await userRepository.ListTenantMembersAsync(tenantId, cancellationToken)).FirstOrDefault(m => m.UserId == userId)
            : null;

        var effectiveFirstName = currentTenantMember?.FirstName ?? user?.FirstName;
        var effectiveAvatarUrl = currentTenantMember?.AvatarUrl ?? user?.AvatarUrl;
        var firstName = string.IsNullOrWhiteSpace(effectiveFirstName) ? null : effectiveFirstName.Trim();
        var displayName = firstName
            ?? principal.Identity?.Name?.Split('@').FirstOrDefault()
            ?? "Gebruiker";

        var tenantShort = currentMembership?.TenantShortName;
        var tenantDisplay = !string.IsNullOrWhiteSpace(tenantShort)
            ? tenantShort!
            : (tenantId > 0 ? $"#{tenantId}" : string.Empty);

        return new HeaderContext
        {
            IsAuthenticated = true,
            DisplayName = displayName,
            AvatarUrl = effectiveAvatarUrl,
            CurrentTenantId = tenantId > 0 ? tenantId : null,
            CurrentTenantDisplay = tenantDisplay,
            CurrentTenantFullName = currentMembership?.TenantName,
            CanManageTenant = string.Equals(role, "OWNER", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase),
            IsGlobalAdmin = principal.HasClaim(AuthClaimTypes.GlobalAdmin, "true"),
            HasMultipleTenants = memberships.Count(m => m.IsActive) > 1,
            ActiveTenants = memberships.Where(m => m.IsActive).ToList()
        };
    }
}
