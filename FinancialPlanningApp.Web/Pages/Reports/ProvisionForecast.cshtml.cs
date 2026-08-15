using FinancialPlanningApp.Web.Services.Imports;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public class ProvisionForecastModel(
    IReconciliationService reconciliationService,
    IAccountMonthlyBalanceService accountMonthlyBalanceService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public decimal? OpeningBalance { get; set; }

    public decimal ResolvedOpeningBalance { get; private set; }

    public IReadOnlyList<Row> Rows { get; private set; } = [];

    public sealed class Row
    {
        public int Month { get; init; }
        public decimal ExpectedCosts { get; init; }
        public decimal ActualRecurringCosts { get; init; }
        public decimal RecurrentDifference => ActualRecurringCosts - ExpectedCosts;
        public decimal RecurrentDifferencePercent => ExpectedCosts == 0m ? 0m : RecurrentDifference / ExpectedCosts * 100m;
        public decimal YearlyAverageCosts { get; init; }
        public decimal ProvisioningPaidAndForecast { get; init; }
        public decimal EndOfMonthBalance { get; init; }
        public decimal? ActualEndOfMonthBalance { get; init; }
        public decimal? BalanceDifference => ActualEndOfMonthBalance is null
            ? null
            : ActualEndOfMonthBalance.Value - EndOfMonthBalance;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var monthly = (await reconciliationService.GetMonthlyPlannedVsPaidAsync(Year, cancellationToken))
            .Where(x => !x.IsYearTotal)
            .OrderBy(x => x.Month)
            .ToList();

        var totalExpected = monthly.Sum(m => m.PlannedTotal);
        var averageExpected = totalExpected / 12m;
        var actualRecurringByMonth = (await reconciliationService.GetTemplateActualTotalsForYearsAsync(Year, Year, cancellationToken))
            .Where(x => x.Year == Year)
            .GroupBy(x => x.Month)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Abs(x.TotalAmount)));

        var balancesThisYear = await accountMonthlyBalanceService.ListByYearForCurrentUserAsync(Year, cancellationToken);
        var balancesPrevYear = await accountMonthlyBalanceService.ListByYearForCurrentUserAsync(Year - 1, cancellationToken);

        var openingFromJanuary = balancesThisYear.Where(b => b.Month == 1).Sum(b => b.OpeningBalance ?? 0m);
        var openingFromPrevDecember = balancesPrevYear.Where(b => b.Month == 12).Sum(b => b.ClosingBalance);
        var actualClosingBalanceByMonth = balancesThisYear
            .GroupBy(b => b.Month)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ClosingBalance));

        ResolvedOpeningBalance = OpeningBalance
            ?? (openingFromJanuary != 0m ? openingFromJanuary : openingFromPrevDecember);

        var now = DateTime.UtcNow;
        var actualDepositsUpToNow = monthly
            .Where(m => Year < now.Year || (Year == now.Year && m.Month <= now.Month))
            .Select(m => m.DepositedTotal)
            .Where(x => x != 0m)
            .ToList();

        var projectedMonthlyDeposit = actualDepositsUpToNow.Count > 0
            ? actualDepositsUpToNow.Average()
            : 0m;

        var rows = new List<Row>(12);
        var running = ResolvedOpeningBalance;

        foreach (var m in monthly)
        {
            var isFutureMonth = Year > now.Year || (Year == now.Year && m.Month > now.Month);
            var provision = m.DepositedTotal != 0m
                ? m.DepositedTotal
                : (isFutureMonth ? projectedMonthlyDeposit : 0m);

            running += provision - m.PlannedTotal;

            rows.Add(new Row
            {
                Month = m.Month,
                ExpectedCosts = m.PlannedTotal,
                ActualRecurringCosts = actualRecurringByMonth.GetValueOrDefault(m.Month, 0m),
                YearlyAverageCosts = averageExpected,
                ProvisioningPaidAndForecast = provision,
                EndOfMonthBalance = running,
                ActualEndOfMonthBalance = actualClosingBalanceByMonth.TryGetValue(m.Month, out var actualClosingBalance)
                    ? actualClosingBalance
                    : null
            });
        }

        Rows = rows;
    }
}
