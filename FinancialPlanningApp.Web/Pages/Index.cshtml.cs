using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Auth;
using FinancialPlanningApp.Web.Services.Imports;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages;

public class IndexModel(
    IReconciliationService reconciliationService,
    IAccountMonthlyBalanceService accountMonthlyBalanceService,
    IRecurringPaymentService recurringPaymentService,
    IApplicationSettingsService applicationSettingsService,
    IDesktopBootstrapService desktopBootstrapService) : PageModel
{
    public DashboardSummary Dashboard { get; private set; } = new();
    public IReadOnlyList<DashboardMonthRow> Months { get; private set; } = [];
    public IReadOnlyList<DashboardMonthRow> FocusMonths { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (await desktopBootstrapService.IsSetupRequiredAsync(cancellationToken))
        {
            return RedirectToPage("/Account/DesktopSetup");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var today = DateTime.Today;
            var year = today.Year;
            var currentMonth = today.Month;
            var currentYearRows = await reconciliationService.GetMonthlyPlannedVsPaidAsync(year, cancellationToken);
            var previousYearRows = currentMonth <= 2
                ? await reconciliationService.GetMonthlyPlannedVsPaidAsync(year - 1, cancellationToken)
                : [];
            var monthly = currentYearRows
                .Where(x => !x.IsYearTotal)
                .OrderBy(x => x.Month)
                .ToList();

            var balances = await accountMonthlyBalanceService.ListByYearForCurrentTenantAsync(year, cancellationToken);
            var latestBalanceGroup = balances
                .Where(b => b.Month <= currentMonth)
                .GroupBy(b => b.Month)
                .OrderByDescending(g => g.Key)
                .FirstOrDefault();

            var expectedYearlyCost = monthly.Sum(m => m.PlannedTotal);
            var fixedMonthlyProvision = Math.Ceiling(expectedYearlyCost / 12m);
            var expectedProvisionYtd = fixedMonthlyProvision * currentMonth;
            var remainingProvisionExpected = fixedMonthlyProvision * (12 - currentMonth);
            var actualProvisionedYtd = monthly.Where(m => m.Month <= currentMonth).Sum(m => m.DepositedTotal);
            var expectedCostsYtd = monthly.Where(m => m.Month <= currentMonth).Sum(m => m.PlannedTotal);
            var actualPaidCostsYtd = monthly.Where(m => m.Month <= currentMonth).Sum(m => Math.Abs(m.MappedToMonthTotal));

            var current = monthly.FirstOrDefault(m => m.Month == currentMonth);
            var currentMonthExpectedCosts = current?.PlannedTotal ?? 0m;
            var currentMonthPaidCosts = current is null ? 0m : Math.Abs(current.MappedToMonthTotal);
            var configuredProvisionAmount = await applicationSettingsService.GetMonthlyProvisionAmountAsync(cancellationToken);
            var provisionDay = await applicationSettingsService.GetMonthlyProvisionDayAsync(cancellationToken);
            if (configuredProvisionAmount is > 0m)
            {
                fixedMonthlyProvision = configuredProvisionAmount.Value;
                expectedProvisionYtd = fixedMonthlyProvision * currentMonth;
                remainingProvisionExpected = fixedMonthlyProvision * (12 - currentMonth);
            }

            var pendingByMonth = await BuildPendingByMonthAsync(year, today, fixedMonthlyProvision, provisionDay, monthly, cancellationToken);
            pendingByMonth.TryGetValue(currentMonth, out var currentPending);
            currentPending ??= new PendingMonthAmounts();
            var currentMonthRemainingCosts = Math.Max(0m, currentMonthExpectedCosts - currentMonthPaidCosts);
            var futureExpectedCosts = monthly.Where(m => m.Month > currentMonth).Sum(m => m.PlannedTotal);
            var remainingCostsForYear = currentMonthRemainingCosts + futureExpectedCosts;
            var currentAccountBalance = latestBalanceGroup?.Sum(x => x.ClosingBalance);

            Dashboard = new DashboardSummary
            {
                Year = year,
                CurrentMonth = currentMonth,
                ExpectedYearlyCost = expectedYearlyCost,
                FixedMonthlyProvision = fixedMonthlyProvision,
                ExpectedProvisionYtd = expectedProvisionYtd,
                ActualProvisionedYtd = actualProvisionedYtd,
                RemainingProvisionExpected = remainingProvisionExpected,
                ExpectedCostsYtd = expectedCostsYtd,
                ActualPaidCostsYtd = actualPaidCostsYtd,
                PresumedPaidCostsYtd = pendingByMonth.Where(x => x.Key <= currentMonth).Sum(x => x.Value.PresumedPaidCosts),
                PresumedProvisionedYtd = pendingByMonth.Where(x => x.Key <= currentMonth).Sum(x => x.Value.PresumedProvisioned),
                CurrentMonthExpectedCosts = currentMonthExpectedCosts,
                CurrentMonthPaidCosts = currentMonthPaidCosts,
                CurrentMonthPresumedPaidCosts = currentPending.PresumedPaidCosts,
                CurrentMonthPresumedProvisioned = currentPending.PresumedProvisioned,
                CurrentMonthRemainingCosts = currentMonthRemainingCosts,
                RemainingCostsForYear = remainingCostsForYear,
                CurrentAccountBalance = currentAccountBalance,
                CurrentAccountBalanceMonth = latestBalanceGroup?.Key,
                ProjectedYearEndBalance = currentAccountBalance is null
                    ? null
                    : currentAccountBalance.Value + remainingProvisionExpected - remainingCostsForYear
            };

            Months = monthly.Select(m =>
            {
                var pending = pendingByMonth.TryGetValue(m.Month, out var value) ? value : new PendingMonthAmounts();
                return new DashboardMonthRow
                {
                    Year = year,
                    Month = m.Month,
                    ExpectedCosts = m.PlannedTotal,
                    PaidCosts = Math.Abs(m.MappedToMonthTotal),
                    Provisioned = m.DepositedTotal,
                    PresumedPaidCosts = pending.PresumedPaidCosts,
                    PresumedProvisioned = pending.PresumedProvisioned,
                    PendingItems = pending.Items.OrderBy(i => i.DueDate).ThenBy(i => i.Description).ToList(),
                    BalanceAfterMappedItems = m.ProvisionedBalanceEndOfMonth
                };
            }).ToList();

            var rowsByYearMonth = currentYearRows
                .Concat(previousYearRows)
                .Where(x => !x.IsYearTotal)
                .ToDictionary(x => (x.Year, x.Month));

            FocusMonths = Enumerable.Range(0, 3)
                .Select(offset => new
                {
                    Offset = offset,
                    Period = new DateOnly(year, currentMonth, 1).AddMonths(-offset)
                })
                .Select(item =>
                {
                    rowsByYearMonth.TryGetValue((item.Period.Year, item.Period.Month), out var row);
                    var expected = row?.PlannedTotal ?? 0m;
                    var paid = row is null ? 0m : Math.Abs(row.MappedToMonthTotal);
                    var pending = item.Period.Year == year
                        ? (pendingByMonth.TryGetValue(item.Period.Month, out var value) ? value : new PendingMonthAmounts())
                        : new PendingMonthAmounts();
                    return new DashboardMonthRow
                    {
                        Year = item.Period.Year,
                        Month = item.Period.Month,
                        ExpectedCosts = expected,
                        PaidCosts = paid,
                        Provisioned = row?.DepositedTotal ?? 0m,
                        PresumedPaidCosts = pending.PresumedPaidCosts,
                        PresumedProvisioned = pending.PresumedProvisioned,
                        PendingItems = pending.Items.OrderBy(i => i.DueDate).ThenBy(i => i.Description).ToList(),
                        BalanceAfterMappedItems = row?.ProvisionedBalanceEndOfMonth ?? 0m,
                        Label = item.Offset switch
                        {
                            0 => "Huidige maand",
                            1 => "Vorige maand",
                            _ => "Twee maanden geleden"
                        }
                    };
                })
                .ToList();
        }

        return Page();
    }

    private async Task<Dictionary<int, PendingMonthAmounts>> BuildPendingByMonthAsync(
        int year,
        DateTime todayDateTime,
        decimal fixedMonthlyProvision,
        int provisionDay,
        IReadOnlyList<MonthlyPlanPaidRow> monthly,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(todayDateTime);
        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        var actuals = await reconciliationService.GetTemplateActualTotalsForYearsAsync(year, year, cancellationToken);
        var actualByTemplate = actuals
            .Where(a => a.Year == year)
            .GroupBy(a => (a.TemplateId, a.Month))
            .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(x => x.TotalAmount)));
        var result = Enumerable.Range(1, 12).ToDictionary(m => m, _ => new PendingMonthAmounts());

        foreach (var template in templates)
        {
            foreach (var month in RecurringPaymentSchedule.GetPaymentMonths(template))
            {
                if (!RecurringPaymentSchedule.IsValidForMonth(template, year, month))
                {
                    continue;
                }

                var dueDate = BuildDueDate(template, year, month);
                if (dueDate > today)
                {
                    continue;
                }

                if (actualByTemplate.ContainsKey((template.Id, month)))
                {
                    continue;
                }

                if (dueDate.Year != year)
                {
                    continue;
                }

                var displayMonth = dueDate.Month;
                if (!IsLikelyAutomatic(template.PaymentMethod))
                {
                    result[displayMonth].Items.Add(new DashboardPendingItem
                    {
                        Description = template.Description,
                        Amount = RecurringPaymentSchedule.GetOccurrenceAmount(template, month),
                        DueDate = dueDate,
                        Kind = "Niet meegeteld",
                        Detail = $"Betaalwijze: {FormatPaymentMethod(template.PaymentMethod)}; rapporteringsmaand {year}-{month:00}"
                    });
                    continue;
                }

                result[displayMonth].PresumedPaidCosts += RecurringPaymentSchedule.GetOccurrenceAmount(template, month);
                result[displayMonth].Items.Add(new DashboardPendingItem
                {
                    Description = template.Description,
                    Amount = RecurringPaymentSchedule.GetOccurrenceAmount(template, month),
                    DueDate = dueDate,
                    Kind = "Kost",
                    Detail = $"{FormatPaymentMethod(template.PaymentMethod)}; rapporteringsmaand {year}-{month:00}"
                });
            }
        }

        foreach (var row in monthly.Where(r => !r.IsYearTotal))
        {
            var provisionDate = new DateOnly(row.Year, row.Month, Math.Clamp(provisionDay, 1, 28));
            if (provisionDate <= today)
            {
                result[row.Month].PresumedProvisioned = Math.Max(0m, fixedMonthlyProvision - row.DepositedTotal);
                if (result[row.Month].PresumedProvisioned > 0m)
                {
                    result[row.Month].Items.Add(new DashboardPendingItem
                    {
                        Description = "Maandelijkse provisie",
                        Amount = result[row.Month].PresumedProvisioned,
                        DueDate = provisionDate,
                        Kind = "Provisie"
                    });
                }
            }
        }

        return result;
    }

    private static DateOnly BuildDueDate(RecurringPaymentTemplate template, int year, int month)
    {
        var period = new DateOnly(year, month, 1);
        var dueBase = period.AddMonths(Math.Max(0, template.PaymentLagMonths));
        var day = template.PaymentDay is > 0 and <= 28 ? template.PaymentDay.Value : 1;
        return new DateOnly(dueBase.Year, dueBase.Month, day);
    }

    private static bool IsLikelyAutomatic(string paymentMethod)
        => string.Equals(paymentMethod, "DirectDebit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod, "CreditCard", StringComparison.OrdinalIgnoreCase);

    private static string FormatPaymentMethod(string paymentMethod)
        => paymentMethod switch
        {
            "DirectDebit" => "Domiciliering",
            "CreditCard" => "Kredietkaart",
            "Transfer" => "Overschrijving",
            _ => paymentMethod
        };

    public sealed class DashboardSummary
    {
        public int Year { get; init; }
        public int CurrentMonth { get; init; }
        public decimal ExpectedYearlyCost { get; init; }
        public decimal FixedMonthlyProvision { get; init; }
        public decimal ExpectedProvisionYtd { get; init; }
        public decimal ActualProvisionedYtd { get; init; }
        public decimal ProvisionGap => ActualProvisionedYtd - ExpectedProvisionYtd;
        public decimal RemainingProvisionExpected { get; init; }
        public decimal ExpectedCostsYtd { get; init; }
        public decimal ActualPaidCostsYtd { get; init; }
        public decimal PresumedPaidCostsYtd { get; init; }
        public decimal PresumedProvisionedYtd { get; init; }
        public decimal CostGapYtd => ActualPaidCostsYtd - ExpectedCostsYtd;
        public decimal CurrentMonthExpectedCosts { get; init; }
        public decimal CurrentMonthPaidCosts { get; init; }
        public decimal CurrentMonthPresumedPaidCosts { get; init; }
        public decimal CurrentMonthPresumedProvisioned { get; init; }
        public decimal CurrentMonthRemainingCosts { get; init; }
        public decimal RemainingCostsForYear { get; init; }
        public decimal? CurrentAccountBalance { get; init; }
        public int? CurrentAccountBalanceMonth { get; init; }
        public decimal? ProjectedYearEndBalance { get; init; }
    }

    public sealed class DashboardMonthRow
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public string Label { get; init; } = string.Empty;
        public decimal ExpectedCosts { get; init; }
        public decimal PaidCosts { get; init; }
        public decimal PresumedPaidCosts { get; init; }
        public decimal Provisioned { get; init; }
        public decimal PresumedProvisioned { get; init; }
        public IReadOnlyList<DashboardPendingItem> PendingItems { get; init; } = [];
        public decimal BalanceAfterMappedItems { get; init; }
        public decimal CostDifference => PaidCosts - ExpectedCosts;
    }

    public sealed class DashboardPendingItem
    {
        public string Description { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public DateOnly DueDate { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class PendingMonthAmounts
    {
        public decimal PresumedPaidCosts { get; set; }
        public decimal PresumedProvisioned { get; set; }
        public List<DashboardPendingItem> Items { get; } = [];
    }
}
