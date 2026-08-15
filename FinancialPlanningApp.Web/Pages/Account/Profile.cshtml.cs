using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class ProfileModel(
    ITenantContextService tenantContextService,
    Data.Repositories.IUserRepository userRepository,
    IPasswordService passwordService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();
    [BindProperty]
    public PasswordInput PasswordInputModel { get; set; } = new();

    public sealed class InputModel
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Url]
        public string? AvatarUrl { get; set; }
    }
    public sealed class PasswordInput
    {
        [Required, DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;
        [Required, MinLength(8), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(tenantContextService.GetCurrentUserId(), cancellationToken);
        Input = new InputModel
        {
            FirstName = user?.FirstName,
            LastName = user?.LastName,
            AvatarUrl = user?.AvatarUrl
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            return Page();
        }

        await userRepository.UpdateProfileAsync(
            tenantContextService.GetCurrentUserId(),
            string.IsNullOrWhiteSpace(Input.FirstName) ? null : Input.FirstName.Trim(),
            string.IsNullOrWhiteSpace(Input.LastName) ? null : Input.LastName.Trim(),
            string.IsNullOrWhiteSpace(Input.AvatarUrl) ? null : Input.AvatarUrl.Trim(),
            cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(PasswordInputModel, nameof(PasswordInputModel)))
        {
            await LoadProfileAsync(cancellationToken);
            return Page();
        }

        var userId = tenantContextService.GetCurrentUserId();
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !passwordService.Verify(user.PasswordHash, PasswordInputModel.CurrentPassword))
        {
            ModelState.AddModelError(string.Empty, "Het huidige wachtwoord is onjuist.");
            await LoadProfileAsync(cancellationToken);
            return Page();
        }

        var passwordHash = passwordService.Hash(PasswordInputModel.NewPassword);
        await userRepository.UpdatePasswordHashAsync(userId, passwordHash, cancellationToken);

        TempData["StatusMessage"] = "Wachtwoord gewijzigd.";
        return RedirectToPage();
    }

    private async Task LoadProfileAsync(CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(tenantContextService.GetCurrentUserId(), cancellationToken);
        Input = new InputModel
        {
            FirstName = user?.FirstName,
            LastName = user?.LastName,
            AvatarUrl = user?.AvatarUrl
        };
    }
}
