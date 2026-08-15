using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

public class ForgotPasswordModel(IPasswordResetService passwordResetService, IMailSettingsService mailSettingsService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Submitted { get; private set; }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
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
}
