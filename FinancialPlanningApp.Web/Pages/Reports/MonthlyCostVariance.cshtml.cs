using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Reports;

[Authorize]
public class MonthlyCostVarianceModel(
    IRecurringPaymentService recurringPaymentService,
    IReconciliationService reconciliationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.Today.Year;

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.Today.Month;

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public TotalsRow Totals { get; private set; } = new();
    public bool HasMappedPaymentsForMonth { get; private set; }
    public bool IsCurrentOrFutureMonth { get; private set; }

    public sealed class Row
    {
        public long? TemplateId { get; init; }
        public string Description { get; init; } = string.Empty;
        public string Schedule { get; init; } = string.Empty;
        public decimal ExpectedAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal PresumedPaidAmount { get; init; }
        public string Status { get; init; } = string.Empty;
        public IReadOnlyList<PresumedPaymentRow> PresumedPayments { get; init; } = [];
        public IReadOnlyList<TransactionRow> Transactions { get; init; } = [];
        public decimal Difference => PaidAmount - ExpectedAmount;
        public decimal DifferenceWithPresumed => PaidAmount + PresumedPaidAmount - ExpectedAmount;
    }

    public sealed class TransactionRow
    {
        public DateOnly ExecutionDate { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; } = string.Empty;
        public string ExecutionType { get; init; } = string.Empty;
    }

    public sealed class PresumedPaymentRow
    {
        public DateOnly DueDate { get; init; }
        public int ReportingYear { get; init; }
        public int ReportingMonth { get; init; }
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; } = string.Empty;
    }

    public sealed class TotalsRow
    {
        public decimal ExpectedAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal PresumedPaidAmount { get; init; }
        public decimal Difference => PaidAmount - ExpectedAmount;
        public decimal DifferenceWithPresumed => PaidAmount + PresumedPaidAmount - ExpectedAmount;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Month = Math.Clamp(Month, 1, 12);

        var templates = (await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Description)
            .ToList();
        var templatesById = templates.ToDictionary(t => t.Id);
        var executions = await reconciliationService.GetMappedExpenseExecutionsForPeriodAsync(Year, Month, cancellationToken);
        HasMappedPaymentsForMonth = executions.Count > 0;
        var today = DateTime.Today;
        IsCurrentOrFutureMonth = Year > today.Year || (Year == today.Year && Month >= today.Month);
        var isCurrentMonth = Year == today.Year && Month == today.Month;
        var actualsByTemplate = executions
            .Where(e => e.MappedTemplateId is not null && string.Equals(e.ExecutionType, "RECURRING_PAYMENT", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.MappedTemplateId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var actualTotals = await reconciliationService.GetTemplateActualTotalsForYearsAsync(Year, Year, cancellationToken);
        var actualMappedPeriods = actualTotals
            .Where(a => a.Year == Year)
            .Select(a => (a.TemplateId, a.Month))
            .ToHashSet();

        var expectedByTemplate = templates
            .Where(OccursInRequestedMonth)
            .ToDictionary(t => t.Id, t => RecurringPaymentSchedule.GetOccurrenceAmount(t, Month));

        var templateIds = expectedByTemplate.Keys
            .Concat(actualsByTemplate.Keys)
            .Concat(isCurrentMonth ? BuildPresumedPayments(templates, actualMappedPeriods, DateOnly.FromDateTime(today)).Keys : [])
            .Distinct()
            .ToList();

        var rows = new List<Row>();
        var presumedByTemplate = isCurrentMonth
            ? BuildPresumedPayments(templates, actualMappedPeriods, DateOnly.FromDateTime(today))
            : [];
        foreach (var templateId in templateIds)
        {
            templatesById.TryGetValue(templateId, out var template);
            var actualTransactions = actualsByTemplate.GetValueOrDefault(templateId) ?? [];
            var expected = expectedByTemplate.GetValueOrDefault(templateId, 0m);
            var paid = actualTransactions.Sum(t => Math.Abs(t.Amount));
            var presumed = presumedByTemplate.GetValueOrDefault(templateId) ?? [];

            rows.Add(new Row
            {
                TemplateId = templateId,
                Description = template?.Description ?? $"Template {templateId}",
                Schedule = template is null ? string.Empty : RecurringPaymentSchedule.FormatDetailedSchedule(template),
                ExpectedAmount = expected,
                PaidAmount = paid,
                PresumedPaidAmount = presumed.Sum(p => p.Amount),
                Status = BuildStatus(expected, paid, presumed.Sum(p => p.Amount), HasMappedPaymentsForMonth, IsCurrentOrFutureMonth),
                PresumedPayments = presumed,
                Transactions = actualTransactions.Select(ToTransactionRow).ToList()
            });
        }

        var unplannedTransactions = executions
            .Where(e => !string.Equals(e.ExecutionType, "RECURRING_PAYMENT", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var transaction in unplannedTransactions)
        {
            rows.Add(new Row
            {
                Description = transaction.ExecutionType,
                Schedule = "Gemapt als niet-recurrente kost",
                ExpectedAmount = 0m,
                PaidAmount = Math.Abs(transaction.Amount),
                Status = "Ongeplande gemapte kost",
                Transactions = [ToTransactionRow(transaction)]
            });
        }

        Rows = rows
            .OrderByDescending(r => Math.Abs(r.Difference))
            .ThenBy(r => r.Description)
            .ToList();

        Totals = new TotalsRow
        {
            ExpectedAmount = Rows.Sum(r => r.ExpectedAmount),
            PaidAmount = Rows.Sum(r => r.PaidAmount),
            PresumedPaidAmount = Rows.Sum(r => r.PresumedPaidAmount)
        };
    }

    private Dictionary<long, List<PresumedPaymentRow>> BuildPresumedPayments(
        IReadOnlyList<RecurringPaymentTemplate> templates,
        HashSet<(long TemplateId, int Month)> actualMappedPeriods,
        DateOnly today)
    {
        var result = new Dictionary<long, List<PresumedPaymentRow>>();
        foreach (var template in templates)
        {
            if (!IsLikelyAutomatic(template.PaymentMethod))
            {
                continue;
            }

            foreach (var reportingMonth in RecurringPaymentSchedule.GetPaymentMonths(template))
            {
                if (!RecurringPaymentSchedule.IsValidForMonth(template, Year, reportingMonth))
                {
                    continue;
                }

                var dueDate = BuildDueDate(template, Year, reportingMonth);
                if (dueDate.Year != Year || dueDate.Month != Month || dueDate > today)
                {
                    continue;
                }

                if (actualMappedPeriods.Contains((template.Id, reportingMonth)))
                {
                    continue;
                }

                if (!result.TryGetValue(template.Id, out var items))
                {
                    items = [];
                    result[template.Id] = items;
                }

                items.Add(new PresumedPaymentRow
                {
                    DueDate = dueDate,
                    ReportingYear = Year,
                    ReportingMonth = reportingMonth,
                    Amount = RecurringPaymentSchedule.GetOccurrenceAmount(template, reportingMonth),
                    PaymentMethod = FormatPaymentMethod(template.PaymentMethod)
                });
            }
        }

        return result;
    }

    private bool OccursInRequestedMonth(RecurringPaymentTemplate template)
    {
        if (!RecurringPaymentSchedule.GetPaymentMonths(template).Contains(Month))
        {
            return false;
        }

        return RecurringPaymentSchedule.IsValidForMonth(template, Year, Month);
    }

    private static string BuildStatus(decimal expected, decimal paid, decimal presumedPaid, bool hasMappedPaymentsForMonth, bool isCurrentOrFutureMonth)
    {
        if (paid == 0m && presumedPaid > 0m)
        {
            return "Vermoedelijk betaald";
        }

        if (paid > 0m && presumedPaid > 0m)
        {
            return "Deels gemapt, deels vermoedelijk";
        }

        if (expected > 0m && paid == 0m && !hasMappedPaymentsForMonth)
        {
            return isCurrentOrFutureMonth ? "Nog geen gemapte betaling" : "Geen gemapte betaling gevonden";
        }

        if (expected == 0m && paid > 0m)
        {
            return "Niet gepland";
        }

        if (expected > 0m && paid == 0m)
        {
            return "Ontbrekende betaling";
        }

        if (paid > expected)
        {
            return "Meer betaald";
        }

        if (paid < expected)
        {
            return "Minder betaald";
        }

        return "OK";
    }

    private static TransactionRow ToTransactionRow(MappedExpenseExecution execution)
        => new()
        {
            ExecutionDate = execution.ExecutionDate,
            Description = execution.Description,
            Amount = Math.Abs(execution.Amount),
            PaymentMethod = execution.PaymentMethod,
            ExecutionType = execution.ExecutionType
        };

    private static DateOnly BuildDueDate(RecurringPaymentTemplate template, int year, int month)
    {
        var period = new DateOnly(year, month, 1);
        var dueBase = period.AddMonths(Math.Max(0, template.PaymentLagMonths));
        var day = template.PaymentDay is > 0 and <= 28 ? template.PaymentDay.Value : 1;
        return new DateOnly(dueBase.Year, dueBase.Month, day);
    }

    private static bool IsLikelyAutomatic(string paymentMethod)
        => string.Equals(paymentMethod, "DirectDebit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod, "CreditCard", StringComparison.OrdinalIgnoreCase);

    private static string FormatPaymentMethod(string paymentMethod)
        => paymentMethod switch
        {
            "DirectDebit" => "Domiciliering",
            "CreditCard" => "Kredietkaart",
            "Transfer" => "Overschrijving",
            _ => paymentMethod
        };
}
