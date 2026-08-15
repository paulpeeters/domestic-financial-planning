using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class ReconciliationRepository(IDbConnectionFactory connectionFactory) : IReconciliationRepository
{
    public async Task<IReadOnlyList<PaymentCandidate>> FindCandidatesAsync(long userId, long tenantId, DateOnly fromDate, DateOnly toDate, DateOnly dueDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, execution_date AS ExecutionDate, description, amount, payment_method AS PaymentMethod,
               ABS(DATEDIFF(execution_date, @dueDate)) AS DayDistance
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND execution_date BETWEEN @fromDate AND @toDate
          AND amount < 0
          AND mapping_status = 'UNMAPPED'
        ORDER BY DayDistance ASC, execution_date ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PaymentCandidate>(sql, new { userId, tenantId, fromDate, toDate, dueDate });
        return rows.ToList();
    }

    public async Task<bool> IsTemplateMappedForPeriodAsync(long userId, long tenantId, long templateId, int year, int month, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1 FROM payment_template_mappings
            WHERE user_id = @userId AND tenant_id = @tenantId AND template_id = @templateId AND period_year = @year AND period_month = @month
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId, templateId, year, month });
    }

    public async Task<bool> MapExecutionAsync(long userId, long tenantId, long executionId, long templateId, int year, int month, string mappedBy, string? confidenceNote, CancellationToken cancellationToken = default)
    {
        const string updateSql = """
        UPDATE payment_executions
        SET mapping_status = 'MAPPED', mapped_template_id = @templateId, mapped_period_year = @year, mapped_period_month = @month, execution_type = 'RECURRING_PAYMENT'
        WHERE user_id = @userId AND tenant_id = @tenantId AND id = @executionId;
        """;

        const string insertSql = """
        INSERT INTO payment_template_mappings(user_id, tenant_id, execution_id, template_id, period_year, period_month, mapped_by, mapped_utc, confidence_note)
        VALUES(@userId, @tenantId, @executionId, @templateId, @year, @month, @mappedBy, UTC_TIMESTAMP(6), @confidenceNote)
        ON DUPLICATE KEY UPDATE template_id = VALUES(template_id), period_year = VALUES(period_year), period_month = VALUES(period_month), mapped_by = VALUES(mapped_by), mapped_utc = VALUES(mapped_utc), confidence_note = VALUES(confidence_note);
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        var changed = await connection.ExecuteAsync(updateSql, new { userId, tenantId, executionId, templateId, year, month }, tx);
        if (changed == 0)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(insertSql, new { userId, tenantId, executionId, templateId, year, month, mappedBy, confidenceNote }, tx);
        tx.Commit();
        return true;
    }

    public async Task<IReadOnlyList<MonthlyPaidRow>> GetMonthlyPaidTotalsAsync(long userId, long tenantId, int year, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END AS Year,
            CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END AS Month,
            SUM(CASE
                WHEN mapping_status = 'MAPPED' AND execution_type IN ('PLANNED_DEPOSIT', 'EXTRA_DEPOSIT', 'CARD_SETTLEMENT') THEN 0
                WHEN mapping_status = 'MAPPED' THEN amount
                ELSE 0
            END) AS PaidTotal,
            SUM(CASE
                WHEN mapping_status = 'MAPPED' AND execution_type IN ('PLANNED_DEPOSIT', 'EXTRA_DEPOSIT') THEN amount
                ELSE 0
            END) AS DepositedTotal
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND mapping_status = 'MAPPED'
          AND (CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END) = @year
        GROUP BY
            CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END,
            CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END
        ORDER BY Year, Month;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MonthlyPaidRow>(sql, new { userId, tenantId, year });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MonthlyPaidRow>> GetMonthlyPaidTotalsForTenantAsync(long tenantId, int year, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END AS Year,
            CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END AS Month,
            SUM(CASE
                WHEN mapping_status = 'MAPPED' AND execution_type IN ('PLANNED_DEPOSIT', 'EXTRA_DEPOSIT', 'CARD_SETTLEMENT') THEN 0
                WHEN mapping_status = 'MAPPED' THEN amount
                ELSE 0
            END) AS PaidTotal,
            SUM(CASE
                WHEN mapping_status = 'MAPPED' AND execution_type IN ('PLANNED_DEPOSIT', 'EXTRA_DEPOSIT') THEN amount
                ELSE 0
            END) AS DepositedTotal
        FROM payment_executions
        WHERE tenant_id = @tenantId
          AND mapping_status = 'MAPPED'
          AND (CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END) = @year
        GROUP BY
            CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END,
            CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END
        ORDER BY Year, Month;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MonthlyPaidRow>(sql, new { tenantId, year });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ReconciliationExecutionRow>> GetExecutionsForReviewAsync(long userId, long tenantId, int year, int? month, bool onlyUnmapped, string? search, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT e.id, e.execution_date AS ExecutionDate, e.description, e.notes, e.amount, e.payment_method AS PaymentMethod, e.source_type AS SourceType,
               e.mapping_status AS MappingStatus, e.execution_type AS ExecutionType, e.mapped_template_id AS MappedTemplateId,
               e.mapped_period_year AS MappedPeriodYear, e.mapped_period_month AS MappedPeriodMonth
        FROM payment_executions e
        LEFT JOIN recurring_payment_templates t
          ON t.user_id = e.user_id
         AND t.tenant_id = e.tenant_id
         AND t.id = e.mapped_template_id
        WHERE e.user_id = @userId
          AND e.tenant_id = @tenantId
          AND YEAR(e.execution_date) = @year
          AND (@month IS NULL OR MONTH(e.execution_date) = @month)
          AND (@onlyUnmapped = FALSE OR e.mapping_status = 'UNMAPPED')
          AND (
              @search IS NULL
              OR e.description LIKE CONCAT('%', @search, '%')
              OR e.notes LIKE CONCAT('%', @search, '%')
              OR t.description LIKE CONCAT('%', @search, '%')
              OR t.matching_keywords LIKE CONCAT('%', @search, '%')
          )
        ORDER BY e.execution_date ASC, e.id ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ReconciliationExecutionRow>(sql, new
        {
            userId,
            tenantId,
            year,
            month,
            onlyUnmapped,
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
        });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ReconciliationExecutionRow>> GetExecutionsForReviewForTenantAsync(long tenantId, int year, int? month, bool onlyUnmapped, string? search, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT e.id, e.execution_date AS ExecutionDate, e.description, e.notes, e.amount, e.payment_method AS PaymentMethod, e.source_type AS SourceType,
               e.mapping_status AS MappingStatus, e.execution_type AS ExecutionType, e.mapped_template_id AS MappedTemplateId,
               e.mapped_period_year AS MappedPeriodYear, e.mapped_period_month AS MappedPeriodMonth
        FROM payment_executions e
        LEFT JOIN recurring_payment_templates t
          ON t.tenant_id = e.tenant_id
         AND t.id = e.mapped_template_id
        WHERE e.tenant_id = @tenantId
          AND YEAR(e.execution_date) = @year
          AND (@month IS NULL OR MONTH(e.execution_date) = @month)
          AND (@onlyUnmapped = FALSE OR e.mapping_status = 'UNMAPPED')
          AND (
              @search IS NULL
              OR e.description LIKE CONCAT('%', @search, '%')
              OR e.notes LIKE CONCAT('%', @search, '%')
              OR t.description LIKE CONCAT('%', @search, '%')
              OR t.matching_keywords LIKE CONCAT('%', @search, '%')
          )
        ORDER BY e.execution_date ASC, e.id ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ReconciliationExecutionRow>(sql, new
        {
            tenantId,
            year,
            month,
            onlyUnmapped,
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
        });
        return rows.ToList();
    }

    public async Task<bool> SetExecutionMappingAsync(long userId, long tenantId, long executionId, string executionType, string mappingStatus, long? mappedTemplateId, int? mappedPeriodYear, int? mappedPeriodMonth, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE payment_executions
        SET execution_type = @executionType,
            mapping_status = @mappingStatus,
            mapped_template_id = @mappedTemplateId,
            mapped_period_year = @mappedPeriodYear,
            mapped_period_month = @mappedPeriodMonth
        WHERE user_id = @userId AND tenant_id = @tenantId AND id = @executionId;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, tenantId, executionId, executionType, mappingStatus, mappedTemplateId, mappedPeriodYear, mappedPeriodMonth }) > 0;
    }

    public async Task<bool> UnmapExecutionAsync(long userId, long tenantId, long executionId, CancellationToken cancellationToken = default)
    {
        const string updateSql = """
        UPDATE payment_executions
        SET mapping_status = 'UNMAPPED',
            execution_type = NULL,
            mapped_template_id = NULL,
            mapped_period_year = NULL,
            mapped_period_month = NULL
        WHERE user_id = @userId AND tenant_id = @tenantId AND id = @executionId;
        """;

        const string deleteSql = "DELETE FROM payment_template_mappings WHERE user_id = @userId AND tenant_id = @tenantId AND execution_id = @executionId;";

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        var changed = await connection.ExecuteAsync(updateSql, new { userId, tenantId, executionId }, tx);
        await connection.ExecuteAsync(deleteSql, new { userId, tenantId, executionId }, tx);
        tx.Commit();
        return changed > 0;
    }

    public async Task<IReadOnlyList<TemplateActualTotalRow>> GetTemplateActualTotalsAsync(long userId, long tenantId, int fromYear, int toYear, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            mapped_template_id AS TemplateId,
            mapped_period_year AS Year,
            mapped_period_month AS Month,
            SUM(amount) AS TotalAmount
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND mapping_status = 'MAPPED'
          AND execution_type = 'RECURRING_PAYMENT'
          AND mapped_template_id IS NOT NULL
          AND mapped_period_year IS NOT NULL
          AND mapped_period_month IS NOT NULL
          AND mapped_period_year BETWEEN @fromYear AND @toYear
        GROUP BY mapped_template_id, mapped_period_year, mapped_period_month
        ORDER BY mapped_template_id, mapped_period_year, mapped_period_month;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TemplateActualTotalRow>(sql, new { userId, tenantId, fromYear, toYear });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TemplateActualTotalRow>> GetTemplateActualTotalsForTenantAsync(long tenantId, int fromYear, int toYear, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            mapped_template_id AS TemplateId,
            mapped_period_year AS Year,
            mapped_period_month AS Month,
            SUM(amount) AS TotalAmount
        FROM payment_executions
        WHERE tenant_id = @tenantId
          AND mapping_status = 'MAPPED'
          AND execution_type = 'RECURRING_PAYMENT'
          AND mapped_template_id IS NOT NULL
          AND mapped_period_year IS NOT NULL
          AND mapped_period_month IS NOT NULL
          AND mapped_period_year BETWEEN @fromYear AND @toYear
        GROUP BY mapped_template_id, mapped_period_year, mapped_period_month
        ORDER BY mapped_template_id, mapped_period_year, mapped_period_month;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TemplateActualTotalRow>(sql, new { tenantId, fromYear, toYear });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MappedExpenseExecutionRow>> GetMappedExpenseExecutionsForPeriodAsync(long userId, long tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, execution_date AS ExecutionDate, description, amount, payment_method AS PaymentMethod,
               execution_type AS ExecutionType, mapped_template_id AS MappedTemplateId,
               CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END AS MappedPeriodYear,
               CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END AS MappedPeriodMonth
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND mapping_status = 'MAPPED'
          AND execution_type NOT IN ('PLANNED_DEPOSIT', 'EXTRA_DEPOSIT', 'CARD_SETTLEMENT')
          AND (CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END) = @year
          AND (CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END) = @month
        ORDER BY execution_date ASC, id ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MappedExpenseExecutionRow>(sql, new { userId, tenantId, year, month });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MappedExpenseExecutionRow>> GetMappedExpenseExecutionsForTenantPeriodAsync(long tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, execution_date AS ExecutionDate, description, amount, payment_method AS PaymentMethod,
               execution_type AS ExecutionType, mapped_template_id AS MappedTemplateId,
               CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END AS MappedPeriodYear,
               CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END AS MappedPeriodMonth
        FROM payment_executions
        WHERE tenant_id = @tenantId
          AND mapping_status = 'MAPPED'
          AND execution_type NOT IN ('PLANNED_DEPOSIT', 'EXTRA_DEPOSIT', 'CARD_SETTLEMENT')
          AND (CASE WHEN mapped_period_year IS NOT NULL THEN mapped_period_year ELSE YEAR(execution_date) END) = @year
          AND (CASE WHEN mapped_period_month IS NOT NULL THEN mapped_period_month ELSE MONTH(execution_date) END) = @month
        ORDER BY execution_date ASC, id ASC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MappedExpenseExecutionRow>(sql, new { tenantId, year, month });
        return rows.ToList();
    }
}
