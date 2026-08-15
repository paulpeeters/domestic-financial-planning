using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Payments;

[Authorize]
public class IndexModel(IRecurringPaymentService recurringPaymentService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool IncludeInactive { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public bool Reorder { get; set; }

    [BindProperty]
    public string OrderedIds { get; set; } = string.Empty;

    public int PageSize { get; } = 10;
    public int TotalPages { get; private set; } = 1;
    public IReadOnlyList<RecurringPaymentTemplate> Items { get; private set; } = [];
    public string? NavKey { get; private set; }
    public IReadOnlyDictionary<long, int> NavigationIndexes { get; private set; } = new Dictionary<long, int>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Reorder)
        {
            Items = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
            IncludeInactive = true;
            PageNumber = 1;
            TotalPages = 1;
            return;
        }

        var result = await recurringPaymentService.ListForCurrentUserAsync(
            new RecurringPaymentFilter(Search, IncludeInactive, PageNumber, PageSize),
            cancellationToken);

        Items = result.Items;
        PageNumber = result.PageNumber;
        TotalPages = result.TotalPages;

        var allItems = await recurringPaymentService.ListAllForCurrentUserAsync(IncludeInactive, cancellationToken);
        var filteredIds = allItems
            .Where(item => string.IsNullOrWhiteSpace(Search) || item.Description.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();

        NavKey = PaymentEditNavigationSession.Store(
            HttpContext.Session,
            new PaymentEditNavigationSnapshot(filteredIds, Search, IncludeInactive, PageSize, DateTime.UtcNow));
        NavigationIndexes = filteredIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
    }

    public async Task<IActionResult> OnPostArchiveAsync(long id, CancellationToken cancellationToken)
    {
        await recurringPaymentService.ArchiveAsync(id, cancellationToken);
        return RedirectToPage(new { Search, IncludeInactive, PageNumber });
    }

    public async Task<IActionResult> OnPostActivateAsync(long id, CancellationToken cancellationToken)
    {
        await recurringPaymentService.ActivateAsync(id, cancellationToken);
        return RedirectToPage(new { Search, IncludeInactive, PageNumber });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        await recurringPaymentService.DeleteAsync(id, cancellationToken);
        return RedirectToPage(new { Search, IncludeInactive, PageNumber });
    }

    public async Task<IActionResult> OnPostSaveOrderAsync(CancellationToken cancellationToken)
    {
        var ids = (OrderedIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => long.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        await recurringPaymentService.UpdateDisplayOrderAsync(ids, cancellationToken);
        return RedirectToPage(new { IncludeInactive = true });
    }

    public static string FormatPaymentSchedule(RecurringPaymentTemplate template)
        => RecurringPaymentSchedule.FormatPaymentSchedule(template);

    public static string FormatPaymentMethod(string paymentMethod)
        => RecurringPaymentSchedule.FormatPaymentMethod(paymentMethod);

    public static bool IsCurrentlyActive(RecurringPaymentTemplate template)
        => RecurringPaymentSchedule.IsCurrentlyActive(template, DateOnly.FromDateTime(DateTime.Today));

    public static bool HasEnded(RecurringPaymentTemplate template)
        => template.ActiveUntil is not null && template.ActiveUntil.Value < DateOnly.FromDateTime(DateTime.Today);

    public static string FormatValidity(RecurringPaymentTemplate template)
    {
        var activeNow = IsCurrentlyActive(template);
        var until = template.ActiveUntil?.ToString("dd/MM/yyyy") ?? "geen einddatum";
        return activeNow
            ? $"Actief sinds {template.ActiveFrom:dd/MM/yyyy}"
            : $"Niet actief ({template.ActiveFrom:dd/MM/yyyy} - {until})";
    }
}
