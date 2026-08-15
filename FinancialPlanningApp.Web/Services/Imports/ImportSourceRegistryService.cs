using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services.Auth;

namespace FinancialPlanningApp.Web.Services.Imports;

public interface IImportSourceRegistryService
{
    Task<bool> IsRegisteredBankAccountForCurrentUserAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<bool> IsRegisteredCreditCardForCurrentUserAsync(string cardNumber, CancellationToken cancellationToken = default);
}

public sealed class ImportSourceRegistryService(
    IImportSourceRegistryRepository repository,
    ITenantContextService tenantContextService) : IImportSourceRegistryService
{
    public Task<bool> IsRegisteredBankAccountForCurrentUserAsync(string accountNumber, CancellationToken cancellationToken = default)
        => repository.IsRegisteredBankAccountAsync(GetCurrentUserId(), GetCurrentTenantId(), accountNumber, cancellationToken);

    public Task<bool> IsRegisteredCreditCardForCurrentUserAsync(string cardNumber, CancellationToken cancellationToken = default)
        => repository.IsRegisteredCreditCardAsync(GetCurrentUserId(), GetCurrentTenantId(), cardNumber, cancellationToken);

    private long GetCurrentUserId() => tenantContextService.GetCurrentUserId();
    private long GetCurrentTenantId() => tenantContextService.GetCurrentTenantId();
}
