using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Reconciliation;

[Authorize]
public class MissingModel(IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty]
    public MapInput Input { get; set; } = new();

    public IReadOnlyList<MissingPaymentSuggestion> Suggestions { get; private set; } = [];

    public sealed class MapInput
    {
        public long ExecutionId { get; set; }
        public long TemplateId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        [MaxLength(256)]
        public string? Note { get; set; }
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Suggestions = await reconciliationService.GetMissingSuggestionsAsync(Year, cancellationToken);
    }

    public async Task<IActionResult> OnPostMapAsync(CancellationToken cancellationToken)
    {
        await reconciliationService.MapExecutionAsync(Input.ExecutionId, Input.TemplateId, Input.Year, Input.Month, Input.Note, cancellationToken);
        return RedirectToPage(new { Year = Input.Year });
    }
}
