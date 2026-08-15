using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

public class ResetPasswordModel(IPasswordResetService passwordResetService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Completed { get; private set; }

    public sealed class InputModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;
        [Required, MinLength(8), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ModelState.AddModelError(string.Empty, "Resettoken ontbreekt.");
            return Page();
        }

        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await passwordResetService.ResetPasswordAsync(Input.Token, Input.NewPassword, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Wachtwoord resetten mislukt.");
            return Page();
        }

        Completed = true;
        return Page();
    }
}
