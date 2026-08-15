namespace FinancialPlanningApp.Web.Services.Auth;

public interface IAuthService
{
    Task<(bool Success, string? Error)> RegisterAsync(string email, string password, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task<(bool Success, long UserId, long TenantId, bool IsGlobalAdmin, bool RequiresTenantSelection, string? FirstName, string? LastName, string? AvatarUrl, string? Error)> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);
}
