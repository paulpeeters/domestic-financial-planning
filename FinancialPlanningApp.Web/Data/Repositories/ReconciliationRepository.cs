using FinancialPlanningApp.Web.Data.Models;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class PaymentCandidate
{
    public long Id { get; set; }
    public DateOnly ExecutionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public int DayDistance { get; set; }
}

public sealed class MonthlyPaidRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal DepositedTotal { get; set; }
}

public sealed class ReconciliationExecutionRow
{
    public long Id { get; set; }
    public DateOnly ExecutionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? MappingStatus { get; set; }
    public string? ExecutionType { get; set; }
    public long? MappedTemplateId { get; set; }
    public int? MappedPeriodYear { get; set; }
    public int? MappedPeriodMonth { get; set; }
}

public sealed class TemplateActualTotalRow
{
    public long TemplateId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class MappedExpenseExecutionRow
{
    public long Id { get; set; }
    public DateOnly ExecutionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string ExecutionType { get; set; } = string.Empty;
    public long? MappedTemplateId { get; set; }
    public int MappedPeriodYear { get; set; }
    public int MappedPeriodMonth { get; set; }
}

public interface IReconciliationRepository
{
    Task<IReadOnlyList<PaymentCandidate>> FindCandidatesAsync(long userId, long tenantId, DateOnly fromDate, DateOnly toDate, DateOnly dueDate, CancellationToken cancellationToken = default);
    Task<bool> IsTemplateMappedForPeriodAsync(long userId, long tenantId, long templateId, int year, int month, CancellationToken cancellationToken = default);
    Task<bool> MapExecutionAsync(long userId, long tenantId, long executionId, long templateId, int year, int month, string mappedBy, string? confidenceNote, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlyPaidRow>> GetMonthlyPaidTotalsAsync(long userId, long tenantId, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlyPaidRow>> GetMonthlyPaidTotalsForTenantAsync(long tenantId, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationExecutionRow>> GetExecutionsForReviewAsync(long userId, long tenantId, int year, int? month, bool onlyUnmapped, string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationExecutionRow>> GetExecutionsForReviewForTenantAsync(long tenantId, int year, int? month, bool onlyUnmapped, string? search, CancellationToken cancellationToken = default);
    Task<bool> SetExecutionMappingAsync(long userId, long tenantId, long executionId, string executionType, string mappingStatus, long? mappedTemplateId, int? mappedPeriodYear, int? mappedPeriodMonth, CancellationToken cancellationToken = default);
    Task<bool> UnmapExecutionAsync(long userId, long tenantId, long executionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateActualTotalRow>> GetTemplateActualTotalsAsync(long userId, long tenantId, int fromYear, int toYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateActualTotalRow>> GetTemplateActualTotalsForTenantAsync(long tenantId, int fromYear, int toYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MappedExpenseExecutionRow>> GetMappedExpenseExecutionsForPeriodAsync(long userId, long tenantId, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MappedExpenseExecutionRow>> GetMappedExpenseExecutionsForTenantPeriodAsync(long tenantId, int year, int month, CancellationToken cancellationToken = default);
}
