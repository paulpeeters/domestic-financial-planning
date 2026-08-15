using FinancialPlanningApp.Web.Data.Models;

namespace FinancialPlanningApp.Web.Data.Repositories;

public interface IRecurringPaymentTemplateRepository
{
    Task<(IReadOnlyList<RecurringPaymentTemplate> Items, int TotalCount)> ListByUserAsync(long userId, long tenantId, RecurringPaymentListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringPaymentTemplate>> ListAllByUserAsync(long userId, long tenantId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<RecurringPaymentTemplate?> GetByIdAsync(long userId, long tenantId, long templateId, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default);
    Task<bool> UpdateDisplayOrdersAsync(long userId, long tenantId, IReadOnlyList<long> templateIds, CancellationToken cancellationToken = default);
    Task<bool> SetActiveStateAsync(long userId, long tenantId, long templateId, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long userId, long tenantId, long templateId, CancellationToken cancellationToken = default);
}
