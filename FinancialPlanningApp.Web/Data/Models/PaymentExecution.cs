namespace FinancialPlanningApp.Web.Data.Models;

public sealed class PaymentExecution
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long TenantId { get; set; }
    public long? TemplateId { get; set; }
    public DateOnly ExecutionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceType { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public string? SourceSequence { get; set; }
    public string? SourceAccountNumber { get; set; }
    public string? SourceCardNumber { get; set; }
    public string? Notes { get; set; }
    public string? ExecutionType { get; set; }
    public string MappingStatus { get; set; } = "UNMAPPED";
    public long? MappedTemplateId { get; set; }
    public int? MappedPeriodYear { get; set; }
    public int? MappedPeriodMonth { get; set; }
    public DateTime CreatedUtc { get; set; }
}
