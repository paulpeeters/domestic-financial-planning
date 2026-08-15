using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public class RecurringPaymentProvisionModel(
    IRecurringPaymentService recurringPaymentService,
    IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public decimal ThresholdPercent { get; set; } = 2m;

    [BindProperty(SupportsGet = true)]
    public string EvaluationMode { get; set; } = ProjectedYearEndMode;

    public const string ProjectedYearEndMode = "ProjectedYearEnd";
    public const string YearToDateMode = "YearToDate";

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public TotalsRow Totals { get; private set; } = new();
    public string EvaluationLabel => IsYearToDateMode ? "Year-to-date pro-rata" : "Prognose jaareinde";

    private bool IsYearToDateMode => string.Equals(EvaluationMode, YearToDateMode, StringComparison.OrdinalIgnoreCase);

    public sealed class Row
    {
        public string Description { get; init; } = string.Empty;
        public string Schedule { get; init; } = string.Empty;
        public decimal[] PaidByMonth { get; init; } = new decimal[12];
        public bool[] ExpectedByMonth { get; init; } = new bool[12];
        public decimal PlannedYearly { get; init; }
        public decimal PlannedToDate { get; init; }
        public decimal PaidToDate { get; init; }
        public decimal FutureExpected { get; init; }
        public decimal EvaluatedPlanned { get; init; }
        public decimal EvaluatedExpense { get; init; }
        public decimal ProjectedYearly => PaidToDate + FutureExpected;
        public decimal EvaluationDifference => EvaluatedExpense - EvaluatedPlanned;
        public decimal EvaluationDifferencePercent => EvaluatedPlanned == 0m ? 0m : EvaluationDifference / EvaluatedPlanned * 100m;
        public ProvisionStatus Status { get; init; }
    }

    public sealed class TotalsRow
    {
        public decimal[] PaidByMonth { get; init; } = new decimal[12];
        public decimal PlannedYearly { get; init; }
        public decimal PlannedToDate { get; init; }
        public decimal PaidToDate { get; init; }
        public decimal FutureExpected { get; init; }
        public decimal EvaluatedPlanned { get; init; }
        public decimal EvaluatedExpense { get; init; }
        public decimal ProjectedYearly => PaidToDate + FutureExpected;
        public decimal EvaluationDifference => EvaluatedExpense - EvaluatedPlanned;
        public decimal EvaluationDifferencePercent => EvaluatedPlanned == 0m ? 0m : EvaluationDifference / EvaluatedPlanned * 100m;
    }

    public enum ProvisionStatus
    {
        InRange,
        UnderPlan,
        OverPlan
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (ThresholdPercent < 0m)
        {
            ThresholdPercent = 0m;
        }

        if (!IsYearToDateMode)
        {
            EvaluationMode = ProjectedYearEndMode;
        }

        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(false, cancellationToken);
        var actuals = await reconciliationService.GetTemplateActualTotalsForYearsAsync(Year, Year, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = GetCutoffDate(Year, today);
        var thresholdRatio = ThresholdPercent / 100m;

        var rows = new List<Row>();
        foreach (var template in templates.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Description))
        {
            var occurrences = BuildOccurrences(template, Year);
            if (occurrences.Count == 0)
            {
                continue;
            }
            var expectedByMonth = new bool[12];
            foreach (var occurrenceMonth in occurrences.Select(x => x.DueDate.Month).Distinct())
            {
                expectedByMonth[occurrenceMonth - 1] = true;
            }

            var actualsForTemplate = actuals
                .Where(a => a.TemplateId == template.Id)
                .ToList();

            var paidByMonth = new decimal[12];
            foreach (var actual in actualsForTemplate)
            {
                if (actual.Month is >= 1 and <= 12)
                {
                    paidByMonth[actual.Month - 1] += Math.Abs(actual.TotalAmount);
                }
            }

            var plannedYearly = occurrences.Sum(x => x.Amount);
            var plannedToDate = occurrences
                .Where(x => x.DueDate <= cutoff)
                .Sum(x => x.Amount);
            var paidToDate = Enumerable.Range(1, 12)
                .Where(month => new DateOnly(Year, month, 1) <= new DateOnly(cutoff.Year, cutoff.Month, 1))
                .Sum(month => paidByMonth[month - 1]);
            var futureExpected = occurrences
                .Where(x => x.DueDate > today)
                .Sum(x => x.Amount);

            var projectedYearly = paidToDate + futureExpected;
            var evaluatedPlanned = IsYearToDateMode ? plannedToDate : plannedYearly;
            var evaluatedExpense = IsYearToDateMode ? paidToDate : projectedYearly;
            var lowerBound = evaluatedPlanned * (1m - thresholdRatio);
            var upperBound = evaluatedPlanned * (1m + thresholdRatio);
            var status = evaluatedExpense < lowerBound
                ? ProvisionStatus.UnderPlan
                : evaluatedExpense > upperBound
                    ? ProvisionStatus.OverPlan
                    : ProvisionStatus.InRange;

            rows.Add(new Row
            {
                Description = template.Description,
                Schedule = RecurringPaymentSchedule.FormatCompactSchedule(template),
                PaidByMonth = paidByMonth,
                ExpectedByMonth = expectedByMonth,
                PlannedYearly = plannedYearly,
                PlannedToDate = plannedToDate,
                PaidToDate = paidToDate,
                FutureExpected = futureExpected,
                EvaluatedPlanned = evaluatedPlanned,
                EvaluatedExpense = evaluatedExpense,
                Status = status
            });
        }

        Rows = rows;
        Totals = new TotalsRow
        {
            PaidByMonth = Enumerable.Range(0, 12).Select(i => rows.Sum(r => r.PaidByMonth[i])).ToArray(),
            PlannedYearly = rows.Sum(r => r.PlannedYearly),
            PlannedToDate = rows.Sum(r => r.PlannedToDate),
            PaidToDate = rows.Sum(r => r.PaidToDate),
            FutureExpected = rows.Sum(r => r.FutureExpected),
            EvaluatedPlanned = rows.Sum(r => r.EvaluatedPlanned),
            EvaluatedExpense = rows.Sum(r => r.EvaluatedExpense)
        };
    }

    private sealed record Occurrence(DateOnly DueDate, decimal Amount);

    private static DateOnly GetCutoffDate(int year, DateOnly today)
    {
        if (year < today.Year)
        {
            return new DateOnly(year, 12, 31);
        }

        if (year > today.Year)
        {
            return new DateOnly(year, 1, 1).AddDays(-1);
        }

        return today;
    }

    private static IReadOnlyList<Occurrence> BuildOccurrences(RecurringPaymentTemplate template, int year)
    {
        var list = new List<Occurrence>();
        for (var month = 1; month <= 12; month++)
        {
            if (!OccursInMonth(template, month))
            {
                continue;
            }

            var day = template.PaymentDay is > 0 and <= 28 ? template.PaymentDay.Value : 1;
            var due = new DateOnly(year, month, day);
            if (due < template.ActiveFrom || (template.ActiveUntil is not null && due > template.ActiveUntil.Value))
            {
                continue;
            }

            list.Add(new Occurrence(due, RecurringPaymentSchedule.GetOccurrenceAmount(template, month)));
        }

        return list;
    }

    private static bool OccursInMonth(RecurringPaymentTemplate template, int month)
        => RecurringPaymentSchedule.GetPaymentMonths(template).Contains(month);
}
