using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services.Auth;
using FinancialPlanningApp.Web.Services.Payments;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Services.Reconciliation;

public sealed class ExpectedOccurrence
{
    public long TemplateId { get; init; }
    public string TemplateDescription { get; init; } = string.Empty;
    public string Periodicity { get; init; } = string.Empty;
    public DateOnly DueDate { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal ExpectedAmount { get; init; }
}

public sealed class MissingPaymentSuggestion
{
    public ExpectedOccurrence Expected { get; init; } = new();
    public IReadOnlyList<PaymentCandidate> Candidates { get; init; } = [];
}

public sealed class MonthlyPlanPaidRow
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal PlannedTotal { get; init; }
    public decimal PaidTotal { get; init; }
    public decimal MappedToMonthTotal { get; init; }
    public decimal DepositedTotal { get; init; }
    public decimal ProvisionedBalanceEndOfMonth { get; init; }
    public bool IsYearTotal { get; init; }
    public decimal Variance => PlannedTotal - Math.Abs(MappedToMonthTotal);
}

public sealed class MappingOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int? SuggestedMapYear { get; init; }
    public int? SuggestedMapMonth { get; init; }
}

public sealed class TemplateActualTotal
{
    public long TemplateId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed class MappedExpenseExecution
{
    public long Id { get; init; }
    public DateOnly ExecutionDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string ExecutionType { get; init; } = string.Empty;
    public long? MappedTemplateId { get; init; }
    public int MappedPeriodYear { get; init; }
    public int MappedPeriodMonth { get; init; }
}

public sealed class ReconciliationReviewRow
{
    public long ExecutionId { get; init; }
    public DateOnly ExecutionDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? DescriptionDetails { get; init; }
    public decimal Amount { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public string SourceDisplay { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public bool IsMapped { get; init; }
    public string? MappedDisplay { get; init; }
    public int? MappedPeriodYear { get; init; }
    public int? MappedPeriodMonth { get; init; }
    public int? SuggestedMappedPeriodYear { get; init; }
    public int? SuggestedMappedPeriodMonth { get; init; }
    public string? SelectedOptionValue { get; init; }
    public IReadOnlyList<MappingOption> Options { get; init; } = [];
}

public interface IReconciliationService
{
    Task<IReadOnlyList<MissingPaymentSuggestion>> GetMissingSuggestionsAsync(int year, CancellationToken cancellationToken = default);
    Task<bool> MapExecutionAsync(long executionId, long templateId, int year, int month, string? note, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlyPlanPaidRow>> GetMonthlyPlannedVsPaidAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationReviewRow>> GetReviewRowsAsync(int year, int? month, bool onlyUnmapped, string? search, CancellationToken cancellationToken = default);
    Task<bool> ApplyMappingOptionAsync(long executionId, string optionValue, int mapYear, int mapMonth, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateActualTotal>> GetTemplateActualTotalsForYearsAsync(int fromYear, int toYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MappedExpenseExecution>> GetMappedExpenseExecutionsForPeriodAsync(int year, int month, CancellationToken cancellationToken = default);
}

public sealed class ReconciliationService(
    IRecurringPaymentService recurringPaymentService,
    IReconciliationRepository reconciliationRepository,
    ITenantContextService tenantContextService,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ReconciliationOptions> reconciliationOptions) : IReconciliationService
{
    public async Task<IReadOnlyList<MissingPaymentSuggestion>> GetMissingSuggestionsAsync(int year, CancellationToken cancellationToken = default)
    {
        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(false, cancellationToken);
        var expected = BuildExpectedOccurrences(templates, year);
        var suggestions = new List<MissingPaymentSuggestion>();

        foreach (var item in expected)
        {
            var alreadyMapped = await reconciliationRepository.IsTemplateMappedForPeriodAsync(GetCurrentUserId(), GetCurrentTenantId(), item.TemplateId, item.Year, item.Month, cancellationToken);
            if (alreadyMapped)
            {
                continue;
            }

            var from = item.DueDate.AddDays(-20);
            var to = item.DueDate.AddDays(20);
            var candidates = await reconciliationRepository.FindCandidatesAsync(GetCurrentUserId(), GetCurrentTenantId(), from, to, item.DueDate, cancellationToken);
            var ranked = candidates.OrderBy(c => c.DayDistance).ThenByDescending(c => ScoreSimilarity(item.TemplateDescription, c.Description)).Take(5).ToList();
            suggestions.Add(new MissingPaymentSuggestion { Expected = item, Candidates = ranked });
        }

        return suggestions.OrderBy(s => s.Expected.DueDate).ToList();
    }

    public Task<bool> MapExecutionAsync(long executionId, long templateId, int year, int month, string? note, CancellationToken cancellationToken = default)
        => reconciliationRepository.MapExecutionAsync(GetCurrentUserId(), GetCurrentTenantId(), executionId, templateId, year, month, httpContextAccessor.HttpContext?.User.Identity?.Name ?? "user", note, cancellationToken);

    public async Task<IReadOnlyList<MonthlyPlanPaidRow>> GetMonthlyPlannedVsPaidAsync(int year, CancellationToken cancellationToken = default)
    {
        var templates = await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken);
        var expected = BuildExpectedOccurrences(templates, year);
        var plannedByMonth = expected.GroupBy(e => e.Month).ToDictionary(g => g.Key, g => g.Sum(x => x.ExpectedAmount));

        var tenantId = GetCurrentTenantId();
        var paid = await reconciliationRepository.GetMonthlyPaidTotalsForTenantAsync(tenantId, year, cancellationToken);
        var mappedByMonth = paid.ToDictionary(x => x.Month, x => x.PaidTotal);
        var depositedByMonth = paid.ToDictionary(x => x.Month, x => x.DepositedTotal);

        var executions = await reconciliationRepository.GetExecutionsForReviewForTenantAsync(tenantId, year, null, false, null, cancellationToken);
        var paidByTransactionMonth = executions
            .Where(e => string.Equals(e.MappingStatus, "MAPPED", StringComparison.OrdinalIgnoreCase))
            .Where(e => !string.Equals(e.ExecutionType, "PLANNED_DEPOSIT", StringComparison.OrdinalIgnoreCase))
            .Where(e => !string.Equals(e.ExecutionType, "EXTRA_DEPOSIT", StringComparison.OrdinalIgnoreCase))
            .Where(e => !string.Equals(e.ExecutionType, "CARD_SETTLEMENT", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.ExecutionDate.Month)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var rows = new List<MonthlyPlanPaidRow>();
        var runningProvision = 0m;
        for (var month = 1; month <= 12; month++)
        {
            var plannedTotal = plannedByMonth.GetValueOrDefault(month, 0m);
            var paidTotal = paidByTransactionMonth.GetValueOrDefault(month, 0m);
            var mappedTotal = mappedByMonth.GetValueOrDefault(month, 0m);
            var depositedTotal = depositedByMonth.GetValueOrDefault(month, 0m);
            runningProvision += depositedTotal + mappedTotal;

            rows.Add(new MonthlyPlanPaidRow
            {
                Year = year,
                Month = month,
                PlannedTotal = plannedTotal,
                PaidTotal = paidTotal,
                MappedToMonthTotal = mappedTotal,
                DepositedTotal = depositedTotal,
                ProvisionedBalanceEndOfMonth = runningProvision
            });
        }

        rows.Add(new MonthlyPlanPaidRow
        {
            Year = year,
            Month = 0,
            PlannedTotal = rows.Sum(r => r.PlannedTotal),
            PaidTotal = rows.Sum(r => r.PaidTotal),
            MappedToMonthTotal = rows.Sum(r => r.MappedToMonthTotal),
            DepositedTotal = rows.Sum(r => r.DepositedTotal),
            ProvisionedBalanceEndOfMonth = runningProvision,
            IsYearTotal = true
        });

        return rows;
    }

    public async Task<IReadOnlyList<ReconciliationReviewRow>> GetReviewRowsAsync(int year, int? month, bool onlyUnmapped, string? search, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = GetCurrentTenantId();
        var executions = await reconciliationRepository.GetExecutionsForReviewAsync(userId, tenantId, year, month, onlyUnmapped, search, cancellationToken);
        var templates = (await recurringPaymentService.ListAllForCurrentUserAsync(true, cancellationToken)).ToList();

        var rows = new List<ReconciliationReviewRow>();
        foreach (var e in executions)
        {
            var options = BuildOptions(e, templates, reconciliationOptions.Value);
            var selectedOption = options.FirstOrDefault(o => o.Value == GetSelectedOptionValue(e))
                ?? options.FirstOrDefault(o => o.Value.StartsWith("PLANNED|", StringComparison.Ordinal));
            var mappedDisplay = e.MappingStatus == "MAPPED"
                ? $"{e.ExecutionType} {(e.MappedTemplateId is not null ? $"(template {e.MappedTemplateId}, {e.MappedPeriodYear}-{e.MappedPeriodMonth:00})" : string.Empty)}"
                : null;

            rows.Add(new ReconciliationReviewRow
            {
                ExecutionId = e.Id,
                ExecutionDate = e.ExecutionDate,
                Description = e.Description,
                DescriptionDetails = BuildDescriptionDetails(e),
                Amount = e.Amount,
                SourceType = e.SourceType,
                SourceDisplay = MapSourceDisplay(e.SourceType),
                PaymentMethod = e.PaymentMethod,
                IsMapped = string.Equals(e.MappingStatus, "MAPPED", StringComparison.OrdinalIgnoreCase),
                MappedDisplay = mappedDisplay,
                MappedPeriodYear = e.MappedPeriodYear,
                MappedPeriodMonth = e.MappedPeriodMonth,
                SuggestedMappedPeriodYear = selectedOption?.SuggestedMapYear,
                SuggestedMappedPeriodMonth = selectedOption?.SuggestedMapMonth,
                SelectedOptionValue = GetSelectedOptionValue(e),
                Options = options
            });
        }

        return rows;
    }

    private static string? BuildDescriptionDetails(ReconciliationExecutionRow row)
    {
        if (!row.SourceType.Contains("CODA", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(row.Notes))
        {
            return null;
        }

        var notes = row.Notes;
        var parts = notes.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.StartsWith("Beneficiary:", StringComparison.OrdinalIgnoreCase)
                        || p.StartsWith("Account:", StringComparison.OrdinalIgnoreCase)
                        || p.StartsWith("BIC:", StringComparison.OrdinalIgnoreCase)
                        || p.StartsWith("Context:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" | ", parts);
    }

    private static string? GetSelectedOptionValue(ReconciliationExecutionRow row)
    {
        if (!string.Equals(row.MappingStatus, "MAPPED", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(row.ExecutionType, "RECURRING_PAYMENT", StringComparison.OrdinalIgnoreCase) && row.MappedTemplateId is not null)
        {
            return $"PLANNED|{row.MappedTemplateId.Value}";
        }

        return row.ExecutionType switch
        {
            "CARD_SETTLEMENT" => "CARD_SETTLEMENT",
            "PLANNED_DEPOSIT" => "PLANNED_DEPOSIT",
            "EXTRA_DEPOSIT" => "EXTRA_DEPOSIT",
            "EXTRA_EXPENSE" => "EXTRA_EXPENSE",
            "INTERNAL_TRANSFER_OUT" => "INTERNAL_TRANSFER_OUT",
            _ => null
        };
    }

    public async Task<bool> ApplyMappingOptionAsync(long executionId, string optionValue, int mapYear, int mapMonth, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = GetCurrentTenantId();
        if (optionValue.StartsWith("PLANNED|", StringComparison.Ordinal))
        {
            var parts = optionValue.Split('|');
            if (parts.Length < 2) return false;
            if (!long.TryParse(parts[1], out var templateId)) return false;
            return await reconciliationRepository.MapExecutionAsync(userId, tenantId, executionId, templateId, mapYear, mapMonth, httpContextAccessor.HttpContext?.User.Identity?.Name ?? "user", "Manual reconciliation", cancellationToken);
        }

        if (optionValue == "UNMAP")
        {
            return await reconciliationRepository.UnmapExecutionAsync(userId, tenantId, executionId, cancellationToken);
        }

        return optionValue switch
        {
            "CARD_SETTLEMENT" => await reconciliationRepository.SetExecutionMappingAsync(userId, tenantId, executionId, "CARD_SETTLEMENT", "MAPPED", null, null, null, cancellationToken),
            "PLANNED_DEPOSIT" => await reconciliationRepository.SetExecutionMappingAsync(userId, tenantId, executionId, "PLANNED_DEPOSIT", "MAPPED", null, null, null, cancellationToken),
            "EXTRA_DEPOSIT" => await reconciliationRepository.SetExecutionMappingAsync(userId, tenantId, executionId, "EXTRA_DEPOSIT", "MAPPED", null, null, null, cancellationToken),
            "EXTRA_EXPENSE" => await reconciliationRepository.SetExecutionMappingAsync(userId, tenantId, executionId, "EXTRA_EXPENSE", "MAPPED", null, null, null, cancellationToken),
            "INTERNAL_TRANSFER_OUT" => await reconciliationRepository.SetExecutionMappingAsync(userId, tenantId, executionId, "INTERNAL_TRANSFER_OUT", "MAPPED", null, null, null, cancellationToken),
            _ => false
        };
    }

    public async Task<IReadOnlyList<TemplateActualTotal>> GetTemplateActualTotalsForYearsAsync(int fromYear, int toYear, CancellationToken cancellationToken = default)
    {
        var rows = await reconciliationRepository.GetTemplateActualTotalsForTenantAsync(GetCurrentTenantId(), fromYear, toYear, cancellationToken);

        return rows.Select(r => new TemplateActualTotal
        {
            TemplateId = r.TemplateId,
            Year = r.Year,
            Month = r.Month,
            TotalAmount = r.TotalAmount
        }).ToList();
    }

    public async Task<IReadOnlyList<MappedExpenseExecution>> GetMappedExpenseExecutionsForPeriodAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var rows = await reconciliationRepository.GetMappedExpenseExecutionsForTenantPeriodAsync(GetCurrentTenantId(), year, month, cancellationToken);

        return rows.Select(r => new MappedExpenseExecution
        {
            Id = r.Id,
            ExecutionDate = r.ExecutionDate,
            Description = r.Description,
            Amount = r.Amount,
            PaymentMethod = r.PaymentMethod,
            ExecutionType = r.ExecutionType,
            MappedTemplateId = r.MappedTemplateId,
            MappedPeriodYear = r.MappedPeriodYear,
            MappedPeriodMonth = r.MappedPeriodMonth
        }).ToList();
    }

    private static IReadOnlyList<MappingOption> BuildOptions(ReconciliationExecutionRow e, IReadOnlyList<RecurringPaymentTemplate> templates, ReconciliationOptions options)
    {
        var candidates = new List<(bool KeywordHit, decimal AmountDelta, int TextScore, MappingOption Opt)>();
        var monthTemplates = templates
            .Where(t =>
            {
                var mappedPeriod = RecurringPaymentSchedule.GetMappedPeriodForExecution(t, e.ExecutionDate);
                return RecurringPaymentSchedule.IsValidForMonth(t, mappedPeriod.Year, mappedPeriod.Month);
            })
            .ToList();

        var normalizedDescription = NormalizeText($"{e.Description} {e.Notes}".Trim());

        foreach (var t in monthTemplates)
        {
            var mappedPeriod = RecurringPaymentSchedule.GetMappedPeriodForExecution(t, e.ExecutionDate);
            var dueDay = t.PaymentDay is > 0 and <= 28 ? t.PaymentDay.Value : 1;
            var due = new DateOnly(e.ExecutionDate.Year, e.ExecutionDate.Month, dueDay);
            var expectedAmount = RecurringPaymentSchedule.GetOccurrenceAmount(t, mappedPeriod.Month);
            var amountDelta = Math.Abs(Math.Abs(e.Amount) - Math.Abs(expectedAmount));
            var textScore = ScoreSimilarity(t.Description, e.Description);
            var keywordHit = IsKeywordHit(t.MatchingKeywords, normalizedDescription);

            candidates.Add((keywordHit, amountDelta, textScore, new MappingOption
            {
                Value = $"PLANNED|{t.Id}",
                Label = $"Gepland: {t.Description} (periode {mappedPeriod:yyyy-MM}, vervaldag {due:yyyy-MM-dd}, geschat {expectedAmount:F2})",
                SuggestedMapYear = mappedPeriod.Year,
                SuggestedMapMonth = mappedPeriod.Month
            }));
        }

        var sortedPlanned = candidates
            .GroupBy(c => c.Opt.Value)
            .Select(g => g.OrderByDescending(x => x.KeywordHit).ThenBy(x => x.AmountDelta).ThenByDescending(x => x.TextScore).First())
            .OrderByDescending(x => x.KeywordHit)
            .ThenBy(x => x.AmountDelta)
            .ThenByDescending(x => x.TextScore)
            .Select(x => x.Opt)
            .Take(100)
            .ToList();

        var priority = new List<MappingOption>();
        var nonPlanned = new List<MappingOption>();

        if (options.PrioritizeCardSettlementByDescription &&
            !string.IsNullOrWhiteSpace(options.CardSettlementPrefix) &&
            e.Description.StartsWith(options.CardSettlementPrefix, StringComparison.OrdinalIgnoreCase))
        {
            priority.Add(new MappingOption { Value = "CARD_SETTLEMENT", Label = "Kredietkaartafrekening" });
        }

        if (options.PrioritizeDepositsForPositiveAmounts && e.Amount > 0)
        {
            priority.Add(new MappingOption { Value = "PLANNED_DEPOSIT", Label = "Provisiestorting (gepland maandelijks)" });
            priority.Add(new MappingOption { Value = "EXTRA_DEPOSIT", Label = "Provisiestorting (extra)" });
        }

        nonPlanned.Add(new MappingOption { Value = "CARD_SETTLEMENT", Label = "Kredietkaartafrekening" });
        nonPlanned.Add(new MappingOption { Value = "PLANNED_DEPOSIT", Label = "Provisiestorting (gepland maandelijks)" });
        nonPlanned.Add(new MappingOption { Value = "EXTRA_DEPOSIT", Label = "Provisiestorting (extra)" });
        nonPlanned.Add(new MappingOption { Value = "INTERNAL_TRANSFER_OUT", Label = "Interne overschrijving (telt als kost)" });
        nonPlanned.Add(new MappingOption { Value = "EXTRA_EXPENSE", Label = "Extra kost (ongepland)" });

        return [.. priority.DistinctBy(x => x.Value), .. sortedPlanned, .. nonPlanned.DistinctBy(x => x.Value)];
    }

    private static string MapSourceDisplay(string sourceType)
    {
        if (sourceType.Contains("CODA", StringComparison.OrdinalIgnoreCase))
        {
            return "CODA";
        }

        if (sourceType.Contains("CREDITCARD", StringComparison.OrdinalIgnoreCase))
        {
            return "CREDITCARD";
        }

        return sourceType.Length > 12 ? sourceType[..12] : sourceType;
    }

    private static List<ExpectedOccurrence> BuildExpectedOccurrences(IReadOnlyList<RecurringPaymentTemplate> templates, int year)
    {
        var list = new List<ExpectedOccurrence>();
        foreach (var t in templates)
        {
            for (var month = 1; month <= 12; month++)
            {
                if (!OccursInMonth(t, month)) continue;
                if (!RecurringPaymentSchedule.IsValidForMonth(t, year, month)) continue;
                var period = new DateOnly(year, month, 1);
                var day = t.PaymentDay is > 0 and <= 28 ? t.PaymentDay.Value : 1;
                var dueBase = period.AddMonths(Math.Max(0, t.PaymentLagMonths));
                var due = new DateOnly(dueBase.Year, dueBase.Month, day);
                list.Add(new ExpectedOccurrence { TemplateId = t.Id, TemplateDescription = t.Description, Periodicity = t.Periodicity, DueDate = due, Year = year, Month = month, ExpectedAmount = RecurringPaymentSchedule.GetOccurrenceAmount(t, month) });
            }
        }

        return list;
    }

    private static bool OccursInMonth(RecurringPaymentTemplate t, int month)
        => RecurringPaymentSchedule.GetPaymentMonths(t).Contains(month);

    private static int ScoreSimilarity(string a, string b)
    {
        var tokensA = a.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var tokensB = b.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        return tokensA.Intersect(tokensB).Count();
    }

    private static bool IsKeywordHit(string? keywordsCsv, string normalizedDescription)
    {
        if (string.IsNullOrWhiteSpace(keywordsCsv))
        {
            return false;
        }

        var keywords = keywordsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var k in keywords)
        {
            var nk = NormalizeText(k);
            if (nk.Length == 0)
            {
                continue;
            }

            if (normalizedDescription.Contains(nk, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeText(string value)
    {
        var chars = value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private long GetCurrentUserId()
        => tenantContextService.GetCurrentUserId();

    private long GetCurrentTenantId()
        => tenantContextService.GetCurrentTenantId();
}
