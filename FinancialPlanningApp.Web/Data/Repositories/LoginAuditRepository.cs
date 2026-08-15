using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public interface ILoginAuditRepository
{
    Task AddAsync(string? email, long? userId, bool isSuccess, string? failureReason, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoginAttempt>> ListAsync(DateTime? fromUtc, DateTime? toUtc, string? email, bool? isSuccess, int limit, CancellationToken cancellationToken = default);
}

public sealed class LoginAuditRepository(IDbConnectionFactory connectionFactory) : ILoginAuditRepository
{
    public async Task AddAsync(string? email, long? userId, bool isSuccess, string? failureReason, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO auth_login_attempts(attempted_utc, email, user_id, is_success, failure_reason, ip_address, user_agent)
        VALUES(UTC_TIMESTAMP(6), @email, @userId, @isSuccess, @failureReason, @ipAddress, @userAgent);
        """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { email, userId, isSuccess, failureReason, ipAddress, userAgent });
    }

    public async Task<IReadOnlyList<LoginAttempt>> ListAsync(DateTime? fromUtc, DateTime? toUtc, string? email, bool? isSuccess, int limit, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            id,
            attempted_utc AS AttemptedUtc,
            email,
            user_id AS UserId,
            is_success AS IsSuccess,
            failure_reason AS FailureReason,
            ip_address AS IpAddress,
            user_agent AS UserAgent
        FROM auth_login_attempts
        WHERE (@fromUtc IS NULL OR attempted_utc >= @fromUtc)
          AND (@toUtc IS NULL OR attempted_utc <= @toUtc)
          AND (@email IS NULL OR email LIKE CONCAT('%', @email, '%'))
          AND (@isSuccess IS NULL OR is_success = @isSuccess)
        ORDER BY attempted_utc DESC, id DESC
        LIMIT @limit;
        """;

        var safeLimit = limit is < 1 or > 1000 ? 200 : limit;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LoginAttempt>(sql, new
        {
            fromUtc,
            toUtc,
            email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            isSuccess,
            limit = safeLimit
        });
        return rows.ToList();
    }
}
