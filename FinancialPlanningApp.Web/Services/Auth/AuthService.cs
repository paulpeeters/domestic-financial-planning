using FinancialPlanningApp.Web.Data.Repositories;

namespace FinancialPlanningApp.Web.Services.Auth;

public sealed class AuthService(IUserRepository users, IPasswordService passwordService) : IAuthService
{
    public async Task<(bool Success, string? Error)> RegisterAsync(string email, string password, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await users.GetByEmailAsync(normalized, cancellationToken);
        if (existing is not null)
        {
            return (false, "Er bestaat al een account met dit e-mailadres.");
        }

        var passwordHash = passwordService.Hash(password);
        var userId = await users.CreateAsync(normalized, passwordHash, firstName, lastName, avatarUrl, cancellationToken);
        await users.EnsureDefaultTenantForUserAsync(userId, normalized, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, long UserId, long TenantId, bool IsGlobalAdmin, bool RequiresTenantSelection, string? FirstName, string? LastName, string? AvatarUrl, string? Error)> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalized, cancellationToken);
        if (user is null || !user.IsActive || !passwordService.Verify(user.PasswordHash, password))
        {
            return (false, 0, 0, false, false, null, null, null, "Ongeldige aanmeldgegevens.");
        }

        await users.EnsureDefaultTenantForUserAsync(user.Id, normalized, cancellationToken);
        var tenantId = await users.GetDefaultTenantIdAsync(user.Id, cancellationToken);
        if (tenantId is null)
        {
            return (false, 0, 0, false, false, null, null, null, "Geen actieve tenant gevonden voor deze gebruiker.");
        }

        var count = await users.CountActiveTenantMembershipsAsync(user.Id, cancellationToken);
        var requiresTenantSelection = count > 1 && user.PreferredTenantId is null;

        return (true, user.Id, tenantId.Value, user.IsGlobalAdmin, requiresTenantSelection, user.FirstName, user.LastName, user.AvatarUrl, null);
    }
}
