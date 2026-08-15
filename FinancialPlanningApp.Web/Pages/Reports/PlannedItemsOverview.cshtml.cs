using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public class PlannedItemsOverviewModel(
    IRecurringPaymentService recurringPaymentService,
    IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public TotalsRow Totals { get; private set; } = new();

    public sealed class Row
    {
        public long TemplateId { get; init; }
        public string Category { get; init; } = string.Empty;
        public string PlannedSchedule { get; init; } = string.Empty;
        public decimal PlannedMonthly { get; init; }
        public decimal ActualMonthly { get; init; }
        public decimal PlannedYearly { get; init; }
        public decimal ActualYearly { get; init; }
        public decimal ActualPreviousYear { get; init; }
        public decimal Difference => PlannedYearly - Math.Abs(ActualYearly);
    }

    public sealed class TotalsRow
    {
        public decimal PlannedMonthly { get; init; }
        public decimal ActualMonthly { get; init; }
        public decimal PlannedYearly { get; init; }
        public decimal ActualYearly { get; init; }
        public decimal ActualPreviousYear { get; init; }
        public decimal Difference => PlannedYearly - Math.Abs(ActualYearly);
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        var actuals = await reconciliationService.GetTemplateActualTotalsForYearsAsync(Year - 1, Year, cancellationToken);

        var rows = new List<Row>();
        foreach (var t in templates.OrderBy(x => x.Description))
        {
            var plannedYearly = CalculatePlannedYearlyForYear(t, Year);
            if (plannedYearly == 0m)
            {
                continue;
            }

            var currentYearActual = actuals
                .Where(a => a.TemplateId == t.Id && a.Year == Year)
                .Sum(a => a.TotalAmount);
            var previousYearActual = actuals
                .Where(a => a.TemplateId == t.Id && a.Year == Year - 1)
                .Sum(a => a.TotalAmount);

            var plannedMonthly = plannedYearly / 12m;
            var actualMonthly = currentYearActual / 12m;

            rows.Add(new Row
            {
                TemplateId = t.Id,
                Category = t.Description,
                PlannedSchedule = RecurringPaymentSchedule.FormatDetailedSchedule(t),
                PlannedMonthly = plannedMonthly,
                ActualMonthly = actualMonthly,
                PlannedYearly = plannedYearly,
                ActualYearly = currentYearActual,
                ActualPreviousYear = previousYearActual
            });
        }

        Rows = rows;
        Totals = new TotalsRow
        {
            PlannedMonthly = rows.Sum(r => r.PlannedMonthly),
            ActualMonthly = rows.Sum(r => r.ActualMonthly),
            PlannedYearly = rows.Sum(r => r.PlannedYearly),
            ActualYearly = rows.Sum(r => r.ActualYearly),
            ActualPreviousYear = rows.Sum(r => r.ActualPreviousYear)
        };
    }

    private static decimal CalculatePlannedYearlyForYear(RecurringPaymentTemplate template, int year)
    {
        decimal total = 0m;
        for (var month = 1; month <= 12; month++)
        {
            if (!OccursInMonth(template, month))
            {
                continue;
            }

            var day = template.PaymentDay is > 0 and <= 28 ? template.PaymentDay.Value : 1;
            if (!RecurringPaymentSchedule.IsValidForMonth(template, year, month))
            {
                continue;
            }

            total += RecurringPaymentSchedule.GetOccurrenceAmount(template, month);
        }

        return total;
    }

    private static bool OccursInMonth(RecurringPaymentTemplate t, int month)
        => RecurringPaymentSchedule.GetPaymentMonths(t).Contains(month);
}
