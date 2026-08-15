using FinancialPlanningApp.Web.Data.Models;

namespace FinancialPlanningApp.Web.Data.Repositories;

public sealed class ExecutionMatch
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class DuplicateExecutionCandidate
{
    public long Id { get; set; }
    public DateOnly ExecutionDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
}

public interface IPaymentTrackingRepository
{
    Task<long> AddExecutionAsync(PaymentExecution execution, CancellationToken cancellationToken = default);
    Task<long> AddCorrectionAsync(PaymentCorrection correction, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsBySourceReferenceAsync(long userId, long tenantId, string? sourceReference, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListDescriptionsForExecutionAmountDateAsync(long userId, long tenantId, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default);
    Task<string?> GetExecutionNotesAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default);
    Task<bool> UpdateExecutionNotesAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string description, string notes, CancellationToken cancellationToken = default);
    Task<string?> GetExecutionNotesBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default);
    Task<bool> UpdateExecutionNotesBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, string notes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionMatch>> ListExecutionsBySourceDateAmountAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default);
    Task<bool> UpdateExecutionNotesByIdAsync(long userId, long tenantId, long executionId, string notes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DuplicateExecutionCandidate>> FindDuplicateCandidatesAsync(long userId, long tenantId, string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default);
}
