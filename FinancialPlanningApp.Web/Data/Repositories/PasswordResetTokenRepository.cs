using Dapper;
using FinancialPlanningApp.Web.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class PasswordResetTokenRepository(
    IDbConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> databaseOptions) : IPasswordResetTokenRepository
{
    private bool IsSqlite => ProviderDbConnectionFactory.NormalizeProvider(databaseOptions.Value.Provider) == DatabaseProviders.Sqlite;
    private string UtcNowSql => IsSqlite ? "STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')" : "UTC_TIMESTAMP(6)";

    public async Task<long> CreateAsync(long userId, string tokenHash, DateTime expiresUtc, string? requestIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        var sql = IsSqlite
            ? """
              INSERT INTO password_reset_tokens(user_id, token_hash, expires_utc, request_ip, user_agent)
              VALUES(@userId, @tokenHash, @expiresUtc, @requestIp, @userAgent);
              SELECT last_insert_rowid();
              """
            : """
              INSERT INTO password_reset_tokens(user_id, token_hash, expires_utc, request_ip, user_agent)
              VALUES(@userId, @tokenHash, @expiresUtc, @requestIp, @userAgent);
              SELECT LAST_INSERT_ID();
              """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, new { userId, tokenHash, expiresUtc, requestIp, userAgent });
    }

    public async Task<long?> GetValidUserIdAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = $"""
        SELECT user_id
        FROM password_reset_tokens
        WHERE token_hash = @tokenHash
          AND used_utc IS NULL
          AND expires_utc > {UtcNowSql}
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long?>(sql, new { tokenHash });
    }

    public async Task<bool> MarkUsedAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = $"""
        UPDATE password_reset_tokens
        SET used_utc = {UtcNowSql}
        WHERE token_hash = @tokenHash
          AND used_utc IS NULL;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tokenHash }) > 0;
    }

    public async Task ExpireOpenTokensAsync(long userId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
        UPDATE password_reset_tokens
        SET used_utc = {UtcNowSql}
        WHERE user_id = @userId
          AND used_utc IS NULL;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { userId });
    }
}
