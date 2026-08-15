using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class TenantsModel(ITenantMembershipService membershipService, Data.Repositories.IUserRepository userRepository, ITenantContextService tenantContextService) : PageModel
{
    public sealed class TenantRow
    {
        public long TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? TenantShortName { get; set; }
        public string TenantSlug { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public IReadOnlyList<TenantRow> Tenants { get; private set; } = [];
    public long? CurrentTenantId { get; private set; }

    [BindProperty]
    [Required]
    public long SelectedTenantId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSwitchAsync(CancellationToken cancellationToken)
    {
        var canAccess = await membershipService.CanAccessTenantAsync(SelectedTenantId, cancellationToken);
        if (!canAccess)
        {
            ModelState.AddModelError(string.Empty, "Je hebt geen toegang tot de geselecteerde tenant.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var claims = HttpContext.User.Claims
            .Where(c => !string.Equals(c.Type, AuthClaimTypes.TenantId, StringComparison.Ordinal))
            .ToList();
        claims.Add(new Claim(AuthClaimTypes.TenantId, SelectedTenantId.ToString()));

        var identity = new ClaimsIdentity(claims, "AppCookie");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("AppCookie", principal);
        await userRepository.SetPreferredTenantAsync(tenantContextService.GetCurrentUserId(), SelectedTenantId, cancellationToken);

        return RedirectToPage("/Index");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var tenantIdClaim = HttpContext.User.FindFirstValue(AuthClaimTypes.TenantId);
        if (long.TryParse(tenantIdClaim, out var currentTenantId))
        {
            CurrentTenantId = currentTenantId;
            SelectedTenantId = currentTenantId;
        }

        var memberships = await membershipService.ListForCurrentUserAsync(cancellationToken);
        Tenants = memberships
            .Select(m => new TenantRow
            {
                TenantId = m.TenantId,
                TenantName = m.TenantName,
                TenantShortName = m.TenantShortName,
                TenantSlug = m.TenantSlug,
                Role = m.Role,
                IsActive = m.IsActive
            })
            .ToList();
    }
}
