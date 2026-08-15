using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace FinancialPlanningApp.Web.Pages.Reconciliation;

[Authorize]
public class IndexModel(IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int? Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OnlyUnmapped { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty]
    public long ExecutionId { get; set; }

    [BindProperty]
    public string OptionValue { get; set; } = string.Empty;

    public IReadOnlyList<ReconciliationReviewRow> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Rows = await reconciliationService.GetReviewRowsAsync(Year, Month, OnlyUnmapped, Search, cancellationToken);
    }

    public async Task<IActionResult> OnPostMapAsync(
        [FromForm] long executionId,
        [FromForm] string optionValue,
        [FromForm] int mapYear,
        [FromForm] int mapMonth,
        CancellationToken cancellationToken)
    {
        await reconciliationService.ApplyMappingOptionAsync(executionId, optionValue, mapYear, mapMonth, cancellationToken);
        return RedirectToPage(routeValues: BuildFilterRouteValues());
    }

    public async Task<IActionResult> OnPostUnmapAsync(
        [FromForm] long executionId,
        [FromForm] int mapYear,
        [FromForm] int mapMonth,
        CancellationToken cancellationToken)
    {
        await reconciliationService.ApplyMappingOptionAsync(executionId, "UNMAP", mapYear, mapMonth, cancellationToken);
        return RedirectToPage(routeValues: BuildFilterRouteValues());
    }

    private RouteValueDictionary BuildFilterRouteValues()
    {
        var routeValues = new RouteValueDictionary
        {
            ["Year"] = Year,
            ["OnlyUnmapped"] = OnlyUnmapped ? "true" : "false"
        };

        if (Month.HasValue)
        {
            routeValues["Month"] = Month.Value;
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            routeValues["Search"] = Search;
        }

        return routeValues;
    }
}
