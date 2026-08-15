using System.Text.Json;

namespace FinancialPlanningApp.Web.Pages.Payments;

public sealed record PaymentEditNavigationSnapshot(
    IReadOnlyList<long> TemplateIds,
    string? Search,
    bool IncludeInactive,
    int PageSize,
    DateTime CreatedUtc);

public static class PaymentEditNavigationSession
{
    private const string Prefix = "PaymentEditNav:";

    public static string Store(ISession session, PaymentEditNavigationSnapshot snapshot)
    {
        var key = Guid.NewGuid().ToString("N");
        session.SetString(Prefix + key, JsonSerializer.Serialize(snapshot));
        return key;
    }

    public static PaymentEditNavigationSnapshot? Get(ISession session, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var json = session.GetString(Prefix + key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaymentEditNavigationSnapshot>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
