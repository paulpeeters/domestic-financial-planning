using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;

namespace FinancialPlanningApp.Web.Services.Payments;

public sealed class DuplicateMatchDetail
{
    public long ExistingId { get; init; }
    public string ExistingSourceType { get; init; } = string.Empty;
    public DateOnly ExistingDate { get; init; }
    public decimal ExistingAmount { get; init; }
    public string ExistingDescription { get; init; } = string.Empty;
    public string? ExistingSourceReference { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public interface IPaymentTrackingService
{
    Task<long> AddExecutionForCurrentUserAsync(PaymentExecution execution, CancellationToken cancellationToken = default);
    Task<long> AddCorrectionForCurrentUserAsync(PaymentCorrection correction, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsBySourceReferenceForCurrentUserAsync(string? sourceReference, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsBySourceDateAmountForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default);
    Task<bool> ExecutionExistsCrossSourceForCurrentUserAsync(DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default);
    Task<bool> TryEnrichDuplicateExecutionNotesForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string description, string additionalNotes, CancellationToken cancellationToken = default);
    Task<bool> TryEnrichDuplicateExecutionNotesBySourceDateAmountForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string descriptionHint, string additionalNotes, CancellationToken cancellationToken = default);
    Task<DuplicateMatchDetail?> FindDuplicateMatchForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default);
}
