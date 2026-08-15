using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize(Policy = "RequireGlobalAdmin")]
public class GlobalAdminsModel(IGlobalAdminService globalAdminService) : PageModel
{
    public IReadOnlyList<AppUser> Users { get; private set; } = [];
    public IReadOnlyList<Data.Models.TenantInfo> Tenants { get; private set; } = [];
    public bool AllowSelfRegistration { get; private set; } = true;

    [BindProperty]
    public UpdateInput Input { get; set; } = new();
    [BindProperty]
    public EditUserInput EditUser { get; set; } = new();
    [BindProperty]
    public UserActiveInput UserActive { get; set; } = new();
    [BindProperty]
    public CreateTenantInput TenantInput { get; set; } = new();
    [BindProperty]
    public TenantActiveInput TenantActive { get; set; } = new();
    [BindProperty]
    public PurgeTenantInput PurgeTenant { get; set; } = new();
    [BindProperty]
    public PurgeUserInput PurgeUser { get; set; } = new();
    [BindProperty]
    public AddMembershipInput MembershipInput { get; set; } = new();
    [BindProperty]
    public SelfRegistrationInput SelfRegistrationInputModel { get; set; } = new();
    [BindProperty]
    public CreateUserInput CreateUser { get; set; } = new();
    [BindProperty]
    public ResetPasswordInput ResetPassword { get; set; } = new();

    public sealed class UpdateInput
    {
        [Required]
        public long UserId { get; set; }
        public bool IsGlobalAdmin { get; set; }
    }
    public sealed class EditUserInput
    {
        [Required]
        public long UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Url]
        public string? AvatarUrl { get; set; }
        public bool IsGlobalAdmin { get; set; }
        public bool IsActive { get; set; } = true;
    }
    public sealed class UserActiveInput
    {
        [Required]
        public long UserId { get; set; }
        public bool IsActive { get; set; }
    }
    public sealed class CreateTenantInput
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [MaxLength(10)]
        public string? ShortName { get; set; }
    }
    public sealed class TenantActiveInput
    {
        [Required]
        public long TenantId { get; set; }
        public bool IsActive { get; set; }
    }
    public sealed class PurgeTenantInput
    {
        [Required]
        public long TenantId { get; set; }
        [Required]
        public int CaptchaAnswer { get; set; }
        [Required]
        public string ConfirmationText { get; set; } = string.Empty;
        public bool ConfirmDataLoss { get; set; }
    }
    public sealed class PurgeUserInput
    {
        [Required]
        public long UserId { get; set; }
        [Required]
        public int CaptchaAnswer { get; set; }
        [Required]
        public string ConfirmationText { get; set; } = string.Empty;
        public bool ConfirmDataLoss { get; set; }
    }
    public sealed class AddMembershipInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public long TenantId { get; set; }
        [Required]
        public string Role { get; set; } = "VIEWER";
        public bool IsActive { get; set; } = true;
    }
    public sealed class SelfRegistrationInput
    {
        public bool AllowSelfRegistration { get; set; }
    }
    public sealed class CreateUserInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required, MinLength(8), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Url]
        public string? AvatarUrl { get; set; }
        [Required]
        public long TenantId { get; set; }
        [Required]
        public string Role { get; set; } = "VIEWER";
        public bool IsActive { get; set; } = true;
        public bool IsGlobalAdmin { get; set; }
    }
    public sealed class ResetPasswordInput
    {
        [Required]
        public long UserId { get; set; }
        [Required, MinLength(8), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
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
            Users = await globalAdminService.ListUsersAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.SetGlobalAdminAsync(Input.UserId, Input.IsGlobalAdmin, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Globale admin bijwerken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateUserAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(EditUser, nameof(EditUser)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.UpdateUserProfileAsync(
            EditUser.UserId,
            EditUser.FirstName,
            EditUser.LastName,
            EditUser.AvatarUrl,
            cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Gebruikersprofiel bijwerken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        result = await globalAdminService.SetGlobalAdminAsync(EditUser.UserId, EditUser.IsGlobalAdmin, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Globale admin bijwerken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        result = await globalAdminService.SetUserActiveAsync(EditUser.UserId, EditUser.IsActive, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Actieve status van gebruiker bijwerken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateTenantAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(TenantInput, nameof(TenantInput)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.CreateTenantAsync(TenantInput.Name, TenantInput.ShortName, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Tenant maken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetTenantActiveAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(TenantActive, nameof(TenantActive)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.SetTenantActiveAsync(TenantActive.TenantId, TenantActive.IsActive, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Actieve status van tenant bijwerken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPurgeTenantAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(PurgeTenant, nameof(PurgeTenant)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var expectedCaptcha = GetPurgeCaptchaAnswer(PurgeTenant.TenantId);
        if (PurgeTenant.CaptchaAnswer != expectedCaptcha)
        {
            ModelState.AddModelError(string.Empty, "Captcha-antwoord is onjuist.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var expectedConfirmation = GetPurgeConfirmationText(PurgeTenant.TenantId);
        if (!string.Equals(PurgeTenant.ConfirmationText.Trim(), expectedConfirmation, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, $"Typ {expectedConfirmation} om definitief verwijderen van de tenant te bevestigen.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (!PurgeTenant.ConfirmDataLoss)
        {
            ModelState.AddModelError(string.Empty, "Bevestig dat je begrijpt dat dit tenantdata definitief verwijdert.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.PurgeTenantAsync(PurgeTenant.TenantId, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Tenant definitief verwijderen mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddMembershipAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(MembershipInput, nameof(MembershipInput)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.AddUserToTenantAsync(MembershipInput.Email, MembershipInput.TenantId, MembershipInput.Role, MembershipInput.IsActive, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Lidmaatschap toevoegen mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPurgeUserAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(PurgeUser, nameof(PurgeUser)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var expectedCaptcha = GetUserPurgeCaptchaAnswer(PurgeUser.UserId);
        if (PurgeUser.CaptchaAnswer != expectedCaptcha)
        {
            ModelState.AddModelError(string.Empty, "Captcha-antwoord is onjuist.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var expectedConfirmation = GetUserPurgeConfirmationText(PurgeUser.UserId);
        if (!string.Equals(PurgeUser.ConfirmationText.Trim(), expectedConfirmation, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, $"Typ {expectedConfirmation} om definitief verwijderen van de gebruiker te bevestigen.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (!PurgeUser.ConfirmDataLoss)
        {
            ModelState.AddModelError(string.Empty, "Bevestig dat je begrijpt dat dit gebruikersdata definitief verwijdert.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.PurgeUserAsync(PurgeUser.UserId, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Gebruiker definitief verwijderen mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSelfRegistrationAsync(CancellationToken cancellationToken)
    {
        await globalAdminService.SetAllowSelfRegistrationAsync(SelfRegistrationInputModel.AllowSelfRegistration, cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateUserAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(CreateUser, nameof(CreateUser)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.CreateUserAndAssignTenantAsync(
            CreateUser.Email,
            CreateUser.Password,
            CreateUser.FirstName,
            CreateUser.LastName,
            CreateUser.AvatarUrl,
            CreateUser.TenantId,
            CreateUser.Role,
            CreateUser.IsActive,
            CreateUser.IsGlobalAdmin,
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Gebruiker maken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetUserActiveAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(UserActive, nameof(UserActive)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.SetUserActiveAsync(UserActive.UserId, UserActive.IsActive, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Actieve status van gebruiker bijwerken mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(ResetPassword, nameof(ResetPassword)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var result = await globalAdminService.ResetUserPasswordAsync(
            ResetPassword.UserId,
            ResetPassword.NewPassword,
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Wachtwoord resetten mislukt.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Users = await globalAdminService.ListUsersAsync(cancellationToken);
        Tenants = await globalAdminService.ListTenantsAsync(cancellationToken);
        AllowSelfRegistration = await globalAdminService.GetAllowSelfRegistrationAsync(cancellationToken);
        SelfRegistrationInputModel = new SelfRegistrationInput { AllowSelfRegistration = AllowSelfRegistration };
    }

    public static string GetPurgeCaptchaQuestion(long tenantId)
    {
        var left = (int)(tenantId % 7) + 3;
        var right = (int)(tenantId % 5) + 4;
        return $"{left} + {right}";
    }

    public static int GetPurgeCaptchaAnswer(long tenantId)
    {
        var left = (int)(tenantId % 7) + 3;
        var right = (int)(tenantId % 5) + 4;
        return left + right;
    }

    public static string GetPurgeConfirmationText(long tenantId)
        => $"DELETE {tenantId}";

    public static string GetUserPurgeCaptchaQuestion(long userId)
    {
        var left = (int)(userId % 7) + 2;
        var right = (int)(userId % 5) + 3;
        return $"{left} + {right}";
    }

    public static int GetUserPurgeCaptchaAnswer(long userId)
    {
        var left = (int)(userId % 7) + 2;
        var right = (int)(userId % 5) + 3;
        return left + right;
    }

    public static string GetUserPurgeConfirmationText(long userId)
        => $"DELETE USER {userId}";
}
