using System.Globalization;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public class PaymentsByItemModel(
    IRecurringPaymentService recurringPaymentService,
    IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int FromYear { get; set; } = DateTime.UtcNow.Year - 3;

    [BindProperty(SupportsGet = true)]
    public int ToYear { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int StartMonth { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public List<long> TemplateIds { get; set; } = [];

    public IReadOnlyList<TemplateOption> TemplateOptions { get; private set; } = [];
    public IReadOnlyList<YearColumn> Years { get; private set; } = [];
    public IReadOnlyList<MonthRow> Months { get; private set; } = [];
    public IReadOnlyList<decimal> YearTotals { get; private set; } = [];
    public string? ValidationMessage { get; private set; }

    public sealed record TemplateOption(long Id, string Description, string Validity, bool IsActive);
    public sealed record YearColumn(int StartYear, string Label);
    public sealed record ItemAmount(string Description, decimal Amount);
    public sealed record MonthCell(decimal Total, IReadOnlyList<ItemAmount> Items);
    public sealed record MonthRow(string Label, IReadOnlyList<MonthCell> Cells);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        TemplateOptions = templates
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Description)
            .ThenBy(x => x.ActiveFrom)
            .Select(x => new TemplateOption(
                x.Id,
                x.Description,
                FormatValidity(x.ActiveFrom, x.ActiveUntil),
                x.IsActive))
            .ToList();

        StartMonth = Math.Clamp(StartMonth, 1, 12);
        FromYear = Math.Clamp(FromYear, 2000, 2100);
        ToYear = Math.Clamp(ToYear, 2000, 2100);

        if (ToYear < FromYear)
        {
            ValidationMessage = "Het eindjaar moet gelijk zijn aan of na het beginjaar.";
            return;
        }

        var availableIds = TemplateOptions.Select(x => x.Id).ToHashSet();
        TemplateIds = TemplateIds.Distinct().Where(availableIds.Contains).ToList();
        if (TemplateIds.Count == 0)
        {
            return;
        }

        Years = Enumerable.Range(FromYear, ToYear - FromYear + 1)
            .Select(year => new YearColumn(year, FormatReportYear(year, StartMonth)))
            .ToList();

        var actualToYear = ToYear + (StartMonth == 1 ? 0 : 1);
        var actuals = await reconciliationService.GetTemplateActualTotalsForYearsAsync(
            FromYear,
            actualToYear,
            cancellationToken);

        var selectedTemplates = TemplateOptions
            .Where(x => TemplateIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var amounts = actuals
            .Where(x => selectedTemplates.ContainsKey(x.TemplateId))
            .ToDictionary(
                x => (x.TemplateId, x.Year, x.Month),
                x => Math.Abs(x.TotalAmount));

        var monthRows = new List<MonthRow>(12);
        for (var offset = 0; offset < 12; offset++)
        {
            var month = ((StartMonth - 1 + offset) % 12) + 1;
            var cells = new List<MonthCell>(Years.Count);
            foreach (var year in Years)
            {
                var calendarYear = year.StartYear + (month < StartMonth ? 1 : 0);
                var items = selectedTemplates.Values
                    .Select(template => new ItemAmount(
                        template.Description,
                        amounts.GetValueOrDefault((template.Id, calendarYear, month))))
                    .Where(x => x.Amount != 0m)
                    .OrderBy(x => x.Description)
                    .ToList();
                cells.Add(new MonthCell(items.Sum(x => x.Amount), items));
            }

            monthRows.Add(new MonthRow(GetMonthName(month), cells));
        }

        Months = monthRows;
        YearTotals = Enumerable.Range(0, Years.Count)
            .Select(index => Months.Sum(row => row.Cells[index].Total))
            .ToList();
    }

    private static string FormatReportYear(int year, int startMonth)
        => startMonth == 1 ? year.ToString(CultureInfo.InvariantCulture) : $"{year}/{year + 1}";

    private static string FormatValidity(DateOnly from, DateOnly? until)
        => until is null
            ? $"vanaf {from:dd/MM/yyyy}"
            : $"{from:dd/MM/yyyy} – {until:dd/MM/yyyy}";

    private static string GetMonthName(int month)
    {
        var name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
    }
}
