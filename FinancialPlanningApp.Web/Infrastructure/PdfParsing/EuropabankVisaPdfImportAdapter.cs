using FinancialPlanningApp.Web.Services.Imports;
using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace FinancialPlanningApp.Web.Infrastructure.PdfParsing;

public sealed partial class EuropabankVisaPdfImportAdapter : IBankImportAdapter
{
    public string ProviderKey => "CREDITCARD_PDF_EUROPABANK_VISA";

    [GeneratedRegex(@"Datum\s*:\s*(\d{4})-(\d{2})-(\d{2})", RegexOptions.Compiled)]
    private static partial Regex StatementDateRegex();

    [GeneratedRegex(@"(\d{2})\/(\d{2})\s+\d{2}\/\d{2}\s+", RegexOptions.Compiled)]
    private static partial Regex TransactionStartRegex();

    [GeneratedRegex(@"([+-]\d+,\d{2})", RegexOptions.Compiled)]
    private static partial Regex SignedAmountRegex();

    [GeneratedRegex(@"VORIG\s+SALDO:\s+([+-]\d+,\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PreviousBalanceRegex();

    [GeneratedRegex(@"AFREKENING\s+KREDIETKAARTEN\s+([+-]\d+,\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SettlementRegex();

    [GeneratedRegex(@"Totaal\s+kaart:\s+([+-]\d+,\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CardTotalRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"Rekeningnummer\s*:\s*([0-9]{3}-[0-9]{7}-[0-9]{2}/[0-9]{3})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StatementAccountRegex();

    public bool CanHandle(string fileName)
        => fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
           && fileName.Contains("europabank", StringComparison.OrdinalIgnoreCase)
           && fileName.Contains("visa", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ImportedTransaction>> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(stream);
        var statementDate = ExtractStatementDate(document) ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var fullText = string.Join("\n", document.GetPages().Select(p => p.Text));
        var normalized = MultiSpaceRegex().Replace(fullText.Replace("\r", " ").Replace("\n", " "), " ").Trim();
        var statementAccount = ExtractStatementAccount(normalized);

        var results = new List<ImportedTransaction>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sequence = 0;

        ParseInformationalRows(results, normalized, statementDate, fileName);

        var starts = TransactionStartRegex().Matches(normalized);
        for (var i = 0; i < starts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = starts[i];
            var segmentStart = start.Index;
            var segmentEnd = i + 1 < starts.Count ? starts[i + 1].Index : normalized.Length;
            if (segmentEnd <= segmentStart)
            {
                continue;
            }

            var segment = normalized.Substring(segmentStart, segmentEnd - segmentStart);
            var remainder = segment.Length > start.Length ? segment[start.Length..].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(remainder))
            {
                continue;
            }

            var amountMatch = SignedAmountRegex().Match(remainder);
            if (!amountMatch.Success)
            {
                continue;
            }

            var amountText = amountMatch.Groups[1].Value;
            if (!amountText.StartsWith("+", StringComparison.Ordinal) && !amountText.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var descriptionPart = remainder[..amountMatch.Index].Trim();
            var tx = BuildTransaction(start.Groups[1].Value, start.Groups[2].Value, descriptionPart, amountText, statementDate, fileName);
            if (tx is null)
            {
                continue;
            }

            sequence++;
            tx.SourceCardNumber = statementAccount;
            tx.SourceSequence = sequence.ToString(CultureInfo.InvariantCulture);
            tx.SourceReference = BuildSourceReference(statementAccount, statementDate, sequence, fileName);

            var key = $"{tx.ExecutionDate:yyyy-MM-dd}|{tx.Description}|{tx.Amount}|{tx.IsInformational}";
            if (seen.Add(key))
            {
                results.Add(tx);
            }
        }

        AddCardTotalCheckInfo(results, statementDate, fileName);

        return Task.FromResult<IReadOnlyList<ImportedTransaction>>(results.OrderBy(t => t.ExecutionDate).ThenBy(t => t.IsInformational).ToList());
    }

    private static void ParseInformationalRows(List<ImportedTransaction> results, string normalized, DateOnly statementDate, string fileName)
    {
        var prev = PreviousBalanceRegex().Match(normalized);
        if (prev.Success && TryParseAmount(prev.Groups[1].Value, out var prevAmount))
        {
            results.Add(CreateInfo(statementDate, "VORIG SALDO", prevAmount, fileName, "PREVIOUS_BALANCE"));
        }

        var settlement = SettlementRegex().Match(normalized);
        if (settlement.Success && TryParseAmount(settlement.Groups[1].Value, out var settlementAmount))
        {
            results.Add(CreateInfo(statementDate, "AFREKENING KREDIETKAARTEN", settlementAmount, fileName, "SETTLEMENT"));
        }

        var total = CardTotalRegex().Match(normalized);
        if (total.Success && TryParseAmount(total.Groups[1].Value, out var totalAmount))
        {
            results.Add(CreateInfo(statementDate, "Totaal kaart", totalAmount, fileName, "CARD_TOTAL"));
        }
    }

    private static void AddCardTotalCheckInfo(List<ImportedTransaction> results, DateOnly statementDate, string fileName)
    {
        var cardTotal = results.FirstOrDefault(x => x.IsInformational && x.InfoType == "CARD_TOTAL")?.Amount;
        if (cardTotal is null)
        {
            return;
        }

        var txnSum = results.Where(x => !x.IsInformational).Sum(x => x.Amount);
        var diff = Math.Round(txnSum - cardTotal.Value, 2);
        var status = diff == 0 ? "CARD_TOTAL_MATCH" : "CARD_TOTAL_MISMATCH";
        var description = diff == 0
            ? $"Check total: sum(transactions) {txnSum:F2} matches Totaal kaart {cardTotal.Value:F2}"
            : $"Check total: sum(transactions) {txnSum:F2} differs from Totaal kaart {cardTotal.Value:F2} by {diff:F2}";

        results.Add(CreateInfo(statementDate, description, diff, fileName, status));

        var prev = results.FirstOrDefault(x => x.IsInformational && x.InfoType == "PREVIOUS_BALANCE")?.Amount;
        var settlement = results.FirstOrDefault(x => x.IsInformational && x.InfoType == "SETTLEMENT")?.Amount;
        if (prev is not null && settlement is not null)
        {
            var cancel = Math.Round(prev.Value + settlement.Value, 2);
            var cancelStatus = cancel == 0 ? "PREV_SETTLEMENT_MATCH" : "PREV_SETTLEMENT_MISMATCH";
            var cancelDesc = cancel == 0
                ? "Check previous balance + settlement: values cancel each other"
                : $"Check previous balance + settlement: residual {cancel:F2}";

            results.Add(CreateInfo(statementDate, cancelDesc, cancel, fileName, cancelStatus));
        }
    }

    private static ImportedTransaction CreateInfo(DateOnly date, string description, decimal amount, string fileName, string infoType)
        => new()
        {
            ExecutionDate = date,
            Description = description,
            Amount = amount,
            PaymentMethod = "CreditCard",
            SourceReference = Path.GetFileName(fileName),
            IsInformational = true,
            InfoType = infoType
        };

    private static DateOnly? ExtractStatementDate(PdfDocument document)
    {
        foreach (var page in document.GetPages())
        {
            var match = StatementDateRegex().Match(page.Text);
            if (!match.Success)
            {
                continue;
            }

            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return new DateOnly(year, month, day);
        }

        return null;
    }

    private static ImportedTransaction? BuildTransaction(string dd, string mm, string descriptionRaw, string amountRaw, DateOnly statementDate, string fileName)
    {
        if (!int.TryParse(dd, out var day) || !int.TryParse(mm, out var month))
        {
            return null;
        }

        if (!TryParseAmount(amountRaw, out var amount) || amount == 0)
        {
            return null;
        }

        var description = MultiSpaceRegex().Replace(descriptionRaw, " ").Trim();
        if (description.StartsWith("AFREKENING KREDIETKAARTEN", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("VORIG SALDO", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Totaal kaart", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("NIEUW SALDO", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var year = statementDate.Year;
        if (month > statementDate.Month)
        {
            year -= 1;
        }

        DateOnly executionDate;
        try
        {
            executionDate = new DateOnly(year, month, day);
        }
        catch
        {
            return null;
        }

        return new ImportedTransaction
        {
            ExecutionDate = executionDate,
            Description = description,
            Amount = amount,
            PaymentMethod = "CreditCard",
            SourceReference = Path.GetFileName(fileName),
            IsInformational = false
        };
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        var normalized = raw.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private static string? ExtractStatementAccount(string normalizedText)
    {
        var match = StatementAccountRegex().Match(normalizedText);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim();
    }

    private static string BuildSourceReference(string? statementAccount, DateOnly statementDate, int sequence, string fileName)
    {
        var accountPart = string.IsNullOrWhiteSpace(statementAccount) ? Path.GetFileName(fileName) : statementAccount;
        return $"{accountPart}#{statementDate:yyyy-MM-dd}#{sequence:D4}";
    }
}
