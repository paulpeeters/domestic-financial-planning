using FinancialPlanningApp.Web.Data.Models;

namespace FinancialPlanningApp.Web.Services.Payments;

public sealed record RecurringPaymentFilter(string? Search, bool IncludeInactive, int PageNumber, int PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public interface IRecurringPaymentService
{
    Task<PagedResult<RecurringPaymentTemplate>> ListForCurrentUserAsync(RecurringPaymentFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringPaymentTemplate>> ListAllForCurrentUserAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<RecurringPaymentTemplate?> GetForCurrentUserAsync(long templateId, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default);
    Task<bool> UpdateDisplayOrderAsync(IReadOnlyList<long> templateIds, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(long templateId, CancellationToken cancellationToken = default);
    Task<bool> ActivateAsync(long templateId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long templateId, CancellationToken cancellationToken = default);
}
