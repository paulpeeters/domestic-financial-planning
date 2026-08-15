using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Payments;

[Authorize]
public class EditModel(IRecurringPaymentService recurringPaymentService) : PageModel
{
    [BindProperty]
    public RecurringPaymentTemplateInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? NavKey { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? NavIndex { get; set; }

    public long TemplateId { get; set; }
    public string BackUrl { get; private set; } = "/Payments";
    public string? PreviousUrl { get; private set; }
    public string? NextUrl { get; private set; }
    public bool HasNext => NextUrl is not null;
    public bool HasPrevious => PreviousUrl is not null;

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var template = await recurringPaymentService.GetForCurrentUserAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        TemplateId = id;
        PrepareNavigation(id);
        Input = new RecurringPaymentTemplateInputModel
        {
            Description = template.Description,
            Amount = template.Amount,
            AmountMode = template.AmountMode,
            MonthlyAmounts = RecurringPaymentSchedule.GetMonthlyProfileAmounts(template.MonthlyAmountsJson),
            DisplayOrder = template.DisplayOrder,
            Periodicity = template.Periodicity,
            PaymentMonth = template.PaymentMonth,
            PaymentMonths = template.PaymentMonths,
            PaymentDay = template.PaymentDay,
            PaymentLagMonths = template.PaymentLagMonths,
            PaymentMethod = template.PaymentMethod,
            MatchingKeywords = template.MatchingKeywords,
            ActiveFrom = template.ActiveFrom,
            ActiveUntil = template.ActiveUntil
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id, CancellationToken cancellationToken)
        => await OnPostSaveReturnAsync(id, cancellationToken);

    public async Task<IActionResult> OnPostSaveReturnAsync(long id, CancellationToken cancellationToken)
    {
        var result = await SaveCurrentAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return RedirectBackToOverview();
    }

    public async Task<IActionResult> OnPostSaveStayAsync(long id, CancellationToken cancellationToken)
    {
        var result = await SaveCurrentAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        return RedirectToCurrentEdit(id);
    }

    public async Task<IActionResult> OnPostSaveNextAsync(long id, CancellationToken cancellationToken)
    {
        var result = await SaveCurrentAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        var snapshot = PaymentEditNavigationSession.Get(HttpContext.Session, NavKey);
        var currentIndex = GetCurrentIndex(snapshot, id);
        if (snapshot is null || currentIndex is null || currentIndex.Value >= snapshot.TemplateIds.Count - 1)
        {
            return RedirectToCurrentEdit(id);
        }

        return RedirectToEdit(snapshot.TemplateIds[currentIndex.Value + 1], currentIndex.Value + 1);
    }

    public string GetBackUrl() => BackUrl;

    private async Task<IActionResult?> SaveCurrentAsync(long id, CancellationToken cancellationToken)
    {
        NormalizeInput();
        PrepareNavigation(id);

        if (!ModelState.IsValid)
        {
            TemplateId = id;
            return Page();
        }

        if (!await ValidateInputAsync(id, cancellationToken))
        {
            PrepareNavigation(id);
            return Page();
        }

        var existing = await recurringPaymentService.GetForCurrentUserAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Description = Input.Description;
        existing.Amount = Input.Amount;
        existing.AmountMode = Input.AmountMode;
        existing.MonthlyAmountsJson = RecurringPaymentSchedule.NormalizeMonthlyProfileAmounts(Input.MonthlyAmounts);
        existing.Periodicity = Input.Periodicity;
        existing.PaymentMonth = Input.PaymentMonth;
        existing.PaymentMonths = Input.PaymentMonths;
        existing.PaymentDay = Input.PaymentDay;
        existing.PaymentLagMonths = Input.PaymentLagMonths;
        existing.PaymentMethod = Input.PaymentMethod;
        existing.MatchingKeywords = Input.MatchingKeywords;
        existing.ActiveFrom = Input.ActiveFrom;
        existing.ActiveUntil = Input.ActiveUntil;

        await recurringPaymentService.UpdateAsync(existing, cancellationToken);
        return null;
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

    private async Task<bool> ValidateInputAsync(long id, CancellationToken cancellationToken)
    {
        TemplateId = id;

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
            .Where(t => t.Id != id)
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

    private IActionResult RedirectBackToOverview()
        => Url.IsLocalUrl(BackUrl) ? LocalRedirect(BackUrl) : RedirectToPage("/Payments/Index");

    private IActionResult RedirectToCurrentEdit(long id)
        => RedirectToEdit(id, NavIndex);

    private IActionResult RedirectToEdit(long id, int? navIndex)
        => RedirectToPage("/Payments/Edit", new { id, NavKey, NavIndex = navIndex, ReturnUrl });

    private void PrepareNavigation(long id)
    {
        TemplateId = id;
        var snapshot = PaymentEditNavigationSession.Get(HttpContext.Session, NavKey);
        var currentIndex = GetCurrentIndex(snapshot, id);
        if (snapshot is null || currentIndex is null)
        {
            BackUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page("/Payments/Index") ?? "/Payments";
            PreviousUrl = null;
            NextUrl = null;
            return;
        }

        NavIndex = currentIndex;
        var pageNumber = (currentIndex.Value / Math.Max(1, snapshot.PageSize)) + 1;
        BackUrl = (Url.Page("/Payments/Index", new
        {
            Search = snapshot.Search,
            IncludeInactive = snapshot.IncludeInactive,
            PageNumber = pageNumber
        }) ?? "/Payments") + $"#payment-{id}";

        PreviousUrl = currentIndex > 0
            ? Url.Page("/Payments/Edit", new { id = snapshot.TemplateIds[currentIndex.Value - 1], NavKey, NavIndex = currentIndex.Value - 1, ReturnUrl })
            : null;
        NextUrl = currentIndex < snapshot.TemplateIds.Count - 1
            ? Url.Page("/Payments/Edit", new { id = snapshot.TemplateIds[currentIndex.Value + 1], NavKey, NavIndex = currentIndex.Value + 1, ReturnUrl })
            : null;
    }

    private int? GetCurrentIndex(PaymentEditNavigationSnapshot? snapshot, long id)
    {
        if (snapshot is null)
        {
            return null;
        }

        var exactIndex = FindIndex(snapshot.TemplateIds, id);
        if (exactIndex >= 0)
        {
            return exactIndex;
        }

        return NavIndex is >= 0 && NavIndex < snapshot.TemplateIds.Count ? NavIndex : null;
    }

    private static int FindIndex(IReadOnlyList<long> values, long id)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == id)
            {
                return index;
            }
        }

        return -1;
    }
}
