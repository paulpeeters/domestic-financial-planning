using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public sealed class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
