using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Data.Repositories;

public interface IAccountMonthlyBalanceRepository
{
    Task UpsertAsync(AccountMonthlyBalance balance, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMonthlyBalance>> ListByYearAsync(long userId, long tenantId, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountMonthlyBalance>> ListByTenantYearAsync(long tenantId, int year, CancellationToken cancellationToken = default);
}

public sealed class AccountMonthlyBalanceRepository(
    IDbConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> databaseOptions) : IAccountMonthlyBalanceRepository
{
    private bool IsSqlite => ProviderDbConnectionFactory.NormalizeProvider(databaseOptions.Value.Provider) == DatabaseProviders.Sqlite;

    public async Task UpsertAsync(AccountMonthlyBalance balance, CancellationToken cancellationToken = default)
    {
        var sql = IsSqlite
            ? """
              INSERT INTO account_monthly_balances
              (user_id, tenant_id, account_number, year, month, opening_balance, closing_balance, source_reference, created_utc, updated_utc)
              VALUES
              (@UserId, @TenantId, @AccountNumber, @Year, @Month, @OpeningBalance, @ClosingBalance, @SourceReference, STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'), STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'))
              ON CONFLICT(tenant_id, account_number, year, month) DO UPDATE SET
                  user_id = excluded.user_id,
                  opening_balance = excluded.opening_balance,
                  closing_balance = excluded.closing_balance,
                  source_reference = excluded.source_reference,
                  updated_utc = STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now');
              """
            : """
              INSERT INTO account_monthly_balances
              (user_id, tenant_id, account_number, year, month, opening_balance, closing_balance, source_reference, created_utc, updated_utc)
              VALUES
              (@UserId, @TenantId, @AccountNumber, @Year, @Month, @OpeningBalance, @ClosingBalance, @SourceReference, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
              ON DUPLICATE KEY UPDATE
                  opening_balance = VALUES(opening_balance),
                  closing_balance = VALUES(closing_balance),
                  source_reference = VALUES(source_reference),
                  updated_utc = UTC_TIMESTAMP(6);
              """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, balance);
    }

    public async Task<IReadOnlyList<AccountMonthlyBalance>> ListByYearAsync(long userId, long tenantId, int year, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, user_id AS UserId, tenant_id AS TenantId, account_number AS AccountNumber, year, month, opening_balance AS OpeningBalance,
               closing_balance AS ClosingBalance, source_reference AS SourceReference,
               created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
        FROM account_monthly_balances
        WHERE user_id = @userId AND tenant_id = @tenantId AND year = @year
        ORDER BY account_number, month;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountMonthlyBalance>(sql, new { userId, tenantId, year });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AccountMonthlyBalance>> ListByTenantYearAsync(long tenantId, int year, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, user_id AS UserId, tenant_id AS TenantId, account_number AS AccountNumber, year, month, opening_balance AS OpeningBalance,
               closing_balance AS ClosingBalance, source_reference AS SourceReference,
               created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
        FROM account_monthly_balances
        WHERE tenant_id = @tenantId AND year = @year
        ORDER BY account_number, month;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountMonthlyBalance>(sql, new { tenantId, year });
        return rows.ToList();
    }
}
