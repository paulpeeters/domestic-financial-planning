using FinancialPlanningApp.Web.Services.Payments;

namespace FinancialPlanningApp.Web.Services.Planning;

public sealed class AnnualPlanningService(IRecurringPaymentService recurringPaymentService) : IAnnualPlanningService
{
    public async Task<AnnualPlanSummary> BuildCurrentYearAsync(CancellationToken cancellationToken = default)
    {
        var templates = await recurringPaymentService.ListForCurrentUserAsync(new RecurringPaymentFilter(null, true, 1, 500), cancellationToken);
        var year = DateTime.Today.Year;
        var yearly = templates.Items
            .Where(t => t.IsActive)
            .Sum(t => Enumerable.Range(1, 12)
                .Where(month => RecurringPaymentSchedule.GetPaymentMonths(t).Contains(month))
                .Where(month => RecurringPaymentSchedule.IsValidForMonth(t, year, month))
                .Sum(month => RecurringPaymentSchedule.GetOccurrenceAmount(t, month)));
        var monthly = yearly / 12m;
        var suggestedTransfer = Math.Ceiling(monthly);

        return new AnnualPlanSummary(yearly, monthly, suggestedTransfer);
    }
}
