using System.Security.Claims;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface ITenantContextService
{
    long GetCurrentUserId();
    long GetCurrentTenantId();
}

public sealed class TenantContextService(IHttpContextAccessor httpContextAccessor) : ITenantContextService
{
    public long GetCurrentUserId()
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(claim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id is missing.");
        }

        return userId;
    }

    public long GetCurrentTenantId()
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirstValue(AuthClaimTypes.TenantId);
        if (!long.TryParse(claim, out var tenantId))
        {
            throw new InvalidOperationException("Authenticated tenant id is missing.");
        }

        return tenantId;
    }
}

