using FinancialPlanningApp.Web.Services.Imports;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

namespace FinancialPlanningApp.Web.Infrastructure.BankImport;

// IMPORTANT:
// For CODA record layout and fixed-position parsing, always refer to:
// https://febelfin.be/media/pages/publicaties/2023/febelfin-standaarden-voor-online-bankieren/d7168c5c37-1764229602/standard-coda-en_-2025.pdf
// See also: docs/CODA-REFERENCE.md
public sealed class CodaBankImportAdapter : IBankImportAdapter
{
    public string ProviderKey => "CODA";

    public bool CanHandle(string fileName)
        => fileName.EndsWith(".coda", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ImportedTransaction>> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length >= 10)
            {
                lines.Add(line);
            }
        }

        var ownAccountNumber = lines
            .Where(l => SafeSubstring(l, 0, 2) == "12")
            .Select(TryExtractOwnAccountNumberFromRecord12)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var paperNumber = lines
            .Where(l => SafeSubstring(l, 0, 2) == "12")
            .Select(TryExtractPaperNumberFromRecord12)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var record8Date = lines
            .Where(l => l.Length > 0 && l[0] == '8')
            .Select(TryExtractDateFromRecord8)
            .FirstOrDefault(v => v is not null);

        if (record8Date is null)
        {
            // Strict mode: invalid/missing record 8 date means the CODA file is invalid.
            return [];
        }

        var codaYear = record8Date.Value.Year;
        var closingBalance = lines
            .Where(l => l.Length > 0 && l[0] == '8')
            .Select(TryExtractClosingBalanceFromRecord8)
            .FirstOrDefault(v => v is not null);

        var openingBalance = lines
            .Where(l => SafeSubstring(l, 0, 1) == "1")
            .Select(TryExtractOpeningBalanceFromRecord1)
            .FirstOrDefault(v => v is not null);

        var transactions = new List<ImportedTransaction>();
        ImportedTransaction? current = null;
        var reference = Path.GetFileName(fileName);
        foreach (var codaLine in lines)
        {
            var recordCode = SafeSubstring(codaLine, 0, 2);

            if (recordCode == "21")
            {
                var parsed = TryParseDetailLine(codaLine, reference, ownAccountNumber, codaYear, paperNumber);
                if (parsed is not null)
                {
                    transactions.Add(parsed);
                    current = parsed;
                }
                continue;
            }

            if (recordCode == "22" && current is not null)
            {
                EnrichWithContinuationLine(current, codaLine);
                continue;
            }

            if (recordCode == "23" && current is not null)
            {
                EnrichWithCounterpartyLine(current, codaLine);
            }
        }

        if (!string.IsNullOrWhiteSpace(ownAccountNumber) && closingBalance is not null)
        {
            transactions.Add(new ImportedTransaction
            {
                ExecutionDate = record8Date.Value,
                Description = "CODA monthly balance snapshot",
                Amount = 0m,
                PaymentMethod = "Transfer",
                SourceReference = $"{ownAccountNumber}#{record8Date:yyyy-MM-dd}#BALANCE",
                SourceAccountNumber = ownAccountNumber,
                IsInformational = true,
                InfoType = "CODA_MONTHLY_BALANCE",
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance
            });
        }

        return transactions;
    }

    private static void EnrichWithContinuationLine(ImportedTransaction tx, string line)
    {
        var payload = SafeSubstring(line, 10, 110);
        var bicMatch = Regex.Match(payload, @"\b[A-Z]{6}[A-Z0-9]{2}(?:[A-Z0-9]{3})?\b");
        if (bicMatch.Success && string.IsNullOrWhiteSpace(tx.CounterpartyBic))
        {
            tx.CounterpartyBic = bicMatch.Value;
        }

        var info = CleanCodaText(payload);
        if (bicMatch.Success)
        {
            info = CleanCodaText(info.Replace(bicMatch.Value, string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(info))
        {
            return;
        }

        tx.AdditionalContext = string.IsNullOrWhiteSpace(tx.AdditionalContext)
            ? info
            : JoinTextFragments(tx.AdditionalContext, info);

        if (!string.IsNullOrWhiteSpace(tx.Description) &&
            !tx.Description.Contains(info, StringComparison.OrdinalIgnoreCase))
        {
            tx.Description = JoinTextFragments(tx.Description, info);
        }
    }

    private static void EnrichWithCounterpartyLine(ImportedTransaction tx, string line)
    {
        var accountRaw = CleanCodaText(SafeSubstring(line, 10, 37));
        var nameRaw = CleanCodaText(SafeSubstring(line, 44, 35));
        var continuationRaw = CleanCodaText(SafeSubstring(line, 82, 43));

        if (string.IsNullOrWhiteSpace(accountRaw) && string.IsNullOrWhiteSpace(nameRaw) && string.IsNullOrWhiteSpace(continuationRaw))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(accountRaw) && Regex.IsMatch(accountRaw, @"\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b", RegexOptions.IgnoreCase) && string.IsNullOrWhiteSpace(tx.CounterpartyAccount))
        {
            tx.CounterpartyAccount = accountRaw.ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(tx.CounterpartyName) && nameRaw.Length >= 3)
        {
            tx.CounterpartyName = nameRaw.Length <= 80 ? nameRaw : nameRaw[..80];
        }

        if (!string.IsNullOrWhiteSpace(continuationRaw))
        {
            tx.AdditionalContext = string.IsNullOrWhiteSpace(tx.AdditionalContext)
                ? continuationRaw
                : JoinTextFragments(tx.AdditionalContext, continuationRaw);
        }
    }

    private static ImportedTransaction? TryParseDetailLine(string line, string reference, string? ownAccountNumber, int? codaYear, string? paperNumber)
    {
        try
        {
            var transactionNumber = SafeSubstring(line, 2, 4).Trim();
            var detail = SafeSubstring(line, 6, 4).Trim();
            // Fixed-width extraction based on common Belgian CODA layout.
            // CODA amount field is 14 digits with 2 implied decimals.
            // Using 15 digits causes a trailing field digit to leak in and scale amounts by x10.
            var amountRaw = SafeSubstring(line, 32, 14).Trim();
            var sign = SafeSubstring(line, 31, 1);
            var communication = CleanCodaText(SafeSubstring(line, 62, 53));
            var dateRaw = SafeSubstring(line, 47, 6).Trim(); // DDMMYY

            if (!decimal.TryParse(amountRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cents))
            {
                return null;
            }

            var amount = cents / 100m;
            if (sign == "1")
            {
                amount = -amount;
            }

            var date = ParseCodaDate(dateRaw);
            if (date is null)
            {
                return null;
            }

            var sourceReference = BuildMovementSourceReference(ownAccountNumber, codaYear, paperNumber, transactionNumber);
            if (sourceReference is null)
            {
                return null;
            }

            return new ImportedTransaction
            {
                ExecutionDate = date.Value,
                Description = string.IsNullOrWhiteSpace(communication) ? "CODA transaction" : communication,
                Amount = amount,
                PaymentMethod = "Transfer",
                SourceReference = sourceReference,
                SourceSequence = BuildMovementSourceSequence(codaYear, paperNumber, transactionNumber, detail),
                SourceAccountNumber = ownAccountNumber
            };
        }
        catch
        {
            return null;
        }
    }

    private static DateOnly? ParseCodaDate(string value)
    {
        if (value.Length != 6)
        {
            return null;
        }

        if (!int.TryParse(value[0..2], out var day) ||
            !int.TryParse(value[2..4], out var month) ||
            !int.TryParse(value[4..6], out var year2))
        {
            return null;
        }

        var year = year2 >= 70 ? 1900 + year2 : 2000 + year2;
        try
        {
            return new DateOnly(year, month, day);
        }
        catch
        {
            return null;
        }
    }

    private static string SafeSubstring(string value, int start, int length)
    {
        if (start >= value.Length)
        {
            return string.Empty;
        }

        var max = Math.Min(length, value.Length - start);
        return value.Substring(start, max);
    }

    private static string CleanCodaText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, @"\s+[01]\s+[01]\s*$", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    private static string JoinTextFragments(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right.Trim();
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left.Trim();
        }

        var l = left.Trim();
        var r = right.Trim();
        var mergeWithoutSpace =
            char.IsLetter(l[^1]) &&
            char.IsLower(l[^1]) &&
            char.IsLetter(r[0]) &&
            char.IsLower(r[0]) &&
            r.Length <= 2;
        return mergeWithoutSpace ? $"{l}{r}" : $"{l} {r}";
    }

    private static string? BuildMovementSourceReference(string? accountNumber, int? year, string? paperNumber, string? transactionNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) ||
            !year.HasValue ||
            string.IsNullOrWhiteSpace(paperNumber) ||
            string.IsNullOrWhiteSpace(transactionNumber))
        {
            return null;
        }

        return $"{accountNumber}#{year:0000}#{paperNumber}#{transactionNumber}";
    }

    private static string? BuildMovementSourceSequence(int? year, string? paperNumber, string? transactionNumber, string detail)
    {
        if (!year.HasValue || string.IsNullOrWhiteSpace(paperNumber) || string.IsNullOrWhiteSpace(transactionNumber))
        {
            return string.IsNullOrWhiteSpace(transactionNumber) ? null : $"{transactionNumber}-{detail}";
        }

        return $"{year:0000}-{paperNumber}-{transactionNumber}";
    }

    private static string? TryExtractOwnAccountNumberFromRecord12(string line)
    {
        // Record 12: own account field at positions 6-39 (1-based), includes currency code.
        var field = SafeSubstring(line, 5, 34);
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        var compact = field.Trim();

        var ibanMatch = Regex.Match(compact, @"\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b", RegexOptions.IgnoreCase);
        if (ibanMatch.Success)
        {
            return ibanMatch.Value.ToUpperInvariant();
        }

        var domesticDashed = Regex.Match(compact, @"\b\d{3}-\d{7}-\d{2}(?:/\d{3})?\b");
        if (domesticDashed.Success)
        {
            return domesticDashed.Value;
        }

        var domesticPlain = Regex.Match(compact, @"(?<!\d)\d{12}(?!\d)");
        if (domesticPlain.Success)
        {
            var d = domesticPlain.Value;
            return $"{d[..3]}-{d.Substring(3, 7)}-{d.Substring(10, 2)}";
        }

        return null;
    }

    private static string? TryExtractPaperNumberFromRecord12(string line)
    {
        var candidate = SafeSubstring(line, 2, 3).Trim();
        return candidate.All(char.IsDigit) && candidate.Length == 3 ? candidate : null;
    }

    private static DateOnly? TryExtractDateFromRecord8(string line)
    {
        var ddmmyy = SafeSubstring(line, 57, 6).Trim();
        return ParseCodaDate(ddmmyy);
    }

    private static decimal? TryExtractClosingBalanceFromRecord8(string line)
    {
        var signRaw = SafeSubstring(line, 41, 1);
        var amountRaw = SafeSubstring(line, 42, 15).Trim();
        return ParseSignedCodaBalance(signRaw, amountRaw);
    }

    private static decimal? TryExtractOpeningBalanceFromRecord1(string line)
    {
        var signRaw = SafeSubstring(line, 42, 1);
        var amountRaw = SafeSubstring(line, 43, 15).Trim();
        return ParseSignedCodaBalance(signRaw, amountRaw);
    }

    private static decimal? ParseSignedCodaBalance(string signRaw, string amountRaw)
    {
        if (amountRaw.Length == 0 || !decimal.TryParse(amountRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milli))
        {
            return null;
        }

        var value = milli / 1000m;
        if (signRaw == "1")
        {
            value = -value;
        }

        return value;
    }
}
