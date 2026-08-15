using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id, email, password_hash AS PasswordHash, is_global_admin AS IsGlobalAdmin, is_active AS IsActive, preferred_tenant_id AS PreferredTenantId, first_name AS FirstName, last_name AS LastName, avatar_url AS AvatarUrl, created_utc AS CreatedUtc FROM app_users WHERE email = @email LIMIT 1;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<AppUser>(sql, new { email });
    }

    public async Task<AppUser?> GetByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id, email, password_hash AS PasswordHash, is_global_admin AS IsGlobalAdmin, is_active AS IsActive, preferred_tenant_id AS PreferredTenantId, first_name AS FirstName, last_name AS LastName, avatar_url AS AvatarUrl, created_utc AS CreatedUtc FROM app_users WHERE id = @userId LIMIT 1;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<AppUser>(sql, new { userId });
    }

    public async Task<long> CreateAsync(string email, string passwordHash, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO app_users(email, first_name, last_name, avatar_url, password_hash, created_utc) VALUES (@email, @firstName, @lastName, @avatarUrl, @passwordHash, UTC_TIMESTAMP(6)); SELECT LAST_INSERT_ID();";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, new { email, firstName, lastName, avatarUrl, passwordHash });
    }

    public async Task EnsureDefaultTenantForUserAsync(long userId, string email, CancellationToken cancellationToken = default)
    {
        const string insertTenantSql = """
        INSERT INTO tenants(name, slug, is_active, created_utc, updated_utc)
        VALUES (@name, @slug, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id), updated_utc = UTC_TIMESTAMP(6);
        """;

        const string getTenantIdSql = "SELECT id FROM tenants WHERE slug = @slug LIMIT 1;";

        const string insertMembershipSql = """
        INSERT INTO user_tenants(user_id, tenant_id, role, is_active, created_utc, updated_utc)
        VALUES (@userId, @tenantId, 'OWNER', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE role = 'OWNER', is_active = 1, updated_utc = UTC_TIMESTAMP(6);
        """;

        var slug = $"user-{userId}";
        var name = $"Personal - {email}";

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        await connection.ExecuteAsync(insertTenantSql, new { name, slug }, tx);
        var tenantId = await connection.ExecuteScalarAsync<long>(getTenantIdSql, new { slug }, tx);
        await connection.ExecuteAsync(insertMembershipSql, new { userId, tenantId }, tx);

        tx.Commit();
    }

    public async Task<long?> GetDefaultTenantIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT COALESCE(
            (
                SELECT ut.tenant_id
                FROM user_tenants ut
                JOIN tenants t ON t.id = ut.tenant_id
                JOIN app_users u ON u.id = ut.user_id
                WHERE ut.user_id = @userId
                  AND ut.is_active = 1
                  AND t.is_active = 1
                  AND u.preferred_tenant_id IS NOT NULL
                  AND ut.tenant_id = u.preferred_tenant_id
                LIMIT 1
            ),
            (
                SELECT ut.tenant_id
                FROM user_tenants ut
                JOIN tenants t ON t.id = ut.tenant_id
                WHERE ut.user_id = @userId
                  AND ut.is_active = 1
                  AND t.is_active = 1
                ORDER BY CASE ut.role
                            WHEN 'OWNER' THEN 1
                            WHEN 'ADMIN' THEN 2
                            WHEN 'EDITOR' THEN 3
                            WHEN 'VIEWER' THEN 4
                            ELSE 5
                         END,
                         ut.id ASC
                LIMIT 1
            )
        ) AS tenant_id;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long?>(sql, new { userId });
    }

    public async Task<int> CountActiveTenantMembershipsAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT COUNT(*)
        FROM user_tenants ut
        JOIN tenants t ON t.id = ut.tenant_id
        WHERE ut.user_id = @userId
          AND ut.is_active = 1
          AND t.is_active = 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(sql, new { userId });
    }

    public async Task<IReadOnlyList<UserTenantMembership>> ListTenantMembershipsAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            ut.tenant_id AS TenantId,
            t.name AS TenantName,
            t.short_name AS TenantShortName,
            t.slug AS TenantSlug,
            ut.role AS Role,
            (ut.is_active = 1 AND t.is_active = 1) AS IsActive
        FROM user_tenants ut
        JOIN tenants t ON t.id = ut.tenant_id
        WHERE ut.user_id = @userId
        ORDER BY CASE role
                    WHEN 'OWNER' THEN 1
                    WHEN 'ADMIN' THEN 2
                    WHEN 'EDITOR' THEN 3
                    WHEN 'VIEWER' THEN 4
                    ELSE 5
                 END,
                 t.name ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserTenantMembership>(sql, new { userId });
        return rows.ToList();
    }

    public async Task<bool> HasActiveTenantMembershipAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM user_tenants ut
            JOIN tenants t ON t.id = ut.tenant_id
            WHERE ut.user_id = @userId
              AND ut.tenant_id = @tenantId
              AND ut.is_active = 1
              AND t.is_active = 1
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId });
    }

    public async Task<string?> GetTenantRoleAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT role
        FROM user_tenants ut
        JOIN tenants t ON t.id = ut.tenant_id
        WHERE ut.user_id = @userId
          AND ut.tenant_id = @tenantId
          AND ut.is_active = 1
          AND t.is_active = 1
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(sql, new { userId, tenantId });
    }

    public async Task<IReadOnlyList<TenantMember>> ListTenantMembersAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            u.id AS UserId,
            u.email AS Email,
            COALESCE(ut.display_first_name, u.first_name) AS FirstName,
            COALESCE(ut.display_last_name, u.last_name) AS LastName,
            COALESCE(ut.display_avatar_url, u.avatar_url) AS AvatarUrl,
            ut.role AS Role,
            ut.is_active AS IsActive
        FROM user_tenants ut
        JOIN app_users u ON u.id = ut.user_id
        WHERE ut.tenant_id = @tenantId
        ORDER BY CASE ut.role
                    WHEN 'OWNER' THEN 1
                    WHEN 'ADMIN' THEN 2
                    WHEN 'EDITOR' THEN 3
                    WHEN 'VIEWER' THEN 4
                    ELSE 5
                 END,
                 u.email ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TenantMember>(sql, new { tenantId });
        return rows.ToList();
    }

    public async Task<bool> UpsertTenantMembershipAsync(long tenantId, long targetUserId, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO user_tenants(user_id, tenant_id, role, is_active, created_utc, updated_utc)
        VALUES(@targetUserId, @tenantId, @role, @isActive, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE
            role = VALUES(role),
            is_active = VALUES(is_active),
            updated_utc = UTC_TIMESTAMP(6);
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, targetUserId, role, isActive }) > 0;
    }

    public async Task<bool> UpdateTenantMemberDisplayAsync(long tenantId, long targetUserId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE user_tenants
        SET display_first_name = @firstName,
            display_last_name = @lastName,
            display_avatar_url = @avatarUrl,
            updated_utc = UTC_TIMESTAMP(6)
        WHERE tenant_id = @tenantId
          AND user_id = @targetUserId;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, targetUserId, firstName, lastName, avatarUrl }) > 0;
    }

    public async Task<bool> DeactivateTenantMembershipAsync(long tenantId, long targetUserId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE user_tenants
        SET is_active = 0,
            updated_utc = UTC_TIMESTAMP(6)
        WHERE tenant_id = @tenantId
          AND user_id = @targetUserId;
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, targetUserId }) > 0;
    }

    public async Task<int> CountActiveOwnersAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT COUNT(*)
        FROM user_tenants
        WHERE tenant_id = @tenantId
          AND role = 'OWNER'
          AND is_active = 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(sql, new { tenantId });
    }

    public async Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, email, password_hash AS PasswordHash, is_global_admin AS IsGlobalAdmin, is_active AS IsActive, preferred_tenant_id AS PreferredTenantId, first_name AS FirstName, last_name AS LastName, avatar_url AS AvatarUrl, created_utc AS CreatedUtc
        FROM app_users
        ORDER BY email ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppUser>(sql);
        return rows.ToList();
    }

    public async Task<bool> SetGlobalAdminAsync(long userId, bool isGlobalAdmin, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE app_users SET is_global_admin = @isGlobalAdmin WHERE id = @userId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, isGlobalAdmin }) > 0;
    }

    public async Task<bool> SetUserActiveAsync(long userId, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE app_users SET is_active = @isActive WHERE id = @userId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, isActive }) > 0;
    }

    public async Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE app_users SET password_hash = @passwordHash WHERE id = @userId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, passwordHash }) > 0;
    }

    public async Task<int> CountGlobalAdminsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM app_users WHERE is_global_admin = 1 AND is_active = 1;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    public async Task<bool> SetPreferredTenantAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE app_users SET preferred_tenant_id = @tenantId WHERE id = @userId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, tenantId }) > 0;
    }

    public async Task<bool> UpdateProfileAsync(long userId, string? firstName, string? lastName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE app_users
        SET first_name = @firstName,
            last_name = @lastName,
            avatar_url = @avatarUrl
        WHERE id = @userId;
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, firstName, lastName, avatarUrl }) > 0;
    }

    public async Task<TenantInfo?> GetTenantByIdAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, name, short_name AS ShortName, is_active AS IsActive
        FROM tenants
        WHERE id = @tenantId
        LIMIT 1;
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<TenantInfo>(sql, new { tenantId });
    }

    public async Task<IReadOnlyList<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, name, short_name AS ShortName, is_active AS IsActive
        FROM tenants
        ORDER BY name ASC;
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TenantInfo>(sql);
        return rows.ToList();
    }

    public async Task<long> CreateTenantAsync(string name, string? shortName, string? slug, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO tenants(name, short_name, slug, is_active, created_utc, updated_utc)
        VALUES(@name, @shortName, @slug, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
        SELECT LAST_INSERT_ID();
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, new { name, shortName, slug });
    }

    public async Task<bool> UpdateTenantDisplayAsync(long tenantId, string name, string? shortName, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE tenants
        SET name = @name,
            short_name = @shortName,
            updated_utc = UTC_TIMESTAMP(6)
        WHERE id = @tenantId;
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, name, shortName }) > 0;
    }

    public async Task<bool> SetTenantActiveAsync(long tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE tenants
        SET is_active = @isActive,
            updated_utc = UTC_TIMESTAMP(6)
        WHERE id = @tenantId;
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, isActive }) > 0;
    }

    public async Task<TenantPurgePreview?> GetTenantPurgePreviewAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            t.id AS TenantId,
            t.name AS TenantName,
            t.is_active AS IsActive,
            (SELECT COUNT(*) FROM user_tenants ut WHERE ut.tenant_id = t.id) AS MembershipCount,
            (
                SELECT COUNT(*)
                FROM user_tenants ut
                JOIN app_users u ON u.id = ut.user_id
                WHERE ut.tenant_id = t.id
                  AND u.is_global_admin = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM user_tenants other_ut
                      WHERE other_ut.user_id = ut.user_id
                        AND other_ut.tenant_id <> t.id
                  )
            ) AS UsersDeletedCount,
            (SELECT COUNT(*) FROM payment_executions pe WHERE pe.tenant_id = t.id) AS PaymentExecutionsCount,
            (SELECT COUNT(*) FROM recurring_payment_templates rpt WHERE rpt.tenant_id = t.id) AS RecurringTemplatesCount,
            (SELECT COUNT(*) FROM account_monthly_balances amb WHERE amb.tenant_id = t.id) AS AccountBalancesCount,
            ((SELECT COUNT(*) FROM registered_bank_accounts rba WHERE rba.tenant_id = t.id)
             + (SELECT COUNT(*) FROM registered_credit_cards rcc WHERE rcc.tenant_id = t.id)) AS RegisteredSourcesCount
        FROM tenants t
        WHERE t.id = @tenantId
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<TenantPurgePreview>(sql, new { tenantId });
    }

    public async Task<bool> PurgeTenantAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var tenantExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM tenants WHERE id = @tenantId);",
            new { tenantId },
            tx);
        if (!tenantExists)
        {
            tx.Rollback();
            return false;
        }

        var exclusiveUserIds = (await connection.QueryAsync<long>(
            """
            SELECT ut.user_id
            FROM user_tenants ut
            JOIN app_users u ON u.id = ut.user_id
            WHERE ut.tenant_id = @tenantId
              AND u.is_global_admin = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM user_tenants other_ut
                  WHERE other_ut.user_id = ut.user_id
                    AND other_ut.tenant_id <> @tenantId
              );
            """,
            new { tenantId },
            tx)).ToList();

        await connection.ExecuteAsync("DELETE pc FROM payment_corrections pc WHERE pc.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE ptm FROM payment_template_mappings ptm WHERE ptm.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE pe FROM payment_executions pe WHERE pe.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE rpt FROM recurring_payment_templates rpt WHERE rpt.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE rba FROM registered_bank_accounts rba WHERE rba.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE rcc FROM registered_credit_cards rcc WHERE rcc.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE amb FROM account_monthly_balances amb WHERE amb.tenant_id = @tenantId;", new { tenantId }, tx);

        await connection.ExecuteAsync(
            "UPDATE app_users SET preferred_tenant_id = NULL WHERE preferred_tenant_id = @tenantId;",
            new { tenantId },
            tx);
        await connection.ExecuteAsync("DELETE ut FROM user_tenants ut WHERE ut.tenant_id = @tenantId;", new { tenantId }, tx);
        await connection.ExecuteAsync("DELETE t FROM tenants t WHERE t.id = @tenantId;", new { tenantId }, tx);

        if (exclusiveUserIds.Count > 0)
        {
            await connection.ExecuteAsync("DELETE FROM auth_login_attempts WHERE user_id IN @exclusiveUserIds;", new { exclusiveUserIds }, tx);
            await connection.ExecuteAsync("DELETE FROM app_users WHERE id IN @exclusiveUserIds;", new { exclusiveUserIds }, tx);
        }

        tx.Commit();
        return true;
    }

    public async Task<UserPurgePreview?> GetUserPurgePreviewAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            u.id AS UserId,
            u.email AS Email,
            u.is_active AS IsActive,
            u.is_global_admin AS IsGlobalAdmin,
            (SELECT COUNT(*) FROM user_tenants ut WHERE ut.user_id = u.id) AS MembershipCount,
            (SELECT COUNT(*) FROM payment_executions pe WHERE pe.user_id = u.id) AS PaymentExecutionsCount,
            (SELECT COUNT(*) FROM recurring_payment_templates rpt WHERE rpt.user_id = u.id) AS RecurringTemplatesCount,
            (SELECT COUNT(*) FROM account_monthly_balances amb WHERE amb.user_id = u.id) AS AccountBalancesCount,
            ((SELECT COUNT(*) FROM registered_bank_accounts rba WHERE rba.user_id = u.id)
             + (SELECT COUNT(*) FROM registered_credit_cards rcc WHERE rcc.user_id = u.id)) AS RegisteredSourcesCount,
            (SELECT COUNT(*) FROM auth_login_attempts ala WHERE ala.user_id = u.id) AS LoginAttemptsCount
        FROM app_users u
        WHERE u.id = @userId
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<UserPurgePreview>(sql, new { userId });
    }

    public async Task<bool> PurgeUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var userExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM app_users WHERE id = @userId);",
            new { userId },
            tx);
        if (!userExists)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(
            """
            DELETE pc
            FROM payment_corrections pc
            WHERE pc.user_id = @userId
               OR pc.payment_execution_id IN (
                    SELECT pe.id
                    FROM payment_executions pe
                    WHERE pe.user_id = @userId
                       OR pe.template_id IN (SELECT rpt.id FROM recurring_payment_templates rpt WHERE rpt.user_id = @userId)
                       OR pe.mapped_template_id IN (SELECT rpt.id FROM recurring_payment_templates rpt WHERE rpt.user_id = @userId)
               );
            """,
            new { userId },
            tx);
        await connection.ExecuteAsync(
            """
            DELETE ptm
            FROM payment_template_mappings ptm
            WHERE ptm.user_id = @userId
               OR ptm.execution_id IN (SELECT pe.id FROM payment_executions pe WHERE pe.user_id = @userId)
               OR ptm.template_id IN (SELECT rpt.id FROM recurring_payment_templates rpt WHERE rpt.user_id = @userId);
            """,
            new { userId },
            tx);
        await connection.ExecuteAsync(
            """
            DELETE pe
            FROM payment_executions pe
            WHERE pe.user_id = @userId
               OR pe.template_id IN (SELECT rpt.id FROM recurring_payment_templates rpt WHERE rpt.user_id = @userId)
               OR pe.mapped_template_id IN (SELECT rpt.id FROM recurring_payment_templates rpt WHERE rpt.user_id = @userId);
            """,
            new { userId },
            tx);
        await connection.ExecuteAsync("DELETE rpt FROM recurring_payment_templates rpt WHERE rpt.user_id = @userId;", new { userId }, tx);
        await connection.ExecuteAsync("DELETE rba FROM registered_bank_accounts rba WHERE rba.user_id = @userId;", new { userId }, tx);
        await connection.ExecuteAsync("DELETE rcc FROM registered_credit_cards rcc WHERE rcc.user_id = @userId;", new { userId }, tx);
        await connection.ExecuteAsync("DELETE amb FROM account_monthly_balances amb WHERE amb.user_id = @userId;", new { userId }, tx);
        await connection.ExecuteAsync("DELETE ut FROM user_tenants ut WHERE ut.user_id = @userId;", new { userId }, tx);
        await connection.ExecuteAsync("DELETE FROM auth_login_attempts WHERE user_id = @userId;", new { userId }, tx);
        await connection.ExecuteAsync("UPDATE app_users SET preferred_tenant_id = NULL WHERE preferred_tenant_id IN (SELECT tenant_id FROM user_tenants WHERE user_id = @userId);", new { userId }, tx);
        await connection.ExecuteAsync("DELETE FROM app_users WHERE id = @userId;", new { userId }, tx);

        tx.Commit();
        return true;
    }

    public async Task<string?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT `value` FROM app_settings WHERE `key` = @key LIMIT 1;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(sql, new { key });
    }

    public async Task<bool> SetAppSettingAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO app_settings(`key`, `value`, updated_utc)
        VALUES(@key, @value, UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE
            `value` = VALUES(`value`),
            updated_utc = UTC_TIMESTAMP(6);
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { key, value }) > 0;
    }
}
