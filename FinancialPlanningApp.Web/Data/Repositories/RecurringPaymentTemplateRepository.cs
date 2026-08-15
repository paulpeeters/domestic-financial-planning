using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class RecurringPaymentTemplateRepository(IDbConnectionFactory connectionFactory) : IRecurringPaymentTemplateRepository
{
    public async Task<(IReadOnlyList<RecurringPaymentTemplate> Items, int TotalCount)> ListByUserAsync(long userId, long tenantId, RecurringPaymentListQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, user_id AS UserId, tenant_id AS TenantId, description, display_order AS DisplayOrder, periodicity,
               payment_month AS PaymentMonth, payment_months AS PaymentMonths, payment_day AS PaymentDay, payment_lag_months AS PaymentLagMonths,
               payment_method AS PaymentMethod, matching_keywords AS MatchingKeywords,
               amount, amount_mode AS AmountMode, monthly_amounts_json AS MonthlyAmountsJson, normalized_monthly_amount AS NormalizedMonthlyAmount, active_from AS ActiveFrom,
               active_until AS ActiveUntil, is_active AS IsActive, created_utc AS CreatedUtc
        FROM recurring_payment_templates
        WHERE tenant_id = @tenantId
          AND (@includeInactive = TRUE OR is_active = TRUE)
          AND (@search IS NULL OR description LIKE CONCAT('%', @search, '%'))
        ORDER BY is_active DESC, display_order ASC, description ASC
        LIMIT @limit OFFSET @offset;

        SELECT COUNT(1)
        FROM recurring_payment_templates
        WHERE tenant_id = @tenantId
          AND (@includeInactive = TRUE OR is_active = TRUE)
          AND (@search IS NULL OR description LIKE CONCAT('%', @search, '%'));
        """;

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;
        var offset = (pageNumber - 1) * pageSize;
        var parameters = new
        {
            userId,
            tenantId,
            includeInactive = query.IncludeInactive,
            search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            limit = pageSize,
            offset
        };

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(sql, parameters);
        var items = (await multi.ReadAsync<RecurringPaymentTemplate>()).ToList();
        var count = await multi.ReadSingleAsync<int>();
        return (items, count);
    }

    public async Task<RecurringPaymentTemplate?> GetByIdAsync(long userId, long tenantId, long templateId, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, user_id AS UserId, tenant_id AS TenantId, description, display_order AS DisplayOrder, periodicity,
               payment_month AS PaymentMonth, payment_months AS PaymentMonths, payment_day AS PaymentDay, payment_lag_months AS PaymentLagMonths,
               payment_method AS PaymentMethod, matching_keywords AS MatchingKeywords,
               amount, amount_mode AS AmountMode, monthly_amounts_json AS MonthlyAmountsJson, normalized_monthly_amount AS NormalizedMonthlyAmount, active_from AS ActiveFrom,
               active_until AS ActiveUntil, is_active AS IsActive, created_utc AS CreatedUtc
        FROM recurring_payment_templates
        WHERE tenant_id = @tenantId AND id = @templateId
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<RecurringPaymentTemplate>(sql, new { tenantId, templateId });
    }

    public async Task<IReadOnlyList<RecurringPaymentTemplate>> ListAllByUserAsync(long userId, long tenantId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, user_id AS UserId, tenant_id AS TenantId, description, display_order AS DisplayOrder, periodicity,
               payment_month AS PaymentMonth, payment_months AS PaymentMonths, payment_day AS PaymentDay, payment_lag_months AS PaymentLagMonths,
               payment_method AS PaymentMethod, matching_keywords AS MatchingKeywords,
               amount, amount_mode AS AmountMode, monthly_amounts_json AS MonthlyAmountsJson, normalized_monthly_amount AS NormalizedMonthlyAmount, active_from AS ActiveFrom,
               active_until AS ActiveUntil, is_active AS IsActive, created_utc AS CreatedUtc
        FROM recurring_payment_templates
        WHERE tenant_id = @tenantId
          AND (@includeInactive = TRUE OR is_active = TRUE)
        ORDER BY is_active DESC, display_order ASC, description ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RecurringPaymentTemplate>(sql, new { tenantId, includeInactive });
        return rows.ToList();
    }

    public async Task<long> CreateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO recurring_payment_templates
        (user_id, tenant_id, description, display_order, periodicity, payment_month, payment_months, payment_day, payment_lag_months, payment_method, matching_keywords, amount, amount_mode, monthly_amounts_json, normalized_monthly_amount, active_from, active_until, is_active, created_utc)
        VALUES
        (@UserId, @TenantId, @Description, @DisplayOrder, @Periodicity, @PaymentMonth, @PaymentMonths, @PaymentDay, @PaymentLagMonths, @PaymentMethod, @MatchingKeywords, @Amount, @AmountMode, @MonthlyAmountsJson, @NormalizedMonthlyAmount, @ActiveFrom, @ActiveUntil, @IsActive, UTC_TIMESTAMP(6));

        SELECT LAST_INSERT_ID();
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, template);
    }

    public async Task<bool> UpdateAsync(RecurringPaymentTemplate template, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE recurring_payment_templates
        SET description = @Description,
            display_order = @DisplayOrder,
            periodicity = @Periodicity,
            payment_month = @PaymentMonth,
            payment_months = @PaymentMonths,
            payment_day = @PaymentDay,
            payment_lag_months = @PaymentLagMonths,
            payment_method = @PaymentMethod,
            matching_keywords = @MatchingKeywords,
            amount = @Amount,
            amount_mode = @AmountMode,
            monthly_amounts_json = @MonthlyAmountsJson,
            normalized_monthly_amount = @NormalizedMonthlyAmount,
            active_from = @ActiveFrom,
            active_until = @ActiveUntil,
            is_active = @IsActive
        WHERE tenant_id = @TenantId AND id = @Id;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, template) > 0;
    }

    public async Task<bool> UpdateDisplayOrdersAsync(long userId, long tenantId, IReadOnlyList<long> templateIds, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE recurring_payment_templates
        SET display_order = @displayOrder
        WHERE tenant_id = @tenantId AND id = @templateId;
        """;

        var orderedIds = templateIds.Distinct().ToList();
        if (orderedIds.Count == 0)
        {
            return true;
        }

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var displayOrder = 10;
        foreach (var templateId in orderedIds)
        {
            await connection.ExecuteAsync(sql, new { tenantId, templateId, displayOrder }, transaction);
            displayOrder += 10;
        }

        transaction.Commit();
        return true;
    }

    public async Task<bool> SetActiveStateAsync(long userId, long tenantId, long templateId, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE recurring_payment_templates SET is_active = @isActive WHERE tenant_id = @tenantId AND id = @templateId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, templateId, isActive }) > 0;
    }

    public async Task<bool> DeleteAsync(long userId, long tenantId, long templateId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM recurring_payment_templates WHERE tenant_id = @tenantId AND id = @templateId;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { tenantId, templateId }) > 0;
    }
}
