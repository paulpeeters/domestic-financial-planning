using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Payments;

namespace FinancialPlanningApp.Web.Services.Imports;

public sealed class BankImportService(
    IEnumerable<IBankImportAdapter> adapters,
    IPaymentTrackingService paymentTrackingService,
    IImportSourceRegistryService importSourceRegistryService,
    IAccountMonthlyBalanceService accountMonthlyBalanceService,
    ILogger<BankImportService> logger) : IBankImportService
{
    public async Task<IReadOnlyList<ImportedTransaction>> ParseAsync(string providerKey, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var adapter = ResolveAdapter(providerKey);
        return await adapter.ParseAsync(stream, fileName, cancellationToken);
    }

    public async Task<ImportResult> PersistAsync(string providerKey, IReadOnlyList<ImportedTransaction> transactions, string fileName, CancellationToken cancellationToken = default)
    {
        var adapter = ResolveAdapter(providerKey);
        var imported = 0;
        var skipped = 0;
        var duplicate = 0;
        var sameSourceDuplicateCount = 0;
        var crossSourceDuplicateCount = 0;
        var invalid = 0;
        var insertError = 0;
        var warnings = new List<string>();
        var duplicateDetails = new List<ImportDuplicateDetail>();

        foreach (var tx in transactions)
        {
            var isCoda = string.Equals(adapter.ProviderKey, "CODA", StringComparison.OrdinalIgnoreCase);
            var isCreditCard = adapter.ProviderKey.StartsWith("CREDITCARD_", StringComparison.OrdinalIgnoreCase);

            if (tx.IsInformational)
            {
                if (isCoda &&
                    string.Equals(tx.InfoType, "CODA_MONTHLY_BALANCE", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(tx.SourceAccountNumber) &&
                    tx.ClosingBalance is not null)
                {
                    await accountMonthlyBalanceService.UpsertForCurrentUserAsync(new AccountMonthlyBalance
                    {
                        AccountNumber = tx.SourceAccountNumber,
                        Year = tx.ExecutionDate.Year,
                        Month = tx.ExecutionDate.Month,
                        OpeningBalance = tx.OpeningBalance,
                        ClosingBalance = tx.ClosingBalance.Value,
                        SourceReference = tx.SourceReference
                    }, cancellationToken);
                }

                continue;
            }

            if (isCoda)
            {
                if (string.IsNullOrWhiteSpace(tx.SourceAccountNumber))
                {
                    skipped++;
                    invalid++;
                    warnings.Add($"Transactie '{tx.Description}' ({tx.ExecutionDate:yyyy-MM-dd}) overgeslagen: bron-bankrekeningnummer ontbreekt in gelezen CODA-data.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tx.SourceReference))
                {
                    skipped++;
                    invalid++;
                    warnings.Add($"Transactie '{tx.Description}' ({tx.ExecutionDate:yyyy-MM-dd}) overgeslagen: unieke CODA-bronreferentie ontbreekt (jaar/papier/transactie).");
                    continue;
                }

                var registered = await importSourceRegistryService.IsRegisteredBankAccountForCurrentUserAsync(tx.SourceAccountNumber, cancellationToken);
                if (!registered)
                {
                    skipped++;
                    invalid++;
                    warnings.Add($"Transactie '{tx.Description}' ({tx.ExecutionDate:yyyy-MM-dd}) overgeslagen: bron-bankrekening '{tx.SourceAccountNumber}' is niet geregistreerd.");
                    continue;
                }
            }

            if (isCreditCard)
            {
                if (string.IsNullOrWhiteSpace(tx.SourceCardNumber))
                {
                    skipped++;
                    invalid++;
                    warnings.Add($"Transactie '{tx.Description}' ({tx.ExecutionDate:yyyy-MM-dd}) overgeslagen: bron-kredietkaartnummer ontbreekt in gelezen data.");
                    continue;
                }

                var registered = await importSourceRegistryService.IsRegisteredCreditCardForCurrentUserAsync(tx.SourceCardNumber, cancellationToken);
                if (!registered)
                {
                    skipped++;
                    invalid++;
                    warnings.Add($"Transactie '{tx.Description}' ({tx.ExecutionDate:yyyy-MM-dd}) overgeslagen: bron-kredietkaart '{tx.SourceCardNumber}' is niet geregistreerd.");
                    continue;
                }
            }

            if (tx.Amount == 0 || string.IsNullOrWhiteSpace(tx.Description))
            {
                skipped++;
                invalid++;
                continue;
            }

            var sameSourceDuplicate = await paymentTrackingService.ExecutionExistsBySourceReferenceForCurrentUserAsync(tx.SourceReference, cancellationToken);
            var crossSourceDuplicate = false;

            // For CODA, dedupe is strictly based on the bank-provided consolidated key
            // (account/year/page/transaction) encoded in SourceReference.
            if (!isCoda)
            {
                sameSourceDuplicate = sameSourceDuplicate
                    || await paymentTrackingService.ExecutionExistsForCurrentUserAsync(tx.SourceReference, tx.ExecutionDate, tx.Amount, tx.Description, cancellationToken)
                    || await paymentTrackingService.ExecutionExistsBySourceDateAmountForCurrentUserAsync(tx.SourceReference, tx.ExecutionDate, tx.Amount, cancellationToken);

                crossSourceDuplicate = await paymentTrackingService.ExecutionExistsCrossSourceForCurrentUserAsync(tx.ExecutionDate, tx.Amount, tx.Description, cancellationToken);
            }
            if (sameSourceDuplicate || crossSourceDuplicate)
            {
                var duplicateMatch = await paymentTrackingService.FindDuplicateMatchForCurrentUserAsync(
                    tx.SourceReference,
                    tx.ExecutionDate,
                    tx.Amount,
                    tx.Description,
                    cancellationToken);

                if (duplicateMatch is not null)
                {
                    duplicateDetails.Add(new ImportDuplicateDetail
                    {
                        Reason = duplicateMatch.Reason,
                        IncomingDate = tx.ExecutionDate,
                        IncomingAmount = tx.Amount,
                        IncomingDescription = tx.Description,
                        IncomingSourceType = adapter.ProviderKey,
                        IncomingSourceReference = tx.SourceReference,
                        ExistingId = duplicateMatch.ExistingId,
                        ExistingSourceType = duplicateMatch.ExistingSourceType,
                        ExistingDate = duplicateMatch.ExistingDate,
                        ExistingAmount = duplicateMatch.ExistingAmount,
                        ExistingDescription = duplicateMatch.ExistingDescription,
                        ExistingSourceReference = duplicateMatch.ExistingSourceReference
                    });
                }

                var enrichmentNotes = BuildEnrichmentNotes(tx);
                if (!string.IsNullOrWhiteSpace(enrichmentNotes))
                {
                    await paymentTrackingService.TryEnrichDuplicateExecutionNotesBySourceDateAmountForCurrentUserAsync(
                        tx.SourceReference,
                        tx.ExecutionDate,
                        tx.Amount,
                        tx.Description,
                        enrichmentNotes,
                        cancellationToken);
                }

                skipped++;
                duplicate++;
                if (sameSourceDuplicate)
                {
                    sameSourceDuplicateCount++;
                }
                else
                {
                    crossSourceDuplicateCount++;
                }
                continue;
            }

            var execution = new PaymentExecution
            {
                ExecutionDate = tx.ExecutionDate,
                Description = tx.Description,
                PaymentMethod = tx.PaymentMethod,
                Amount = tx.Amount,
                SourceType = adapter.ProviderKey,
                SourceReference = tx.SourceReference,
                SourceSequence = tx.SourceSequence,
                SourceAccountNumber = tx.SourceAccountNumber,
                SourceCardNumber = tx.SourceCardNumber,
                Notes = BuildImportNotes(tx)
            };

            try
            {
                await paymentTrackingService.AddExecutionForCurrentUserAsync(execution, cancellationToken);
                imported++;
            }
            catch (Exception ex)
            {
                skipped++;
                insertError++;
                warnings.Add($"Transactie '{tx.Description}' ({tx.ExecutionDate:yyyy-MM-dd}) overgeslagen: {ex.Message}");
                logger.LogWarning(ex, "Failed to import transaction {Description} on {Date}", tx.Description, tx.ExecutionDate);
            }
        }

        return new ImportResult
        {
            ImportedCount = imported,
            SkippedCount = skipped,
            DuplicateCount = duplicate,
            SameSourceDuplicateCount = sameSourceDuplicateCount,
            CrossSourceDuplicateCount = crossSourceDuplicateCount,
            InvalidCount = invalid,
            InsertErrorCount = insertError,
            Warnings = warnings,
            DuplicateDetails = duplicateDetails
        };
    }

    private static string BuildImportNotes(ImportedTransaction tx)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(tx.InfoType))
        {
            parts.Add($"Imported ({tx.InfoType})");
        }
        else
        {
            parts.Add("Imported");
        }

        if (!string.IsNullOrWhiteSpace(tx.CounterpartyName))
        {
            parts.Add($"Beneficiary: {tx.CounterpartyName}");
        }

        if (!string.IsNullOrWhiteSpace(tx.CounterpartyAccount))
        {
            parts.Add($"Account: {tx.CounterpartyAccount}");
        }

        if (!string.IsNullOrWhiteSpace(tx.CounterpartyBic))
        {
            parts.Add($"BIC: {tx.CounterpartyBic}");
        }

        if (!string.IsNullOrWhiteSpace(tx.AdditionalContext))
        {
            parts.Add($"Context: {tx.AdditionalContext}");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildEnrichmentNotes(ImportedTransaction tx)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(tx.CounterpartyName))
        {
            parts.Add($"Beneficiary: {tx.CounterpartyName}");
        }

        if (!string.IsNullOrWhiteSpace(tx.CounterpartyAccount))
        {
            parts.Add($"Account: {tx.CounterpartyAccount}");
        }

        if (!string.IsNullOrWhiteSpace(tx.CounterpartyBic))
        {
            parts.Add($"BIC: {tx.CounterpartyBic}");
        }

        if (!string.IsNullOrWhiteSpace(tx.AdditionalContext))
        {
            parts.Add($"Context: {tx.AdditionalContext}");
        }

        return string.Join(" | ", parts);
    }

    private IBankImportAdapter ResolveAdapter(string providerKey)
    {
        var adapter = adapters.FirstOrDefault(a => string.Equals(a.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));
        if (adapter is null)
        {
            throw new InvalidOperationException($"No adapter registered for provider '{providerKey}'.");
        }

        return adapter;
    }
}
