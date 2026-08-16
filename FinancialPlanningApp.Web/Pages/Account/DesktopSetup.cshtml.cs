using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinancialPlanningApp.Web.Pages.Account;

public class DesktopSetupModel(IDesktopBootstrapService desktopBootstrapService) : PageModel
{
    [BindProperty]
    public SetupInput Input { get; set; } = new();

    public bool IsAvailable { get; private set; }

    public sealed class SetupInput
    {
        [Required, EmailAddress]
        [Display(Name = "E-mail voor lokale aanmelding")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Voornaam")]
        public string? FirstName { get; set; }

        [Display(Name = "Naam")]
        public string? LastName { get; set; }

        [Required, DataType(DataType.Password), MinLength(8)]
        [Display(Name = "Wachtwoord")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        [Display(Name = "Wachtwoord bevestigen")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Naam van je huishouden")]
        public string TenantName { get; set; } = "Mijn huishouden";

        [StringLength(10)]
        [Display(Name = "Korte naam")]
        public string? TenantShortName { get; set; } = "Thuis";
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        IsAvailable = await desktopBootstrapService.IsSetupRequiredAsync(cancellationToken);
        if (!desktopBootstrapService.IsEnabled)
        {
            return RedirectToPage("/Account/Login");
        }

        if (!IsAvailable)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        IsAvailable = await desktopBootstrapService.IsSetupRequiredAsync(cancellationToken);
        if (!IsAvailable)
        {
            return RedirectToPage("/Index");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await desktopBootstrapService.BootstrapAsync(
            Input.Email,
            Input.Password,
            Input.FirstName,
            Input.LastName,
            Input.TenantName,
            Input.TenantShortName,
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Desktop setup mislukt.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.Email),
            new(AuthClaimTypes.TenantId, result.TenantId.ToString()),
            new(AuthClaimTypes.GlobalAdmin, "true")
        };
        if (!string.IsNullOrWhiteSpace(result.FirstName))
        {
            claims.Add(new(ClaimTypes.GivenName, result.FirstName));
        }
        if (!string.IsNullOrWhiteSpace(result.LastName))
        {
            claims.Add(new(ClaimTypes.Surname, result.LastName));
        }

        var identity = new ClaimsIdentity(claims, "AppCookie");
        await HttpContext.SignInAsync("AppCookie", new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }
}
