using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface IMailSettingsService
{
    Task<MailSettings> GetGlobalAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> SaveGlobalAsync(MailSettings settings, bool updateApiKey, bool updateSmtpPassword, CancellationToken cancellationToken = default);
}

public sealed class MailSettingsService(IMailSettingsRepository repository) : IMailSettingsService
{
    private static readonly HashSet<string> SupportedProviders = ["Disabled", "Brevo", "Resend", "Postmark", "SendGrid", "Mailgun", "CustomSmtp"];
    private static readonly HashSet<string> ApiProviders = ["Brevo", "Resend", "Postmark", "SendGrid", "Mailgun"];

    public Task<MailSettings> GetGlobalAsync(CancellationToken cancellationToken = default)
        => repository.GetGlobalAsync(cancellationToken);

    public async Task<(bool Success, string? Error)> SaveGlobalAsync(MailSettings settings, bool updateApiKey, bool updateSmtpPassword, CancellationToken cancellationToken = default)
    {
        settings.Provider = string.IsNullOrWhiteSpace(settings.Provider) ? "Disabled" : settings.Provider.Trim();
        if (!SupportedProviders.Contains(settings.Provider))
        {
            return (false, "Niet-ondersteunde mailprovider.");
        }

        settings.FromName = Normalize(settings.FromName);
        settings.FromEmail = Normalize(settings.FromEmail);
        settings.BaseUrl = Normalize(settings.BaseUrl)?.TrimEnd('/');
        settings.ApiKey = Normalize(settings.ApiKey);
        settings.SmtpHost = Normalize(settings.SmtpHost);
        settings.SmtpUsername = Normalize(settings.SmtpUsername);
        settings.SmtpPassword = Normalize(settings.SmtpPassword);

        var existing = await repository.GetGlobalAsync(cancellationToken);
        if (!updateApiKey)
        {
            settings.ApiKey = existing.ApiKey;
        }

        if (!updateSmtpPassword)
        {
            settings.SmtpPassword = existing.SmtpPassword;
        }

        if (settings.Provider == "Disabled")
        {
            settings.IsEnabled = false;
        }
        else
        {
            if (settings.IsEnabled && string.IsNullOrWhiteSpace(settings.FromEmail))
            {
                return (false, "Afzender e-mail is verplicht wanneer mail ingeschakeld is.");
            }

            if (settings.IsEnabled && ApiProviders.Contains(settings.Provider) && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                return (false, "API key is verplicht voor de geselecteerde mailprovider.");
            }

            if (settings.IsEnabled && settings.Provider == "CustomSmtp")
            {
                if (string.IsNullOrWhiteSpace(settings.SmtpHost))
                {
                    return (false, "SMTP-host is verplicht voor Custom SMTP.");
                }

                if (settings.SmtpPort is null or < 1 or > 65535)
                {
                    return (false, "SMTP-poort moet tussen 1 en 65535 liggen.");
                }
            }
        }

        var ok = await repository.SaveGlobalAsync(settings, cancellationToken);
        return ok ? (true, null) : (false, "Mailinstellingen opslaan mislukt.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
