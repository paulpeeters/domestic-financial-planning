using FinancialPlanningApp.Web.Services.Auth;
using FinancialPlanningApp.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

public class ForgotPasswordModel(
    IPasswordResetService passwordResetService,
    IMailSettingsService mailSettingsService,
    IOptions<ApplicationModeOptions> applicationMode,
    IDesktopPasswordRecoveryService desktopPasswordRecoveryService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public DesktopInputModel DesktopInput { get; set; } = new();

    public bool Submitted { get; private set; }
    public bool DesktopCompleted { get; private set; }
    public bool IsDesktopMode => applicationMode.Value.IsSingleUserDesktop;
    public DesktopRecoverySettings DesktopRecoverySettings { get; private set; } = new(false, null);
    public IReadOnlyList<string> LocalUserEmails { get; private set; } = Array.Empty<string>();

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class DesktopInputModel
    {
        [Required, EmailAddress]
        [Display(Name = "Aanmelding")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Herstelantwoord of herstelcode")]
        public string RecoveryAnswerOrCode { get; set; } = string.Empty;

        [Required, MinLength(8), DataType(DataType.Password)]
        [Display(Name = "Nieuw wachtwoord")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
        [Display(Name = "Nieuw wachtwoord bevestigen")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDesktopRecoveryAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (IsDesktopMode)
        {
            ModelState.Clear();
            if (!TryValidateModel(DesktopInput, nameof(DesktopInput)))
            {
                await LoadDesktopRecoveryAsync(cancellationToken);
                return Page();
            }

            var desktopResult = await desktopPasswordRecoveryService.ResetPasswordAsync(
                DesktopInput.Email,
                DesktopInput.RecoveryAnswerOrCode,
                DesktopInput.NewPassword,
                cancellationToken);
            if (!desktopResult.Success)
            {
                ModelState.AddModelError(string.Empty, desktopResult.Error ?? "Wachtwoord resetten mislukt.");
                await LoadDesktopRecoveryAsync(cancellationToken);
                return Page();
            }

            DesktopCompleted = true;
            await LoadDesktopRecoveryAsync(cancellationToken);
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var mailSettings = await mailSettingsService.GetGlobalAsync(cancellationToken);
        var resetBaseUrl = !string.IsNullOrWhiteSpace(mailSettings.BaseUrl)
            ? $"{mailSettings.BaseUrl.TrimEnd('/')}/Account/ResetPassword"
            : Url.PageLink("/Account/ResetPassword") ?? $"{Request.Scheme}://{Request.Host}/Account/ResetPassword";
        var result = await passwordResetService.RequestResetAsync(
            Input.Email,
            resetBaseUrl,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Resetmail versturen mislukt.");
            return Page();
        }

        Submitted = true;
        return Page();
    }

    private async Task LoadDesktopRecoveryAsync(CancellationToken cancellationToken)
    {
        if (!IsDesktopMode)
        {
            return;
        }

        DesktopRecoverySettings = await desktopPasswordRecoveryService.GetSettingsAsync(cancellationToken);
        LocalUserEmails = (await desktopPasswordRecoveryService.ListLocalUsersAsync(cancellationToken))
            .Select(user => user.Email)
            .ToList();
    }
}
