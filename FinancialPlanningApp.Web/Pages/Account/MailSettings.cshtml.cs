using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize(Policy = "RequireGlobalAdmin")]
public class MailSettingsModel(IMailSettingsService mailSettingsService, IEmailSender emailSender) : PageModel
{
    public static readonly string[] Providers = ["Disabled", "Brevo", "Resend", "Postmark", "SendGrid", "Mailgun", "CustomSmtp"];

    [BindProperty]
    public InputModel Input { get; set; } = new();
    [BindProperty]
    public TestMailInput TestMail { get; set; } = new();

    public bool HasApiKey { get; private set; }
    public bool HasSmtpPassword { get; private set; }
    public DateTime? UpdatedUtc { get; private set; }

    public sealed class InputModel
    {
        public bool IsEnabled { get; set; }
        [Required]
        public string Provider { get; set; } = "Disabled";
        [MaxLength(128)]
        public string? FromName { get; set; }
        [EmailAddress, MaxLength(256)]
        public string? FromEmail { get; set; }
        [Url, MaxLength(512)]
        public string? BaseUrl { get; set; }
        [DataType(DataType.Password)]
        public string? ApiKey { get; set; }
        public bool ClearApiKey { get; set; }
        [MaxLength(256)]
        public string? SmtpHost { get; set; }
        [Range(1, 65535)]
        public int? SmtpPort { get; set; } = 587;
        [MaxLength(256)]
        public string? SmtpUsername { get; set; }
        [DataType(DataType.Password)]
        public string? SmtpPassword { get; set; }
        public bool ClearSmtpPassword { get; set; }
        public bool SmtpUseSsl { get; set; } = true;
    }
    public sealed class TestMailInput
    {
        [Required, EmailAddress]
        public string RecipientEmail { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            await LoadSecretStateAsync(cancellationToken);
            return Page();
        }

        var result = await SaveSettingsFromInputAsync(cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Mailinstellingen opslaan mislukt.");
            await LoadSecretStateAsync(cancellationToken);
            return Page();
        }

        TempData["StatusMessage"] = "Mailinstellingen opgeslagen.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendTestAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)) || !TryValidateModel(TestMail, nameof(TestMail)))
        {
            await LoadSecretStateAsync(cancellationToken);
            return Page();
        }

        var saveResult = await SaveSettingsFromInputAsync(cancellationToken);
        if (!saveResult.Success)
        {
            ModelState.AddModelError(string.Empty, saveResult.Error ?? "Mailinstellingen opslaan mislukt.");
            await LoadSecretStateAsync(cancellationToken);
            return Page();
        }

        var settings = await mailSettingsService.GetGlobalAsync(cancellationToken);
        var request = new EmailSendRequest(
            TestMail.RecipientEmail.Trim(),
            "FinancialPlanningApp testmail",
            $"Dit is een testmail verstuurd door FinancialPlanningApp op {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.",
            $"<p>Dit is een testmail verstuurd door <strong>FinancialPlanningApp</strong>.</p><p>UTC-tijd: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>");

        try
        {
            var result = await emailSender.SendAsync(settings, request, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Testmail versturen mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Testmail versturen mislukt: {ex.Message}");
            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData["StatusMessage"] = $"Testmail verstuurd naar {TestMail.RecipientEmail.Trim()}.";
        return RedirectToPage();
    }

    private Task<(bool Success, string? Error)> SaveSettingsFromInputAsync(CancellationToken cancellationToken)
    {
        var settings = new MailSettings
        {
            IsEnabled = Input.IsEnabled,
            Provider = Input.Provider,
            FromName = Input.FromName,
            FromEmail = Input.FromEmail,
            BaseUrl = Input.BaseUrl,
            ApiKey = Input.ClearApiKey ? null : Input.ApiKey,
            SmtpHost = Input.SmtpHost,
            SmtpPort = Input.SmtpPort,
            SmtpUsername = Input.SmtpUsername,
            SmtpPassword = Input.ClearSmtpPassword ? null : Input.SmtpPassword,
            SmtpUseSsl = Input.SmtpUseSsl
        };

        var updateApiKey = Input.ClearApiKey || !string.IsNullOrWhiteSpace(Input.ApiKey);
        var updateSmtpPassword = Input.ClearSmtpPassword || !string.IsNullOrWhiteSpace(Input.SmtpPassword);
        return mailSettingsService.SaveGlobalAsync(settings, updateApiKey, updateSmtpPassword, cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await mailSettingsService.GetGlobalAsync(cancellationToken);
        Input = new InputModel
        {
            IsEnabled = settings.IsEnabled,
            Provider = settings.Provider,
            FromName = settings.FromName,
            FromEmail = settings.FromEmail,
            BaseUrl = settings.BaseUrl,
            SmtpHost = settings.SmtpHost,
            SmtpPort = settings.SmtpPort ?? 587,
            SmtpUsername = settings.SmtpUsername,
            SmtpUseSsl = settings.SmtpUseSsl
        };
        HasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey);
        HasSmtpPassword = !string.IsNullOrWhiteSpace(settings.SmtpPassword);
        UpdatedUtc = settings.UpdatedUtc == default ? null : settings.UpdatedUtc;
    }

    private async Task LoadSecretStateAsync(CancellationToken cancellationToken)
    {
        var settings = await mailSettingsService.GetGlobalAsync(cancellationToken);
        HasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey);
        HasSmtpPassword = !string.IsNullOrWhiteSpace(settings.SmtpPassword);
        UpdatedUtc = settings.UpdatedUtc == default ? null : settings.UpdatedUtc;
    }
}
