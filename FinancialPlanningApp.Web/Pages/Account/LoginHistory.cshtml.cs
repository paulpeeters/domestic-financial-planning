using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize(Policy = "RequireGlobalAdmin")]
public class LoginHistoryModel(ILoginAuditService loginAuditService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateTime? FromUtc { get; set; }
    [BindProperty(SupportsGet = true)]
    public DateTime? ToUtc { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }
    [BindProperty(SupportsGet = true)]
    public bool? IsSuccess { get; set; }
    [BindProperty(SupportsGet = true)]
    public int Limit { get; set; } = 200;

    public IReadOnlyList<LoginAttempt> Attempts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Attempts = await loginAuditService.ListAsync(FromUtc, ToUtc, Email, IsSuccess, Limit, cancellationToken);
    }
}
