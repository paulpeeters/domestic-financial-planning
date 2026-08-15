namespace FinancialPlanningApp.Web.Data.Models;

public sealed class PaymentCorrection
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long TenantId { get; set; }
    public long PaymentExecutionId { get; set; }
    public string CorrectionType { get; set; } = "Adjustment";
    public decimal AmountDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
