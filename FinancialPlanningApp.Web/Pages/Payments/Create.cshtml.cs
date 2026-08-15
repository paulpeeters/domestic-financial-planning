using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Payments;

[Authorize]
public class CreateModel(IRecurringPaymentService recurringPaymentService) : PageModel
{
    [BindProperty]
    public RecurringPaymentTemplateInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public long? SourceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Restart { get; set; }

    public string? SourceDescription { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (SourceId is null)
        {
            return Page();
        }

        var source = await recurringPaymentService.GetForCurrentUserAsync(SourceId.Value, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        SourceDescription = source.Description;
        Input = new RecurringPaymentTemplateInputModel
        {
            Description = source.Description,
            Amount = source.Amount,
            AmountMode = source.AmountMode,
            MonthlyAmounts = RecurringPaymentSchedule.GetMonthlyProfileAmounts(source.MonthlyAmountsJson),
            DisplayOrder = 0,
            Periodicity = source.Periodicity,
            PaymentMonth = source.PaymentMonth,
            PaymentMonths = source.PaymentMonths,
            PaymentDay = source.PaymentDay,
            PaymentLagMonths = source.PaymentLagMonths,
            PaymentMethod = source.PaymentMethod,
            MatchingKeywords = source.MatchingKeywords,
            ActiveFrom = Restart ? DateOnly.FromDateTime(DateTime.Today) : source.ActiveFrom,
            ActiveUntil = Restart ? null : source.ActiveUntil
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        NormalizeInput();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!await ValidateInputAsync(null, cancellationToken))
        {
            return Page();
        }

        var existingTemplates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        var displayOrder = existingTemplates.Count == 0 ? 10 : existingTemplates.Max(t => t.DisplayOrder) + 10;

        var template = new RecurringPaymentTemplate
        {
            Description = Input.Description,
            Amount = Input.Amount,
            AmountMode = Input.AmountMode,
            MonthlyAmountsJson = RecurringPaymentSchedule.NormalizeMonthlyProfileAmounts(Input.MonthlyAmounts),
            DisplayOrder = displayOrder,
            Periodicity = Input.Periodicity,
            PaymentMonth = Input.PaymentMonth,
            PaymentMonths = Input.PaymentMonths,
            PaymentDay = Input.PaymentDay,
            PaymentLagMonths = Input.PaymentLagMonths,
            PaymentMethod = Input.PaymentMethod,
            MatchingKeywords = Input.MatchingKeywords,
            ActiveFrom = Input.ActiveFrom,
            ActiveUntil = Input.ActiveUntil,
            IsActive = true
        };

        await recurringPaymentService.CreateAsync(template, cancellationToken);
        return RedirectToPage("/Payments/Index");
    }

    private void NormalizeInput()
    {
        if (Input.MonthlyAmounts.Length != 12)
        {
            Input.MonthlyAmounts = Input.MonthlyAmounts.Take(12).Concat(Enumerable.Repeat(0m, 12)).Take(12).ToArray();
        }

        if (Input.AmountMode == RecurringPaymentSchedule.MonthlyProfileAmountMode)
        {
            Input.Amount = Input.MonthlyAmounts.Sum();
            Input.Periodicity = "Monthly";
            Input.PaymentMonth = null;
            Input.PaymentMonths = null;
        }

        if (Input.Periodicity == "Monthly")
        {
            Input.PaymentMonth = null;
        }

        if (Input.Periodicity != RecurringPaymentSchedule.CustomMonthsYearlyBudget)
        {
            Input.PaymentMonths = null;
        }
    }

    private async Task<bool> ValidateInputAsync(long? currentTemplateId, CancellationToken cancellationToken)
    {
        if (Input.ActiveUntil is not null && Input.ActiveUntil < Input.ActiveFrom)
        {
            ModelState.AddModelError("Input.ActiveUntil", "Geldig tot moet op of na geldig vanaf liggen.");
        }

        if (Input.AmountMode == RecurringPaymentSchedule.FixedAmountMode && Input.Amount <= 0m)
        {
            ModelState.AddModelError("Input.Amount", "Geef een bedrag groter dan nul in.");
        }

        if (Input.AmountMode == RecurringPaymentSchedule.MonthlyProfileAmountMode && Input.MonthlyAmounts.Sum() <= 0m)
        {
            ModelState.AddModelError("Input.MonthlyAmounts", "Geef minstens een maandprofielbedrag in.");
        }

        if (Input.Periodicity == RecurringPaymentSchedule.CustomMonthsYearlyBudget &&
            RecurringPaymentSchedule.ParsePaymentMonths(Input.PaymentMonths).Count == 0)
        {
            ModelState.AddModelError("Input.PaymentMonths", "Geef minstens een betalingsmaand in, bv. 2,5,7,11.");
        }

        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        var overlapping = templates
            .Where(t => currentTemplateId is null || t.Id != currentTemplateId.Value)
            .Where(t => string.Equals(t.Description.Trim(), Input.Description.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(t => string.Equals(t.PaymentMethod, Input.PaymentMethod, StringComparison.OrdinalIgnoreCase))
            .Any(t => DateRangesOverlap(Input.ActiveFrom, Input.ActiveUntil, t.ActiveFrom, t.ActiveUntil));

        if (overlapping)
        {
            ModelState.AddModelError("Input.ActiveFrom", "Er bestaat al een recurrente betaling met dezelfde omschrijving en betaalmethode waarvan de geldigheidsperiode overlapt.");
        }

        return ModelState.IsValid;
    }

    private static bool DateRangesOverlap(DateOnly leftFrom, DateOnly? leftUntil, DateOnly rightFrom, DateOnly? rightUntil)
    {
        var leftEnd = leftUntil ?? DateOnly.MaxValue;
        var rightEnd = rightUntil ?? DateOnly.MaxValue;
        return leftFrom <= rightEnd && rightFrom <= leftEnd;
    }
}
