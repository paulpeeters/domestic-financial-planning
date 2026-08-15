using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinancialPlanningApp.Web.Pages.Account;

public class LoginModel(IAuthService authService, ILoginAuditService loginAuditService) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public sealed class LoginInput
    {
        [Required, EmailAddress]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Display(Name = "Wachtwoord")]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var email = Input.Email?.Trim();
        var normalizedEmail = email?.ToLowerInvariant();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        if (!ModelState.IsValid)
        {
            await loginAuditService.LogAttemptAsync(normalizedEmail, null, false, "Invalid model state", ipAddress, userAgent, cancellationToken);
            return Page();
        }

        email ??= string.Empty;
        var result = await authService.ValidateCredentialsAsync(email, Input.Password, cancellationToken);
        if (!result.Success)
        {
            await loginAuditService.LogAttemptAsync(normalizedEmail, null, false, result.Error ?? "Login failed", ipAddress, userAgent, cancellationToken);
            ModelState.AddModelError(string.Empty, result.Error ?? "Aanmelden mislukt.");
            return Page();
        }

        await loginAuditService.LogAttemptAsync(normalizedEmail, result.UserId, true, null, ipAddress, userAgent, cancellationToken);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, email),
            new(AuthClaimTypes.TenantId, result.TenantId.ToString()),
            new(AuthClaimTypes.GlobalAdmin, result.IsGlobalAdmin ? "true" : "false")
        };
        if (!string.IsNullOrWhiteSpace(result.FirstName))
        {
            claims.Add(new(ClaimTypes.GivenName, result.FirstName));
        }
        if (!string.IsNullOrWhiteSpace(result.LastName))
        {
            claims.Add(new(ClaimTypes.Surname, result.LastName));
        }
        if (!string.IsNullOrWhiteSpace(result.AvatarUrl))
        {
            claims.Add(new("avatar_url", result.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, "AppCookie");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("AppCookie", principal);

        if (result.RequiresTenantSelection)
        {
            return RedirectToPage("/Account/Tenants");
        }

        return RedirectToPage("/Index");
    }
}
