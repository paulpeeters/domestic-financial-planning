using FinancialPlanningApp.Web.Data.Repositories;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface ITenantSessionService
{
    Task<bool> SwitchTenantAsync(HttpContext httpContext, long tenantId, CancellationToken cancellationToken = default);
}

public sealed class TenantSessionService(
    IUserRepository userRepository) : ITenantSessionService
{
    public async Task<bool> SwitchTenantAsync(HttpContext httpContext, long tenantId, CancellationToken cancellationToken = default)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return false;
        }

        var canAccess = await userRepository.HasActiveTenantMembershipAsync(userId, tenantId, cancellationToken);
        if (!canAccess)
        {
            return false;
        }

        var claims = httpContext.User.Claims
            .Where(c => !string.Equals(c.Type, AuthClaimTypes.TenantId, StringComparison.Ordinal))
            .ToList();
        claims.Add(new Claim(AuthClaimTypes.TenantId, tenantId.ToString()));

        var identity = new ClaimsIdentity(claims, "AppCookie");
        var principal = new ClaimsPrincipal(identity);
        await httpContext.SignInAsync("AppCookie", principal);
        await userRepository.SetPreferredTenantAsync(userId, tenantId, cancellationToken);
        return true;
    }
}
