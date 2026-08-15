using FinancialPlanningApp.Web.Services.Payments;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Payments;

public sealed class RecurringPaymentTemplateInputModel
{
    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(0, 9999999)]
    public decimal Amount { get; set; }

    public string AmountMode { get; set; } = RecurringPaymentSchedule.FixedAmountMode;

    public decimal[] MonthlyAmounts { get; set; } = new decimal[12];

    public int DisplayOrder { get; set; }

    public string Periodicity { get; set; } = "Monthly";

    [Range(1, 12)]
    public int? PaymentMonth { get; set; }

    public string? PaymentMonths { get; set; }

    [Range(1, 31)]
    public int? PaymentDay { get; set; } = 1;

    [Range(0, 24)]
    public int PaymentLagMonths { get; set; }

    public string PaymentMethod { get; set; } = "Transfer";

    public string? MatchingKeywords { get; set; }

    public DateOnly ActiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? ActiveUntil { get; set; }
}
