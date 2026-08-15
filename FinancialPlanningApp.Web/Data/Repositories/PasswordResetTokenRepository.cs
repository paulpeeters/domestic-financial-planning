using Dapper;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class PasswordResetTokenRepository(IDbConnectionFactory connectionFactory) : IPasswordResetTokenRepository
{
    public async Task<long> CreateAsync(long userId, string tokenHash, DateTime expiresUtc, string? requestIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO password_reset_tokens(user_id, token_hash, expires_utc, request_ip, user_agent)
        VALUES(@userId, @tokenHash, @expiresUtc, @requestIp, @userAgent);
        SELECT LAST_INSERT_ID();
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, new { userId, tokenHash, expiresUtc, requestIp, userAgent });
    }

    public async Task<long?> GetValidUserIdAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT user_id
        FROM password_reset_tokens
        WHERE token_hash = @tokenHash
          AND used_utc IS NULL
          AND expires_utc > UTC_TIMESTAMP(6)
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long?>(sql, new { tokenHash });
    }

    public async Task<bool> MarkUsedAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE password_reset_tokens
        SET used_utc = UTC_TIMESTAMP(6)
        WHERE token_hash = @tokenHash
          AND used_utc IS NULL;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tokenHash }) > 0;
    }

    public async Task ExpireOpenTokensAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE password_reset_tokens
        SET used_utc = UTC_TIMESTAMP(6)
        WHERE user_id = @userId
          AND used_utc IS NULL;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { userId });
    }
}
