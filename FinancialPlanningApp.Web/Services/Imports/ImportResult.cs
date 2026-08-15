namespace FinancialPlanningApp.Web.Services.Imports;

public sealed class ImportDuplicateDetail
{
    public string Reason { get; init; } = string.Empty;
    public DateOnly IncomingDate { get; init; }
    public decimal IncomingAmount { get; init; }
    public string IncomingDescription { get; init; } = string.Empty;
    public string IncomingSourceType { get; init; } = string.Empty;
    public string? IncomingSourceReference { get; init; }
    public long ExistingId { get; init; }
    public string ExistingSourceType { get; init; } = string.Empty;
    public DateOnly ExistingDate { get; init; }
    public decimal ExistingAmount { get; init; }
    public string ExistingDescription { get; init; } = string.Empty;
    public string? ExistingSourceReference { get; init; }
}

public sealed class ImportResult
{
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
    public int DuplicateCount { get; init; }
    public int SameSourceDuplicateCount { get; init; }
    public int CrossSourceDuplicateCount { get; init; }
    public int InvalidCount { get; init; }
    public int InsertErrorCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<ImportDuplicateDetail> DuplicateDetails { get; init; } = [];
}
