using FinancialPlanningApp.Web.Services.Imports;
using System.Globalization;

namespace FinancialPlanningApp.Web.Infrastructure.CsvParsing;

public sealed class EuropabankHistoryCsvImportAdapter : IBankImportAdapter
{
    public string ProviderKey => "CREDITCARD_CSV_EUROPABANK_HISTORY";

    public bool CanHandle(string fileName)
        => fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
           && fileName.Contains("HIST-", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ImportedTransaction>> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var rows = new List<ImportedTransaction>();
        var lineNumber = 0;
        var sequence = 0;
        var sourceCardNumber = ExtractCardIdentifier(fileName);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (lineNumber == 1 && line.StartsWith("Datum;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 3)
            {
                continue;
            }

            var dateRaw = parts[0].Trim();
            var description = parts[1].Trim();
            var amountRaw = parts[2].Trim();

            if (!DateOnly.TryParseExact(dateRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!TryParseAmount(amountRaw, out var amount))
            {
                continue;
            }

            var isSubtotal = description.Contains("Tussensaldo verrichtingen", StringComparison.OrdinalIgnoreCase);
            if (!isSubtotal)
            {
                sequence++;
            }

            rows.Add(new ImportedTransaction
            {
                ExecutionDate = date,
                Description = description,
                Amount = amount,
                PaymentMethod = "CreditCard",
                SourceReference = Path.GetFileName(fileName),
                SourceCardNumber = sourceCardNumber,
                SourceSequence = isSubtotal ? null : sequence.ToString(CultureInfo.InvariantCulture),
                IsInformational = isSubtotal,
                InfoType = isSubtotal ? "MONTHLY_SUBTOTAL" : null
            });
        }

        AddMonthlySubtotalChecks(rows, fileName, sourceCardNumber);

        // Source export is DESC; keep as-is for user familiarity in preview.
        return rows;
    }

    private static void AddMonthlySubtotalChecks(List<ImportedTransaction> rows, string fileName, string? sourceCardNumber)
    {
        var subtotalRows = rows.Where(r => r.IsInformational && r.InfoType == "MONTHLY_SUBTOTAL").ToList();
        foreach (var subtotal in subtotalRows)
        {
            var previousMonthDate = subtotal.ExecutionDate.AddMonths(-1);
            var monthTransactions = rows.Where(r =>
                !r.IsInformational &&
                r.ExecutionDate.Year == previousMonthDate.Year &&
                r.ExecutionDate.Month == previousMonthDate.Month).ToList();

            var sum = monthTransactions.Sum(t => t.Amount);
            var diff = Math.Round(sum - subtotal.Amount, 2);
            var isMatch = diff == 0m;
            var label = isMatch ? "CSV_SUBTOTAL_MATCH" : "CSV_SUBTOTAL_MISMATCH";
            var desc = isMatch
                ? $"CSV subtotal check {previousMonthDate:yyyy-MM}: sum(transactions) {sum:F2} matches subtotal {subtotal.Amount:F2}"
                : $"CSV subtotal check {previousMonthDate:yyyy-MM}: sum(transactions) {sum:F2} differs from subtotal {subtotal.Amount:F2} by {diff:F2}";

            rows.Add(new ImportedTransaction
            {
                ExecutionDate = subtotal.ExecutionDate,
                Description = desc,
                Amount = diff,
                PaymentMethod = "CreditCard",
                SourceReference = Path.GetFileName(fileName),
                SourceCardNumber = sourceCardNumber,
                IsInformational = true,
                InfoType = label
            });
        }
    }

    private static string? ExtractCardIdentifier(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var marker = "HIST-";
        var idx = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var token = name[(idx + marker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        var normalized = raw.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }
}
