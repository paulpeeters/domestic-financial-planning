namespace FinancialPlanningApp.Web.Services.Imports;

public sealed class ImportedTransaction
{
    public DateOnly ExecutionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Transfer";
    public string SourceReference { get; set; } = string.Empty;
    public bool IsInformational { get; set; }
    public string? InfoType { get; set; }
    public string? SourceSequence { get; set; }
    public string? SourceAccountNumber { get; set; }
    public string? SourceCardNumber { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyAccount { get; set; }
    public string? CounterpartyBic { get; set; }
    public string? AdditionalContext { get; set; }
}
