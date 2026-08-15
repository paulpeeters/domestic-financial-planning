using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services.Auth;

namespace FinancialPlanningApp.Web.Services.Payments;

public sealed class PaymentTrackingService(
    IPaymentTrackingRepository repository,
    ITenantContextService tenantContext) : IPaymentTrackingService
{
    public async Task<long> AddExecutionForCurrentUserAsync(PaymentExecution execution, CancellationToken cancellationToken = default)
    {
        execution.UserId = tenantContext.GetCurrentUserId();
        execution.TenantId = tenantContext.GetCurrentTenantId();
        return await repository.AddExecutionAsync(execution, cancellationToken);
    }

    public async Task<long> AddCorrectionForCurrentUserAsync(PaymentCorrection correction, CancellationToken cancellationToken = default)
    {
        correction.UserId = tenantContext.GetCurrentUserId();
        correction.TenantId = tenantContext.GetCurrentTenantId();
        return await repository.AddCorrectionAsync(correction, cancellationToken);
    }

    public Task<bool> ExecutionExistsBySourceReferenceForCurrentUserAsync(string? sourceReference, CancellationToken cancellationToken = default)
        => repository.ExecutionExistsBySourceReferenceAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), sourceReference, cancellationToken);

    public Task<bool> ExecutionExistsForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default)
        => repository.ExecutionExistsAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), sourceReference, executionDate, amount, description, cancellationToken);

    public Task<bool> ExecutionExistsBySourceDateAmountForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, CancellationToken cancellationToken = default)
        => repository.ExecutionExistsBySourceDateAmountAsync(tenantContext.GetCurrentUserId(), tenantContext.GetCurrentTenantId(), sourceReference, executionDate, amount, cancellationToken);

    public async Task<bool> ExecutionExistsCrossSourceForCurrentUserAsync(DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default)
    {
        var existingDescriptions = await repository.ListDescriptionsForExecutionAmountDateAsync(
            tenantContext.GetCurrentUserId(),
            tenantContext.GetCurrentTenantId(),
            executionDate,
            amount,
            cancellationToken);
        var normalizedTarget = NormalizeDescription(description);
        return existingDescriptions.Any(d => NormalizeDescription(d) == normalizedTarget);
    }

    public async Task<DuplicateMatchDetail?> FindDuplicateMatchForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string description, CancellationToken cancellationToken = default)
    {
        var userId = tenantContext.GetCurrentUserId();
        var tenantId = tenantContext.GetCurrentTenantId();
        var candidates = await repository.FindDuplicateCandidatesAsync(userId, tenantId, sourceReference, executionDate, amount, cancellationToken);
        if (candidates.Count == 0)
        {
            return null;
        }

        var normalizedIncoming = NormalizeDescription(description);
        var sameSourceRef = candidates
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(sourceReference) &&
                                 string.Equals(c.SourceReference ?? string.Empty, sourceReference, StringComparison.Ordinal));
        if (sameSourceRef is not null)
        {
            return new DuplicateMatchDetail
            {
                ExistingId = sameSourceRef.Id,
                ExistingSourceType = sameSourceRef.SourceType,
                ExistingDate = sameSourceRef.ExecutionDate,
                ExistingAmount = sameSourceRef.Amount,
                ExistingDescription = sameSourceRef.Description,
                ExistingSourceReference = sameSourceRef.SourceReference,
                Reason = "same-source"
            };
        }

        var crossSource = candidates.FirstOrDefault(c => NormalizeDescription(c.Description) == normalizedIncoming);
        var chosen = crossSource ?? candidates[0];
        return new DuplicateMatchDetail
        {
            ExistingId = chosen.Id,
            ExistingSourceType = chosen.SourceType,
            ExistingDate = chosen.ExecutionDate,
            ExistingAmount = chosen.Amount,
            ExistingDescription = chosen.Description,
            ExistingSourceReference = chosen.SourceReference,
            Reason = crossSource is not null ? "cross-source" : "same-source"
        };
    }

    public async Task<bool> TryEnrichDuplicateExecutionNotesForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string description, string additionalNotes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(additionalNotes))
        {
            return false;
        }

        var userId = tenantContext.GetCurrentUserId();
        var tenantId = tenantContext.GetCurrentTenantId();
        var currentNotes = await repository.GetExecutionNotesAsync(userId, tenantId, sourceReference, executionDate, amount, description, cancellationToken);
        if (!string.IsNullOrWhiteSpace(currentNotes) &&
            currentNotes.Contains(additionalNotes, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var mergedNotes = string.IsNullOrWhiteSpace(currentNotes)
            ? additionalNotes.Trim()
            : $"{currentNotes} | {additionalNotes.Trim()}";

        return await repository.UpdateExecutionNotesAsync(userId, tenantId, sourceReference, executionDate, amount, description, mergedNotes, cancellationToken);
    }

    public async Task<bool> TryEnrichDuplicateExecutionNotesBySourceDateAmountForCurrentUserAsync(string? sourceReference, DateOnly executionDate, decimal amount, string descriptionHint, string additionalNotes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(additionalNotes))
        {
            return false;
        }

        var userId = tenantContext.GetCurrentUserId();
        var tenantId = tenantContext.GetCurrentTenantId();
        var matches = await repository.ListExecutionsBySourceDateAmountAsync(userId, tenantId, sourceReference, executionDate, amount, cancellationToken);
        if (matches.Count == 0)
        {
            return false;
        }

        var best = matches
            .OrderByDescending(m => ScoreDescriptionMatch(descriptionHint, m.Description))
            .ThenByDescending(m => m.Id)
            .First();

        var mergedNotes = MergeStructuredNotes(best.Notes, additionalNotes);
        if (string.Equals(mergedNotes, best.Notes, StringComparison.Ordinal))
        {
            return false;
        }

        return await repository.UpdateExecutionNotesByIdAsync(userId, tenantId, best.Id, mergedNotes, cancellationToken);
    }

    private static string NormalizeDescription(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        return string.Join(' ', upper.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static int ScoreDescriptionMatch(string a, string b)
    {
        var na = NormalizeDescription(a);
        var nb = NormalizeDescription(b);
        if (na.Length == 0 || nb.Length == 0) return 0;
        if (na == nb) return 10_000;
        var tokensA = na.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var tokensB = nb.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        return tokensA.Intersect(tokensB).Count();
    }

    private static string MergeStructuredNotes(string? existing, string incoming)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static void absorb(Dictionary<string, string> target, string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var parts = text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var idx = part.IndexOf(':');
                if (idx > 0)
                {
                    var key = part[..idx].Trim();
                    var value = part[(idx + 1)..].Trim();
                    if (value.Length > 0) target[key] = value;
                }
                else if (!target.ContainsKey("Imported"))
                {
                    target["Imported"] = part.Trim();
                }
            }
        }

        absorb(map, existing);
        absorb(map, incoming);

        var orderedKeys = new[] { "Imported", "Beneficiary", "Account", "BIC", "Context" };
        var ordered = new List<string>();
        foreach (var key in orderedKeys)
        {
            if (map.TryGetValue(key, out var value))
            {
                ordered.Add(key == "Imported" ? value : $"{key}: {value}");
            }
        }

        foreach (var kv in map)
        {
            if (!orderedKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add($"{kv.Key}: {kv.Value}");
            }
        }

        return string.Join(" | ", ordered);
    }
}

