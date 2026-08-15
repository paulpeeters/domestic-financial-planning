using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface ITenantAdministrationService
{
    Task<IReadOnlyList<TenantMember>> ListMembersForCurrentTenantAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> ListUsersNotInCurrentTenantAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> AddOrUpdateMemberByEmailAsync(string email, string role, bool isActive, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateMemberAsync(long targetUserId, string role, bool isActive, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateMemberDisplayAsync(long targetUserId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task<TenantInfo?> GetCurrentTenantInfoAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateCurrentTenantDisplayAsync(string name, string? shortName, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateUserAndAddToCurrentTenantAsync(string email, string password, string? firstName, string? lastName, string? avatarUrl, string role, bool isActive, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> RemoveMemberFromCurrentTenantAsync(long targetUserId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ResetMemberPasswordAsync(long targetUserId, string newPassword, CancellationToken cancellationToken = default);
}

public sealed class TenantAdministrationService(
    IUserRepository userRepository,
    ITenantContextService tenantContextService,
    IHttpContextAccessor httpContextAccessor,
    IPasswordService passwordService) : ITenantAdministrationService
{
    private static readonly HashSet<string> AllowedRoles = ["OWNER", "ADMIN", "EDITOR", "VIEWER"];

    public async Task<IReadOnlyList<TenantMember>> ListMembersForCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        return await userRepository.ListTenantMembersAsync(tenantContextService.GetCurrentTenantId(), cancellationToken);
    }

    public async Task<IReadOnlyList<AppUser>> ListUsersNotInCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        var tenantId = tenantContextService.GetCurrentTenantId();
        var members = await userRepository.ListTenantMembersAsync(tenantId, cancellationToken);
        var memberIds = members.Select(m => m.UserId).ToHashSet();
        var users = await userRepository.ListUsersAsync(cancellationToken);
        return users
            .Where(u => !memberIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ToList();
    }

    public async Task<(bool Success, string? Error)> AddOrUpdateMemberByEmailAsync(string email, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        if (!AllowedRoles.Contains(role))
        {
            return (false, "Ongeldige rol.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return (false, "E-mail is verplicht.");
        }

        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return (false, "Er bestaat nog geen gebruiker met dit e-mailadres.");
        }

        return await UpdateMemberInternalAsync(user.Id, role, isActive, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> UpdateMemberAsync(long targetUserId, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        if (!AllowedRoles.Contains(role))
        {
            return (false, "Ongeldige rol.");
        }

        var targetUser = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return (false, "Doelgebruiker bestaat niet.");
        }

        return await UpdateMemberInternalAsync(targetUserId, role, isActive, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> UpdateMemberDisplayAsync(long targetUserId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);

        var tenantId = tenantContextService.GetCurrentTenantId();
        var members = await userRepository.ListTenantMembersAsync(tenantId, cancellationToken);
        if (!members.Any(m => m.UserId == targetUserId))
        {
            return (false, "Lid niet gevonden in de actieve tenant.");
        }

        await userRepository.UpdateTenantMemberDisplayAsync(
            tenantId,
            targetUserId,
            Normalize(firstName),
            Normalize(lastName),
            Normalize(avatarUrl),
            cancellationToken);

        return (true, null);
    }

    public async Task<TenantInfo?> GetCurrentTenantInfoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        return await userRepository.GetTenantByIdAsync(tenantContextService.GetCurrentTenantId(), cancellationToken);
    }

    public async Task<(bool Success, string? Error)> UpdateCurrentTenantDisplayAsync(string name, string? shortName, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
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

        var ok = await userRepository.UpdateTenantDisplayAsync(
            tenantContextService.GetCurrentTenantId(),
            trimmedName,
            normalizedShort,
            cancellationToken);

        return ok ? (true, null) : (false, "Tenantweergave bijwerken mislukt.");
    }

    public async Task<(bool Success, string? Error)> CreateUserAndAddToCurrentTenantAsync(string email, string password, string? firstName, string? lastName, string? avatarUrl, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        if (!AllowedRoles.Contains(role))
        {
            return (false, "Ongeldige rol.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return (false, "E-mail is verplicht.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return (false, "Het wachtwoord moet minstens 8 tekens lang zijn.");
        }

        var existing = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return (false, "Gebruiker bestaat al. Gebruik lidmaatschap toevoegen/bijwerken.");
        }

        var passwordHash = passwordService.Hash(password);
        var userId = await userRepository.CreateAsync(normalizedEmail, passwordHash, firstName, lastName, avatarUrl, cancellationToken);
        await userRepository.EnsureDefaultTenantForUserAsync(userId, normalizedEmail, cancellationToken);
        await userRepository.UpsertTenantMembershipAsync(tenantContextService.GetCurrentTenantId(), userId, role, isActive, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveMemberFromCurrentTenantAsync(long targetUserId, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        var tenantId = tenantContextService.GetCurrentTenantId();
        var currentUserId = tenantContextService.GetCurrentUserId();

        if (targetUserId == currentUserId)
        {
            return (false, "Je kan jezelf niet verwijderen uit de actieve tenant.");
        }

        var members = await userRepository.ListTenantMembersAsync(tenantId, cancellationToken);
        var target = members.FirstOrDefault(m => m.UserId == targetUserId);
        if (target is null || !target.IsActive)
        {
            return (false, "Lid niet gevonden of al inactief.");
        }

        if (string.Equals(target.Role, "OWNER", StringComparison.OrdinalIgnoreCase))
        {
            var ownerCount = await userRepository.CountActiveOwnersAsync(tenantId, cancellationToken);
            if (ownerCount <= 1)
            {
                return (false, "Minstens een actieve OWNER is verplicht per tenant.");
            }
        }

        var ok = await userRepository.DeactivateTenantMembershipAsync(tenantId, targetUserId, cancellationToken);
        return ok ? (true, null) : (false, "Lid verwijderen mislukt.");
    }

    public async Task<(bool Success, string? Error)> ResetMemberPasswordAsync(long targetUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageTenantAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return (false, "Het wachtwoord moet minstens 8 tekens lang zijn.");
        }

        var tenantId = tenantContextService.GetCurrentTenantId();
        var members = await userRepository.ListTenantMembersAsync(tenantId, cancellationToken);
        if (!members.Any(m => m.UserId == targetUserId))
        {
            return (false, "Lid niet gevonden in de actieve tenant.");
        }

        var passwordHash = passwordService.Hash(newPassword);
        var ok = await userRepository.UpdatePasswordHashAsync(targetUserId, passwordHash, cancellationToken);
        return ok ? (true, null) : (false, "Wachtwoord bijwerken mislukt.");
    }

    private async Task<(bool Success, string? Error)> UpdateMemberInternalAsync(long targetUserId, string role, bool isActive, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextService.GetCurrentTenantId();
        var currentUserId = tenantContextService.GetCurrentUserId();

        var existingRole = await userRepository.GetTenantRoleAsync(targetUserId, tenantId, cancellationToken);
        var wasOwner = string.Equals(existingRole, "OWNER", StringComparison.OrdinalIgnoreCase);
        var becomesOwner = string.Equals(role, "OWNER", StringComparison.OrdinalIgnoreCase) && isActive;
        var stillOwner = wasOwner && becomesOwner;

        if (targetUserId == currentUserId && !stillOwner)
        {
            return (false, "Je kan je eigen OWNER-rechten in de actieve tenant niet verwijderen.");
        }

        if (wasOwner && !becomesOwner)
        {
            var ownerCount = await userRepository.CountActiveOwnersAsync(tenantId, cancellationToken);
            if (ownerCount <= 1)
            {
                return (false, "Minstens een actieve OWNER is verplicht per tenant.");
            }
        }

        var ok = await userRepository.UpsertTenantMembershipAsync(tenantId, targetUserId, role, isActive, cancellationToken);
        return ok ? (true, null) : (false, "Tenantlidmaatschap bijwerken mislukt.");
    }

    private async Task EnsureCanManageTenantAsync(CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext?.User.HasClaim(AuthClaimTypes.GlobalAdmin, "true") == true)
        {
            return;
        }

        var role = await userRepository.GetTenantRoleAsync(
            tenantContextService.GetCurrentUserId(),
            tenantContextService.GetCurrentTenantId(),
            cancellationToken);

        if (!string.Equals(role, "OWNER", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Alleen OWNER of ADMIN kan tenantlidmaatschappen beheren.");
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
