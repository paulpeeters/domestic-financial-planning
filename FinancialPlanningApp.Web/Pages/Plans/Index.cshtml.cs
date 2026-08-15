using FinancialPlanningApp.Web.Services.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Plans;

[Authorize]
public class IndexModel(IAnnualPlanningService annualPlanningService) : PageModel
{
    public AnnualPlanSummary Summary { get; private set; } = new(0, 0, 0);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Summary = await annualPlanningService.BuildCurrentYearAsync(cancellationToken);
    }
}
