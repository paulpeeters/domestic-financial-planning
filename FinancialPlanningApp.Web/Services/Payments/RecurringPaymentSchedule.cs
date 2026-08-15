using FinancialPlanningApp.Web.Data.Models;
using System.Globalization;
using System.Text.Json;

namespace FinancialPlanningApp.Web.Services.Payments;

public static class RecurringPaymentSchedule
{
    public const string CustomMonthsYearlyBudget = "CustomMonthsYearlyBudget";
    public const string FixedAmountMode = "Fixed";
    public const string MonthlyProfileAmountMode = "MonthlyProfile";

    public static IReadOnlyList<int> GetPaymentMonths(RecurringPaymentTemplate template)
    {
        if (string.Equals(template.Periodicity, CustomMonthsYearlyBudget, StringComparison.OrdinalIgnoreCase))
        {
            var customMonths = ParsePaymentMonths(template.PaymentMonths);
            return customMonths.Count > 0 ? customMonths : [template.PaymentMonth.GetValueOrDefault(1)];
        }

        if (string.Equals(template.Periodicity, "Yearly", StringComparison.OrdinalIgnoreCase))
        {
            return [template.PaymentMonth.GetValueOrDefault(1)];
        }

        return template.Periodicity switch
        {
            "Monthly" => Enumerable.Range(1, 12).ToList(),
            "BiMonthly" => [2, 4, 6, 8, 10, 12],
            "Quarterly" => [3, 6, 9, 12],
            "SemiAnnual" => [6, 12],
            _ => Enumerable.Range(1, 12).ToList()
        };
    }

    public static decimal GetOccurrenceAmount(RecurringPaymentTemplate template)
        => GetOccurrenceAmount(template, null);

    public static decimal GetOccurrenceAmount(RecurringPaymentTemplate template, int? month)
    {
        if (string.Equals(template.AmountMode, MonthlyProfileAmountMode, StringComparison.OrdinalIgnoreCase) &&
            month is >= 1 and <= 12)
        {
            return GetMonthlyProfileAmounts(template.MonthlyAmountsJson)[month.Value - 1];
        }

        if (!string.Equals(template.Periodicity, CustomMonthsYearlyBudget, StringComparison.OrdinalIgnoreCase))
        {
            return template.Amount;
        }

        var months = GetPaymentMonths(template);
        return months.Count == 0 ? template.Amount : template.Amount / months.Count;
    }

    public static decimal GetNormalizedMonthlyAmount(string periodicity, decimal amount, string? paymentMonths)
        => GetNormalizedMonthlyAmount(periodicity, amount, paymentMonths, FixedAmountMode, null);

    public static decimal GetNormalizedMonthlyAmount(string periodicity, decimal amount, string? paymentMonths, string? amountMode, string? monthlyAmountsJson)
    {
        if (string.Equals(amountMode, MonthlyProfileAmountMode, StringComparison.OrdinalIgnoreCase))
        {
            return GetMonthlyProfileAmounts(monthlyAmountsJson).Sum() / 12m;
        }

        if (string.Equals(periodicity, CustomMonthsYearlyBudget, StringComparison.OrdinalIgnoreCase))
        {
            return amount / 12m;
        }

        return periodicity switch
        {
            "Monthly" => amount,
            "BiMonthly" => amount / 2m,
            "Quarterly" => amount / 3m,
            "SemiAnnual" => amount / 6m,
            "Yearly" => amount / 12m,
            _ => amount
        };
    }

    public static string FormatPaymentSchedule(RecurringPaymentTemplate template)
    {
        var day = template.PaymentDay is > 0 and <= 31 ? template.PaymentDay.Value : 1;
        var months = GetPaymentMonths(template);
        var dayText = FormatDayOrdinal(day);

        if (months.Count == 12)
        {
            return $"{dayText} vd maand";
        }

        var monthText = string.Join(", ", months.Select(FormatMonthName));
        return months.Count == 1
            ? $"{dayText} {monthText}"
            : $"{dayText} in {monthText}";
    }

    public static string FormatPaymentMethod(string paymentMethod) => paymentMethod switch
    {
        "DirectDebit" => "Domiciliëring",
        "CreditCard" => "Kredietkaart",
        "Transfer" => "Overschrijving",
        _ => paymentMethod
    };

    public static string FormatPeriodicity(string periodicity) => periodicity switch
    {
        CustomMonthsYearlyBudget => "Aangepaste maanden",
        "Monthly" => "Maandelijks",
        "BiMonthly" => "Elke 2 maanden",
        "Quarterly" => "Per kwartaal",
        "SemiAnnual" => "Halfjaarlijks",
        "Yearly" => "Jaarlijks",
        _ => periodicity
    };

    public static string FormatDetailedSchedule(RecurringPaymentTemplate template)
    {
        var day = template.PaymentDay is > 0 and <= 31 ? template.PaymentDay.Value : 1;
        var lag = template.PaymentLagMonths == 0 ? string.Empty : $" / betaald {template.PaymentLagMonths} maand(en) later";
        if (string.Equals(template.AmountMode, MonthlyProfileAmountMode, StringComparison.OrdinalIgnoreCase))
        {
            return $"Dag {day} / maandprofiel{lag}";
        }

        if (string.Equals(template.Periodicity, CustomMonthsYearlyBudget, StringComparison.OrdinalIgnoreCase))
        {
            return $"Dag {day} / aangepaste maanden ({string.Join(", ", GetPaymentMonths(template))}) / jaarbudget{lag}";
        }

        return template.Periodicity switch
        {
            "Monthly" => $"Dag {day} / maandelijks{lag}",
            "BiMonthly" => $"Dag {day} / elke 2 maanden{lag}",
            "Quarterly" => $"Dag {day} / per kwartaal{lag}",
            "SemiAnnual" => $"Dag {day} / halfjaarlijks{lag}",
            "Yearly" => $"Dag {day}, maand {template.PaymentMonth.GetValueOrDefault(1)} / jaarlijks{lag}",
            _ => $"Dag {day} / {template.Periodicity}{lag}"
        };
    }

    public static string FormatCompactSchedule(RecurringPaymentTemplate template)
    {
        var day = template.PaymentDay is > 0 and <= 31 ? template.PaymentDay.Value : 1;
        var months = GetPaymentMonths(template);
        var monthText = FormatMonthSet(months);
        var lag = template.PaymentLagMonths == 0 ? string.Empty : $" +{template.PaymentLagMonths}m";

        return $"{day:00}/{monthText}{lag}";
    }

    private static string FormatMonthSet(IReadOnlyList<int> months)
    {
        var normalized = months
            .Where(m => m is >= 1 and <= 12)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        if (normalized.Count == 0)
        {
            return "[--]";
        }

        if (normalized.Count == 1)
        {
            return $"{normalized[0]:00}";
        }

        var isContiguous = normalized.Zip(normalized.Skip(1), (left, right) => right == left + 1).All(x => x);
        if (isContiguous)
        {
            return $"[{normalized.First():00}-{normalized.Last():00}]";
        }

        return $"[{string.Join(",", normalized.Select(m => m.ToString("00", CultureInfo.InvariantCulture)))}]";
    }

    private static string FormatDayOrdinal(int day)
        => day is 1 or 8 or 20 or 21 or 28 or 30 or 31 ? $"{day}ste" : $"{day}de";

    private static string FormatMonthName(int month)
        => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month).TrimEnd('.').ToLowerInvariant();

    public static DateOnly GetMappedPeriodForExecution(RecurringPaymentTemplate template, DateOnly executionDate)
    {
        var period = executionDate.AddMonths(-Math.Max(0, template.PaymentLagMonths));
        return new DateOnly(period.Year, period.Month, 1);
    }

    public static bool IsValidForMonth(RecurringPaymentTemplate template, int year, int month)
    {
        var period = new DateOnly(year, month, 1);
        return period >= new DateOnly(template.ActiveFrom.Year, template.ActiveFrom.Month, 1)
            && (template.ActiveUntil is null || period <= new DateOnly(template.ActiveUntil.Value.Year, template.ActiveUntil.Value.Month, 1));
    }

    public static bool IsCurrentlyActive(RecurringPaymentTemplate template, DateOnly today)
        => template.IsActive && today >= template.ActiveFrom && (template.ActiveUntil is null || today <= template.ActiveUntil.Value);

    public static bool HasValidOccurrenceInYear(RecurringPaymentTemplate template, int year)
    {
        for (var month = 1; month <= 12; month++)
        {
            if (GetPaymentMonths(template).Contains(month) && IsValidForMonth(template, year, month))
            {
                return true;
            }
        }

        return false;
    }

    public static decimal[] GetMonthlyProfileAmounts(string? monthlyAmountsJson)
    {
        if (string.IsNullOrWhiteSpace(monthlyAmountsJson))
        {
            return new decimal[12];
        }

        try
        {
            var values = JsonSerializer.Deserialize<decimal[]>(monthlyAmountsJson) ?? [];
            var result = new decimal[12];
            for (var i = 0; i < Math.Min(12, values.Length); i++)
            {
                result[i] = values[i];
            }

            return result;
        }
        catch (JsonException)
        {
            return new decimal[12];
        }
    }

    public static string NormalizeMonthlyProfileAmounts(IEnumerable<decimal> values)
    {
        var result = values.Take(12).Concat(Enumerable.Repeat(0m, 12)).Take(12).ToArray();
        return JsonSerializer.Serialize(result);
    }

    public static IReadOnlyList<int> ParsePaymentMonths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, CultureInfo.InvariantCulture, out var month) ? month : 0)
            .Where(x => x is >= 1 and <= 12)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public static string NormalizePaymentMonths(string? value)
        => string.Join(",", ParsePaymentMonths(value));
}
