using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

public class RegisterModel(
    IAuthService authService,
    IApplicationSettingsService applicationSettingsService,
    IDesktopBootstrapService desktopBootstrapService) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public sealed class RegisterInput
    {
        [Required, EmailAddress]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Url]
        public string? AvatarUrl { get; set; }

        [Required, DataType(DataType.Password), MinLength(8)]
        [Display(Name = "Wachtwoord")]
        public string Password { get; set; } = string.Empty;
    }

    public bool IsSelfRegistrationEnabled { get; private set; } = true;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (await desktopBootstrapService.IsSetupRequiredAsync(cancellationToken))
        {
            return RedirectToPage("/Account/DesktopSetup");
        }

        if (desktopBootstrapService.IsEnabled)
        {
            IsSelfRegistrationEnabled = false;
            return Page();
        }

        IsSelfRegistrationEnabled = await applicationSettingsService.GetAllowSelfRegistrationAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (await desktopBootstrapService.IsSetupRequiredAsync(cancellationToken))
        {
            return RedirectToPage("/Account/DesktopSetup");
        }

        if (desktopBootstrapService.IsEnabled)
        {
            ModelState.AddModelError(string.Empty, "Registratie is niet beschikbaar in desktopmodus.");
            IsSelfRegistrationEnabled = false;
            return Page();
        }

        IsSelfRegistrationEnabled = await applicationSettingsService.GetAllowSelfRegistrationAsync(cancellationToken);
        if (!IsSelfRegistrationEnabled)
        {
            ModelState.AddModelError(string.Empty, "Zelfregistratie is momenteel uitgeschakeld.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authService.RegisterAsync(Input.Email, Input.Password, Input.FirstName, Input.LastName, Input.AvatarUrl, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Registratie mislukt.");
            return Page();
        }

        return RedirectToPage("/Account/Login");
    }
}
