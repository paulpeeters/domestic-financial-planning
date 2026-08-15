using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services.Auth;
using FinancialPlanningApp.Web.Services.Imports;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Reconciliation;

[Authorize]
public class ManualModel(
    IRecurringPaymentService recurringPaymentService,
    IReconciliationService reconciliationService,
    IPaymentTrackingRepository paymentTrackingRepository,
    IAccountMonthlyBalanceService accountMonthlyBalanceService,
    ITenantContextService tenantContextService,
    IApplicationSettingsService applicationSettingsService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.Today.Year;

    [BindProperty(SupportsGet = true)]
    [Range(1, 12)]
    public int Month { get; set; } = DateTime.Today.Month;

    [BindProperty]
    public BalanceInputModel BalanceInput { get; set; } = new();

    [BindProperty]
    public ProvisionInputModel ProvisionInput { get; set; } = new();

    [BindProperty]
    public CardSettlementInputModel CardSettlementInput { get; set; } = new();

    public IReadOnlyList<ManualPaymentRow> Rows { get; private set; } = [];
    public decimal SuggestedMonthlyProvision { get; private set; }
    public decimal ProvisionedThisMonth { get; private set; }
    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostMarkPaidAsync(long templateId, decimal amount, DateOnly paidDate, string? paymentMethod, string? note, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        var row = Rows.FirstOrDefault(r => r.TemplateId == templateId);
        if (row is null)
        {
            return NotFound();
        }

        if (amount <= 0m)
        {
            ModelState.AddModelError(string.Empty, "Geef een betaald bedrag groter dan nul in.");
            return Page();
        }

        var userId = tenantContextService.GetCurrentUserId();
        var tenantId = tenantContextService.GetCurrentTenantId();
        var execution = new PaymentExecution
        {
            UserId = userId,
            TenantId = tenantId,
            TemplateId = templateId,
            ExecutionDate = paidDate,
            Description = row.Description,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? row.PaymentMethod : paymentMethod.Trim(),
            Amount = -Math.Abs(amount),
            SourceType = "MANUAL",
            SourceReference = $"MANUAL-{Guid.NewGuid():N}",
            Notes = string.IsNullOrWhiteSpace(note) ? "Manueel afgepunt" : note.Trim()
        };

        var executionId = await paymentTrackingRepository.AddExecutionAsync(execution, cancellationToken);
        await reconciliationService.MapExecutionAsync(executionId, templateId, Year, Month, "Manueel afgepunt", cancellationToken);
        StatusMessage = $"{row.Description} werd afgepunt voor {Year}-{Month:00}.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddProvisionAsync(CancellationToken cancellationToken)
    {
        if (ProvisionInput.Amount <= 0m)
        {
            ModelState.AddModelError("ProvisionInput.Amount", "Geef een provisiebedrag groter dan nul in.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var userId = tenantContextService.GetCurrentUserId();
        var tenantId = tenantContextService.GetCurrentTenantId();
        await paymentTrackingRepository.AddExecutionAsync(new PaymentExecution
        {
            UserId = userId,
            TenantId = tenantId,
            ExecutionDate = ProvisionInput.PaymentDate,
            Description = string.IsNullOrWhiteSpace(ProvisionInput.Description)
                ? "Maandelijkse provisie"
                : ProvisionInput.Description.Trim(),
            PaymentMethod = string.IsNullOrWhiteSpace(ProvisionInput.PaymentMethod)
                ? "Transfer"
                : ProvisionInput.PaymentMethod.Trim(),
            Amount = Math.Abs(ProvisionInput.Amount),
            SourceType = "MANUAL",
            SourceReference = $"MANUAL-PROVISION-{Guid.NewGuid():N}",
            Notes = "Manuele maandelijkse provisie",
            ExecutionType = "PLANNED_DEPOSIT",
            MappingStatus = "MAPPED"
        }, cancellationToken);

        StatusMessage = $"Maandelijkse provisie voor {Year}-{Month:00} werd toegevoegd.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveBalanceAsync(CancellationToken cancellationToken)
    {
        if (BalanceInput.ClosingBalance is null)
        {
            ModelState.AddModelError("BalanceInput.ClosingBalance", "Geef het huidige banksaldo in.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        await accountMonthlyBalanceService.UpsertForCurrentUserAsync(new AccountMonthlyBalance
        {
            AccountNumber = string.IsNullOrWhiteSpace(BalanceInput.AccountNumber) ? "MANUAL" : BalanceInput.AccountNumber.Trim(),
            Year = Year,
            Month = Month,
            OpeningBalance = BalanceInput.OpeningBalance,
            ClosingBalance = BalanceInput.ClosingBalance.Value,
            SourceReference = "MANUAL"
        }, cancellationToken);

        StatusMessage = $"Banksaldo voor {Year}-{Month:00} werd opgeslagen.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddCardSettlementAsync(CancellationToken cancellationToken)
    {
        if (CardSettlementInput.Amount <= 0m)
        {
            ModelState.AddModelError("CardSettlementInput.Amount", "Geef een afrekeningbedrag groter dan nul in.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var userId = tenantContextService.GetCurrentUserId();
        var tenantId = tenantContextService.GetCurrentTenantId();
        await paymentTrackingRepository.AddExecutionAsync(new PaymentExecution
        {
            UserId = userId,
            TenantId = tenantId,
            ExecutionDate = CardSettlementInput.PaymentDate,
            Description = string.IsNullOrWhiteSpace(CardSettlementInput.Description)
                ? "Kredietkaartafrekening"
                : CardSettlementInput.Description.Trim(),
            PaymentMethod = "CreditCard",
            Amount = -Math.Abs(CardSettlementInput.Amount),
            SourceType = "MANUAL",
            SourceReference = $"MANUAL-CARD-{Guid.NewGuid():N}",
            Notes = "Manuele kredietkaartafrekening",
            ExecutionType = "CARD_SETTLEMENT",
            MappingStatus = "MAPPED"
        }, cancellationToken);

        StatusMessage = "Kredietkaartafrekening werd toegevoegd. Ze telt niet mee als kost.";
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        BalanceInput.Year = Year;
        BalanceInput.Month = Month;
        var provisionDay = await applicationSettingsService.GetMonthlyProvisionDayAsync(cancellationToken);
        ProvisionInput.PaymentDate = ProvisionInput.PaymentDate == default
            ? GetDefaultPaymentDate(provisionDay)
            : ProvisionInput.PaymentDate;
        ProvisionInput.PaymentMethod = string.IsNullOrWhiteSpace(ProvisionInput.PaymentMethod)
            ? "Transfer"
            : ProvisionInput.PaymentMethod;
        CardSettlementInput.PaymentDate = CardSettlementInput.PaymentDate == default
            ? DateOnly.FromDateTime(DateTime.Today)
            : CardSettlementInput.PaymentDate;

        var monthlyPlan = await reconciliationService.GetMonthlyPlannedVsPaidAsync(Year, cancellationToken);
        var monthRow = monthlyPlan.FirstOrDefault(r => !r.IsYearTotal && r.Month == Month);
        var yearTotal = monthlyPlan.FirstOrDefault(r => r.IsYearTotal);
        SuggestedMonthlyProvision = await applicationSettingsService.GetMonthlyProvisionAmountAsync(cancellationToken)
            ?? Math.Ceiling((yearTotal?.PlannedTotal ?? 0m) / 12m);
        ProvisionedThisMonth = monthRow?.DepositedTotal ?? 0m;
        if (ProvisionInput.Amount <= 0m)
        {
            ProvisionInput.Amount = SuggestedMonthlyProvision;
        }

        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        var actuals = await reconciliationService.GetTemplateActualTotalsForYearsAsync(Year, Year, cancellationToken);
        var actualByTemplate = actuals
            .Where(a => a.Year == Year && a.Month == Month)
            .GroupBy(a => a.TemplateId)
            .ToDictionary(g => g.Key, g => Math.Abs(g.Sum(x => x.TotalAmount)));

        Rows = templates
            .Where(t => RecurringPaymentSchedule.GetPaymentMonths(t).Contains(Month))
            .Where(t => RecurringPaymentSchedule.IsValidForMonth(t, Year, Month))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Description)
            .Select(t =>
            {
                var dueDate = BuildDueDate(t, Year, Month);
                var expected = RecurringPaymentSchedule.GetOccurrenceAmount(t, Month);
                var paid = actualByTemplate.GetValueOrDefault(t.Id, 0m);
                return new ManualPaymentRow
                {
                    TemplateId = t.Id,
                    Description = t.Description,
                    Schedule = RecurringPaymentSchedule.FormatPaymentSchedule(t),
                    PaymentMethod = t.PaymentMethod,
                    DueDate = dueDate,
                    ExpectedAmount = expected,
                    PaidAmount = paid,
                    SuggestedAmount = paid > 0m ? Math.Max(0m, expected - paid) : expected
                };
            })
            .ToList();
    }

    private static DateOnly BuildDueDate(RecurringPaymentTemplate template, int year, int month)
    {
        var period = new DateOnly(year, month, 1);
        var dueBase = period.AddMonths(Math.Max(0, template.PaymentLagMonths));
        var day = template.PaymentDay is > 0 and <= 28 ? template.PaymentDay.Value : 1;
        return new DateOnly(dueBase.Year, dueBase.Month, day);
    }

    private DateOnly GetDefaultPaymentDate(int provisionDay)
    {
        var selectedMonth = new DateOnly(Year, Month, Math.Clamp(provisionDay, 1, 28));
        var today = DateOnly.FromDateTime(DateTime.Today);
        return selectedMonth.Year == today.Year && selectedMonth.Month == today.Month
            ? today
            : selectedMonth;
    }

    public sealed class ManualPaymentRow
    {
        public long TemplateId { get; init; }
        public string Description { get; init; } = string.Empty;
        public string Schedule { get; init; } = string.Empty;
        public string PaymentMethod { get; init; } = string.Empty;
        public DateOnly DueDate { get; init; }
        public decimal ExpectedAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal SuggestedAmount { get; init; }
        public decimal Difference => PaidAmount - ExpectedAmount;
        public bool IsPaid => PaidAmount > 0m;
    }

    public sealed class BalanceInputModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string? AccountNumber { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? ClosingBalance { get; set; }
    }

    public sealed class ProvisionInputModel
    {
        public DateOnly PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Transfer";
        public string? Description { get; set; }
    }

    public sealed class CardSettlementInputModel
    {
        public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
