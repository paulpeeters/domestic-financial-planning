using FinancialPlanningApp.Web.Data.Models;

namespace FinancialPlanningApp.Web.Data.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<int> CountUsersAsync(CancellationToken cancellationToken = default);
    Task<long> CreateAsync(string email, string passwordHash, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task EnsureDefaultTenantForUserAsync(long userId, string email, CancellationToken cancellationToken = default);
    Task<long?> GetDefaultTenantIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveTenantMembershipsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserTenantMembership>> ListTenantMembershipsAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveTenantMembershipAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
    Task<string?> GetTenantRoleAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantMember>> ListTenantMembersAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> UpsertTenantMembershipAsync(long tenantId, long targetUserId, string role, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> UpdateTenantMemberDisplayAsync(long tenantId, long targetUserId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task<bool> DeactivateTenantMembershipAsync(long tenantId, long targetUserId, CancellationToken cancellationToken = default);
    Task<int> CountActiveOwnersAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> SetGlobalAdminAsync(long userId, bool isGlobalAdmin, CancellationToken cancellationToken = default);
    Task<bool> SetUserActiveAsync(long userId, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<int> CountGlobalAdminsAsync(CancellationToken cancellationToken = default);
    Task<bool> SetPreferredTenantAsync(long userId, long tenantId, CancellationToken cancellationToken = default);
    Task<bool> UpdateProfileAsync(long userId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task<TenantInfo?> GetTenantByIdAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<long> CreateTenantAsync(string name, string? shortName, string? slug, CancellationToken cancellationToken = default);
    Task<bool> SetTenantActiveAsync(long tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> UpdateTenantDisplayAsync(long tenantId, string name, string? shortName, CancellationToken cancellationToken = default);
    Task<TenantPurgePreview?> GetTenantPurgePreviewAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<bool> PurgeTenantAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<UserPurgePreview?> GetUserPurgePreviewAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> PurgeUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<string?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> SetAppSettingAsync(string key, string? value, CancellationToken cancellationToken = default);
}

public sealed class TenantPurgePreview
{
    public long TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int MembershipCount { get; init; }
    public int UsersDeletedCount { get; init; }
    public int PaymentExecutionsCount { get; init; }
    public int RecurringTemplatesCount { get; init; }
    public int AccountBalancesCount { get; init; }
    public int RegisteredSourcesCount { get; init; }
}

public sealed class UserPurgePreview
{
    public long UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsGlobalAdmin { get; init; }
    public int MembershipCount { get; init; }
    public int PaymentExecutionsCount { get; init; }
    public int RecurringTemplatesCount { get; init; }
    public int AccountBalancesCount { get; init; }
    public int RegisteredSourcesCount { get; init; }
    public int LoginAttemptsCount { get; init; }
}
