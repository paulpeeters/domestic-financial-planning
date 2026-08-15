namespace FinancialPlanningApp.Web.Services.Planning;

public sealed record AnnualPlanSummary(decimal ExpectedYearlyCost, decimal ExpectedMonthlyCost, decimal SuggestedMonthlyTransfer);

public interface IAnnualPlanningService
{
    Task<AnnualPlanSummary> BuildCurrentYearAsync(CancellationToken cancellationToken = default);
}
