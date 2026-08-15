using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services.Auth;

namespace FinancialPlanningApp.Web.Services.Imports;

public interface IAccountMonthlyBalanceService
{
    Task UpsertForCurrentUserAsync(AccountMonthlyBalance balance, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMonthlyBalance>> ListByYearForCurrentUserAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMonthlyBalance>> ListByYearForCurrentTenantAsync(int year, CancellationToken cancellationToken = default);
}

public sealed class AccountMonthlyBalanceService(
    IAccountMonthlyBalanceRepository repository,
    ITenantContextService tenantContext) : IAccountMonthlyBalanceService
{
    public async Task UpsertForCurrentUserAsync(AccountMonthlyBalance balance, CancellationToken cancellationToken = default)
    {
        balance.UserId = tenantContext.GetCurrentUserId();
        balance.TenantId = tenantContext.GetCurrentTenantId();
        await repository.UpsertAsync(balance, cancellationToken);
    }

    public Task<IReadOnlyList<AccountMonthlyBalance>> ListByYearForCurrentUserAsync(int year, CancellationToken cancellationToken = default)
        => repository.ListByYearAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), year, cancellationToken);

    public Task<IReadOnlyList<AccountMonthlyBalance>> ListByYearForCurrentTenantAsync(int year, CancellationToken cancellationToken = default)
        => repository.ListByTenantYearAsync(tenantContext.GetCurrentTenantId(), year, cancellationToken);
}
