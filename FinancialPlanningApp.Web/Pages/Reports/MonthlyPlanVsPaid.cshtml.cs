using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public class MonthlyPlanVsPaidModel(IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    public IReadOnlyList<MonthlyPlanPaidRow> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Rows = await reconciliationService.GetMonthlyPlannedVsPaidAsync(Year, cancellationToken);
    }
}
