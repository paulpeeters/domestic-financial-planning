using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface ITenantMembershipService
{
    Task<IReadOnlyList<UserTenantMembership>> ListForCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<bool> CanAccessTenantAsync(long tenantId, CancellationToken cancellationToken = default);
}

public sealed class TenantMembershipService(
    IUserRepository userRepository,
    ITenantContextService tenantContextService) : ITenantMembershipService
{
    public Task<IReadOnlyList<UserTenantMembership>> ListForCurrentUserAsync(CancellationToken cancellationToken = default)
        => userRepository.ListTenantMembershipsAsync(tenantContextService.GetCurrentUserId(), cancellationToken);

    public Task<bool> CanAccessTenantAsync(long tenantId, CancellationToken cancellationToken = default)
        => userRepository.HasActiveTenantMembershipAsync(tenantContextService.GetCurrentUserId(), tenantId, cancellationToken);
}
