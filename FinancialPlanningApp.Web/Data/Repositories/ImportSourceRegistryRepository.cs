using Dapper;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public interface IImportSourceRegistryRepository
{
    Task<bool> IsRegisteredBankAccountAsync(long userId, long tenantId, string accountNumber, CancellationToken cancellationToken = default);
    Task<bool> IsRegisteredCreditCardAsync(long userId, long tenantId, string cardNumber, CancellationToken cancellationToken = default);
}

public sealed class ImportSourceRegistryRepository(IDbConnectionFactory connectionFactory) : IImportSourceRegistryRepository
{
    public async Task<bool> IsRegisteredBankAccountAsync(long userId, long tenantId, string accountNumber, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM registered_bank_accounts
            WHERE user_id = @userId
              AND tenant_id = @tenantId
              AND is_active = 1
              AND account_number = @accountNumber
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId, accountNumber });
    }

    public async Task<bool> IsRegisteredCreditCardAsync(long userId, long tenantId, string cardNumber, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM registered_credit_cards
            WHERE user_id = @userId
              AND tenant_id = @tenantId
              AND is_active = 1
              AND card_number = @cardNumber
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId, cardNumber });
    }
}
