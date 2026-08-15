using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class SwitchTenantModel(ITenantSessionService tenantSessionService) : PageModel
{
    [BindProperty]
    public long TenantId { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var ok = await tenantSessionService.SwitchTenantAsync(HttpContext, TenantId, cancellationToken);
        if (!ok)
        {
            return Forbid();
        }

        var returnUrl = Request.Query["returnUrl"].ToString();
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
