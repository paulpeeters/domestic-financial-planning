using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services.Auth;

namespace FinancialPlanningApp.Web.Services.Payments;

public sealed class RecurringPaymentService(
    IRecurringPaymentTemplateRepository repository,
    ITenantContextService tenantContext) : IRecurringPaymentService
{
    public async Task<PagedResult<RecurringPaymentTemplate>> ListForCurrentUserAsync(RecurringPaymentFilter filter, CancellationToken cancellationToken = default)
    {
        var userId = tenantContext.GetCurrentUserId();
        var tenantId = tenantContext.GetCurrentTenantId();
        var query = new RecurringPaymentListQuery(filter.IncludeInactive, filter.Search, filter.PageNumber, filter.PageSize);
        var result = await repository.ListByUserAsync(userId, tenantId, query, cancellationToken);
        return new PagedResult<RecurringPaymentTemplate>(result.Items, result.TotalCount, filter.PageNumber, filter.PageSize);
    }

    public Task<IReadOnlyList<RecurringPaymentTemplate>> ListAllForCurrentUserAsync(bool includeInactive, CancellationToken cancellationToken = default)
        => repository.ListAllByUserAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), includeInactive, cancellationToken);

    public Task<RecurringPaymentTemplate?> GetForCurrentUserAsync(long templateId, CancellationToken cancellationToken = default)
        => repository.GetByIdAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), templateId, cancellationToken);

    public async Task<long> CreateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default)
    {
        template.UserId = tenantContext.GetCurrentUserId();
        template.TenantId = tenantContext.GetCurrentTenantId();
        template.PaymentMonths = RecurringPaymentSchedule.NormalizePaymentMonths(template.PaymentMonths);
        template.PaymentLagMonths = Math.Max(0, template.PaymentLagMonths);
        template.NormalizedMonthlyAmount = RecurringPaymentSchedule.GetNormalizedMonthlyAmount(template.Periodicity, template.Amount, template.PaymentMonths, template.AmountMode, template.MonthlyAmountsJson);
        return await repository.CreateAsync(template, cancellationToken);
    }

    public async Task<bool> UpdateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default)
    {
        template.UserId = tenantContext.GetCurrentUserId();
        template.TenantId = tenantContext.GetCurrentTenantId();
        template.PaymentMonths = RecurringPaymentSchedule.NormalizePaymentMonths(template.PaymentMonths);
        template.PaymentLagMonths = Math.Max(0, template.PaymentLagMonths);
        template.NormalizedMonthlyAmount = RecurringPaymentSchedule.GetNormalizedMonthlyAmount(template.Periodicity, template.Amount, template.PaymentMonths, template.AmountMode, template.MonthlyAmountsJson);
        return await repository.UpdateAsync(template, cancellationToken);
    }

    public Task<bool> UpdateDisplayOrderAsync(IReadOnlyList<long> templateIds, CancellationToken cancellationToken = default)
        => repository.UpdateDisplayOrdersAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), templateIds, cancellationToken);

    public Task<bool> ArchiveAsync(long templateId, CancellationToken cancellationToken = default)
        => repository.SetActiveStateAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), templateId, false, cancellationToken);

    public Task<bool> ActivateAsync(long templateId, CancellationToken cancellationToken = default)
        => repository.SetActiveStateAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), templateId, true, cancellationToken);

    public Task<bool> DeleteAsync(long templateId, CancellationToken cancellationToken = default)
        => repository.DeleteAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), templateId, cancellationToken);

}
