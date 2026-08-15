namespace FinancialPlanningApp.Web.Data.Models;

public sealed class AccountMonthlyBalance
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long TenantId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? SourceReference { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
