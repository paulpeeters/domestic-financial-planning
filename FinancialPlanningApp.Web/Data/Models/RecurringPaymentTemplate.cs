namespace FinancialPlanningApp.Web.Data.Models;

public sealed class RecurringPaymentTemplate
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long TenantId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string Periodicity { get; set; } = "Monthly";
    public int? PaymentMonth { get; set; }
    public string? PaymentMonths { get; set; }
    public int? PaymentDay { get; set; }
    public string PaymentMethod { get; set; } = "Transfer";
    public string? MatchingKeywords { get; set; }
    public decimal Amount { get; set; }
    public string AmountMode { get; set; } = "Fixed";
    public string? MonthlyAmountsJson { get; set; }
    public decimal NormalizedMonthlyAmount { get; set; }
    public int PaymentLagMonths { get; set; }
    public DateOnly ActiveFrom { get; set; }
    public DateOnly? ActiveUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
}
