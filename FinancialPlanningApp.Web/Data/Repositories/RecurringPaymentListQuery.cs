namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed record RecurringPaymentListQuery(bool IncludeInactive, string? Search, int PageNumber, int PageSize);
