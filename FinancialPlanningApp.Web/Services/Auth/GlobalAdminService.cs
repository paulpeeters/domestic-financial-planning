using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using System.Security.Claims;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface IGlobalAdminService
{
    Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateUserProfileAsync(long targetUserId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> SetGlobalAdminAsync(long targetUserId, bool isGlobalAdmin, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> SetUserActiveAsync(long targetUserId, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateTenantAsync(string name, string? shortName, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> SetTenantActiveAsync(long tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task<TenantPurgePreview?> GetTenantPurgePreviewAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> PurgeTenantAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<UserPurgePreview?> GetUserPurgePreviewAsync(long userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> PurgeUserAsync(long targetUserId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ResetUserPasswordAsync(long targetUserId, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> AddUserToTenantAsync(string email, long tenantId, string role, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> GetAllowSelfRegistrationAsync(CancellationToken cancellationToken = default);
    Task SetAllowSelfRegistrationAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateUserAndAssignTenantAsync(string email, string password, string? firstName, string? lastName, string? avatarUrl, long tenantId, string role, bool isActive, bool isGlobalAdmin, CancellationToken cancellationToken = default);
}

public sealed class GlobalAdminService(
    IUserRepository userRepository,
    IHttpContextAccessor httpContextAccessor,
    IApplicationSettingsService applicationSettingsService,
    IPasswordService passwordService) : IGlobalAdminService
{
    public Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken cancellationToken = default)
        => userRepository.ListUsersAsync(cancellationToken);

    public async Task<(bool Success, string? Error)> UpdateUserProfileAsync(long targetUserId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        var targetUser = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return (false, "Gebruiker niet gevonden.");
        }

        var normalizedFirstName = Normalize(firstName);
        var normalizedLastName = Normalize(lastName);
        var normalizedAvatarUrl = Normalize(avatarUrl);
        if (string.Equals(targetUser.FirstName, normalizedFirstName, StringComparison.Ordinal)
            && string.Equals(targetUser.LastName, normalizedLastName, StringComparison.Ordinal)
            && string.Equals(targetUser.AvatarUrl, normalizedAvatarUrl, StringComparison.Ordinal))
        {
            return (true, null);
        }

        var changed = await userRepository.UpdateProfileAsync(
            targetUserId,
            normalizedFirstName,
            normalizedLastName,
            normalizedAvatarUrl,
            cancellationToken);

        return changed ? (true, null) : (false, "Gebruikersprofiel bijwerken mislukt.");
    }

    public async Task<(bool Success, string? Error)> SetGlobalAdminAsync(long targetUserId, bool isGlobalAdmin, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var targetUser = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return (false, "Gebruiker niet gevonden.");
        }

        if (!isGlobalAdmin && targetUser.IsGlobalAdmin)
        {
            if (targetUserId == currentUserId)
            {
                return (false, "Je kan je eigen globale-adminrol niet verwijderen.");
            }

            if (targetUser.IsActive)
            {
                var count = await userRepository.CountGlobalAdminsAsync(cancellationToken);
                if (count <= 1)
                {
                    return (false, "Minstens een actieve globale admin is verplicht.");
                }
            }
        }

        if (targetUser.IsGlobalAdmin == isGlobalAdmin)
        {
            return (true, null);
        }

        var changed = await userRepository.SetGlobalAdminAsync(targetUserId, isGlobalAdmin, cancellationToken);
        return changed ? (true, null) : (false, "Globale-adminvlag bijwerken mislukt.");
    }

    public async Task<(bool Success, string? Error)> SetUserActiveAsync(long targetUserId, bool isActive, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var targetUser = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return (false, "Gebruiker niet gevonden.");
        }

        if (!isActive)
        {
            if (targetUserId == currentUserId)
            {
                return (false, "Je kan je eigen gebruiker niet deactiveren.");
            }

            if (targetUser.IsGlobalAdmin)
            {
                var count = await userRepository.CountGlobalAdminsAsync(cancellationToken);
                if (count <= 1)
                {
                    return (false, "Minstens een actieve globale admin is verplicht.");
                }
            }
        }

        if (targetUser.IsActive == isActive)
        {
            return (true, null);
        }

        var changed = await userRepository.SetUserActiveAsync(targetUserId, isActive, cancellationToken);
        return changed ? (true, null) : (false, "Actieve status van gebruiker bijwerken mislukt.");
    }

    public Task<IReadOnlyList<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default)
        => userRepository.ListTenantsAsync(cancellationToken);

    public async Task<(bool Success, string? Error)> CreateTenantAsync(string name, string? shortName, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return (false, "Tenantnaam is verplicht.");
        }

        var normalizedShort = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        if (normalizedShort is not null && normalizedShort.Length > 10)
        {
            return (false, "Korte tenantnaam mag maximaal 10 tekens lang zijn.");
        }

        var slugBase = $"{trimmedName.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}";
        var slug = slugBase[..Math.Min(40, slugBase.Length)];
        await userRepository.CreateTenantAsync(trimmedName, normalizedShort, slug, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetTenantActiveAsync(long tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        var tenant = await userRepository.GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return (false, "Tenant niet gevonden.");
        }

        var changed = await userRepository.SetTenantActiveAsync(tenantId, isActive, cancellationToken);
        return changed ? (true, null) : (false, "Actieve status van tenant bijwerken mislukt.");
    }

    public Task<TenantPurgePreview?> GetTenantPurgePreviewAsync(long tenantId, CancellationToken cancellationToken = default)
        => userRepository.GetTenantPurgePreviewAsync(tenantId, cancellationToken);

    public async Task<(bool Success, string? Error)> PurgeTenantAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await userRepository.GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return (false, "Tenant niet gevonden.");
        }

        if (tenant.IsActive)
        {
            return (false, "Deactiveer de tenant voordat je hem definitief verwijdert.");
        }

        var currentUserId = GetCurrentUserId();
        if ((await userRepository.ListTenantMembershipsAsync(currentUserId, cancellationToken)).Any(m => m.TenantId == tenantId))
        {
            return (false, "Je kan geen tenant definitief verwijderen die aan je eigen gebruiker gekoppeld is.");
        }

        var members = await userRepository.ListTenantMembersAsync(tenantId, cancellationToken);
        foreach (var member in members)
        {
            var user = await userRepository.GetByIdAsync(member.UserId, cancellationToken);
            if (user?.IsGlobalAdmin == true)
            {
                return (false, "Verwijder globale-admingebruikers uit de tenant voordat je hem definitief verwijdert.");
            }
        }

        var ok = await userRepository.PurgeTenantAsync(tenantId, cancellationToken);
        return ok ? (true, null) : (false, "Tenant definitief verwijderen mislukt.");
    }

    public Task<UserPurgePreview?> GetUserPurgePreviewAsync(long userId, CancellationToken cancellationToken = default)
        => userRepository.GetUserPurgePreviewAsync(userId, cancellationToken);

    public async Task<(bool Success, string? Error)> PurgeUserAsync(long targetUserId, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (targetUserId == currentUserId)
        {
            return (false, "Je kan je eigen gebruiker niet definitief verwijderen.");
        }

        var targetUser = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return (false, "Gebruiker niet gevonden.");
        }

        if (targetUser.IsActive)
        {
            return (false, "Deactiveer de gebruiker voordat je hem definitief verwijdert.");
        }

        if (targetUser.IsGlobalAdmin)
        {
            return (false, "Verwijder de globale-adminrol voordat je deze gebruiker definitief verwijdert.");
        }

        var ok = await userRepository.PurgeUserAsync(targetUserId, cancellationToken);
        return ok ? (true, null) : (false, "Gebruiker definitief verwijderen mislukt.");
    }

    public async Task<(bool Success, string? Error)> ResetUserPasswordAsync(long targetUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return (false, "Het wachtwoord moet minstens 8 tekens lang zijn.");
        }

        var targetUser = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return (false, "Gebruiker niet gevonden.");
        }

        var passwordHash = passwordService.Hash(newPassword);
        var changed = await userRepository.UpdatePasswordHashAsync(targetUserId, passwordHash, cancellationToken);
        return changed ? (true, null) : (false, "Wachtwoord bijwerken mislukt.");
    }

    public async Task<(bool Success, string? Error)> AddUserToTenantAsync(string email, long tenantId, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        var allowedRoles = new[] { "OWNER", "ADMIN", "EDITOR", "VIEWER" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return (false, "Ongeldige rol.");
        }

        var user = await userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            return (false, "Gebruiker niet gevonden.");
        }

        var tenant = await userRepository.GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return (false, "Tenant niet gevonden.");
        }

        await userRepository.UpsertTenantMembershipAsync(tenantId, user.Id, role.ToUpperInvariant(), isActive, cancellationToken);
        return (true, null);
    }

    public Task<bool> GetAllowSelfRegistrationAsync(CancellationToken cancellationToken = default)
        => applicationSettingsService.GetAllowSelfRegistrationAsync(cancellationToken);

    public Task SetAllowSelfRegistrationAsync(bool enabled, CancellationToken cancellationToken = default)
        => applicationSettingsService.SetAllowSelfRegistrationAsync(enabled, cancellationToken);

    public async Task<(bool Success, string? Error)> CreateUserAndAssignTenantAsync(string email, string password, string? firstName, string? lastName, string? avatarUrl, long tenantId, string role, bool isActive, bool isGlobalAdmin, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return (false, "E-mail is verplicht.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return (false, "Het wachtwoord moet minstens 8 tekens lang zijn.");
        }

        var allowedRoles = new[] { "OWNER", "ADMIN", "EDITOR", "VIEWER" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return (false, "Ongeldige rol.");
        }

        if (await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            return (false, "Gebruiker bestaat al.");
        }

        var tenant = await userRepository.GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return (false, "Tenant niet gevonden.");
        }

        var passwordHash = passwordService.Hash(password);
        var userId = await userRepository.CreateAsync(normalizedEmail, passwordHash, firstName, lastName, avatarUrl, cancellationToken);
        await userRepository.EnsureDefaultTenantForUserAsync(userId, normalizedEmail, cancellationToken);
        await userRepository.UpsertTenantMembershipAsync(tenantId, userId, role.ToUpperInvariant(), isActive, cancellationToken);
        if (isGlobalAdmin)
        {
            await userRepository.SetGlobalAdminAsync(userId, true, cancellationToken);
        }

        return (true, null);
    }

    private long GetCurrentUserId()
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(claim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id ontbreekt.");
        }

        return userId;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
