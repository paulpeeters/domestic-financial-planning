using Dapper;
using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Infrastructure.Database;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class PaymentTrackingRepository(IDbConnectionFactory connectionFactory) : IPaymentTrackingRepository
{
    public async Task<long> AddExecutionAsync(PaymentExecution execution, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO payment_executions
        (user_id, tenant_id, template_id, execution_date, description, payment_method, amount, source_type, source_reference, source_sequence, source_account_number, source_card_number, notes, execution_type, mapping_status, mapped_template_id, mapped_period_year, mapped_period_month, created_utc)
        VALUES
        (@UserId, @TenantId, @TemplateId, @ExecutionDate, @Description, @PaymentMethod, @Amount, @SourceType, @SourceReference, @SourceSequence, @SourceAccountNumber, @SourceCardNumber, @Notes, @ExecutionType, @MappingStatus, @MappedTemplateId, @MappedPeriodYear, @MappedPeriodMonth, UTC_TIMESTAMP(6));
        SELECT LAST_INSERT_ID();
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, execution);
    }

    public async Task<long> AddCorrectionAsync(PaymentCorrection correction, CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO payment_corrections
        (user_id, tenant_id, payment_execution_id, correction_type, amount_delta, reason, created_utc)
        VALUES
        (@UserId, @TenantId, @PaymentExecutionId, @CorrectionType, @AmountDelta, @Reason, UTC_TIMESTAMP(6));
        SELECT LAST_INSERT_ID();
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, correction);
    }

    public async Task<bool> ExecutionExistsAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM payment_executions
            WHERE user_id = @userId
              AND tenant_id = @tenantId
              AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
              AND execution_date = @executionDate
              AND amount = @amount
              AND description = @description
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId, sourceReference, executionDate, amount, description });
    }

    public async Task<bool> ExecutionExistsBySourceReferenceAsync(long userId, long tenantId, string? sourceReference, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM payment_executions
            WHERE user_id = @userId
              AND tenant_id = @tenantId
              AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId, sourceReference });
    }

    public async Task<bool> ExecutionExistsBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS(
            SELECT 1
            FROM payment_executions
            WHERE user_id = @userId
              AND tenant_id = @tenantId
              AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
              AND execution_date = @executionDate
              AND amount = @amount
        );
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(sql, new { userId, tenantId, sourceReference, executionDate, amount });
    }

    public async Task<IReadOnlyList<string>> ListDescriptionsForExecutionAmountDateAsync(long userId, long tenantId, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT description
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND execution_date = @executionDate
          AND amount = @amount;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(sql, new { userId, tenantId, executionDate, amount });
        return rows.ToList();
    }

    public async Task<string?> GetExecutionNotesAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT notes
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
          AND execution_date = @executionDate
          AND amount = @amount
          AND description = @description
        ORDER BY id DESC
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(sql, new { userId, tenantId, sourceReference, executionDate, amount, description });
    }

    public async Task<bool> UpdateExecutionNotesAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string description, string notes, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE payment_executions
        SET notes = @notes
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
          AND execution_date = @executionDate
          AND amount = @amount
          AND description = @description;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, tenantId, sourceReference, executionDate, amount, description, notes }) > 0;
    }

    public async Task<string?> GetExecutionNotesBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT notes
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
          AND execution_date = @executionDate
          AND amount = @amount
        ORDER BY id DESC
        LIMIT 1;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(sql, new { userId, tenantId, sourceReference, executionDate, amount });
    }

    public async Task<bool> UpdateExecutionNotesBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string notes, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE payment_executions
        SET notes = @notes
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
          AND execution_date = @executionDate
          AND amount = @amount;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, tenantId, sourceReference, executionDate, amount, notes }) > 0;
    }

    public async Task<IReadOnlyList<ExecutionMatch>> ListExecutionsBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, description, notes
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND COALESCE(source_reference, '') = COALESCE(@sourceReference, '')
          AND execution_date = @executionDate
          AND amount = @amount
        ORDER BY id DESC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ExecutionMatch>(sql, new { userId, tenantId, sourceReference, executionDate, amount });
        return rows.ToList();
    }

    public async Task<bool> UpdateExecutionNotesByIdAsync(long userId, long tenantId, long executionId, string notes, CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE payment_executions
        SET notes = @notes
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND id = @executionId;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { userId, tenantId, executionId, notes }) > 0;
    }

    public async Task<IReadOnlyList<DuplicateExecutionCandidate>> FindDuplicateCandidatesAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT id, execution_date AS ExecutionDate, amount, description, source_type AS SourceType, source_reference AS SourceReference
        FROM payment_executions
        WHERE user_id = @userId
          AND tenant_id = @tenantId
          AND (
            (COALESCE(source_reference, '') = COALESCE(@sourceReference, ''))
            OR (execution_date = @executionDate AND amount = @amount)
          )
        ORDER BY id DESC;
        """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DuplicateExecutionCandidate>(sql, new { userId, tenantId, sourceReference, executionDate, amount });
        return rows.ToList();
    }
}
