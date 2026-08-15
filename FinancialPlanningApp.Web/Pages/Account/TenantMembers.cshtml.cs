using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class TenantMembersModel(
    ITenantAdministrationService tenantAdministrationService,
    IApplicationSettingsService applicationSettingsService) : PageModel
{
    private static readonly string[] Roles = ["OWNER", "ADMIN", "EDITOR", "VIEWER"];

    public IReadOnlyList<TenantMember> Members { get; private set; } = [];
    public IReadOnlyList<AppUser> UsersNotInTenant { get; private set; } = [];
    public Data.Models.TenantInfo? TenantInfo { get; private set; }
    public int MonthlyProvisionDay { get; private set; } = 1;
    public decimal? MonthlyProvisionAmount { get; private set; }
    public IReadOnlyList<string> AvailableRoles => Roles;

    [BindProperty]
    public AddMemberInput AddInput { get; set; } = new();

    [BindProperty]
    public UpdateMemberInput UpdateInput { get; set; } = new();
    [BindProperty]
    public UpdateTenantInput TenantInput { get; set; } = new();
    [BindProperty]
    public ProvisionSettingsInput ProvisionSettings { get; set; } = new();
    [BindProperty]
    public CreateUserInput CreateUserInputModel { get; set; } = new();
    [BindProperty]
    public long RemoveUserId { get; set; }
    [BindProperty]
    public ResetPasswordInput ResetPassword { get; set; } = new();

    public sealed class AddMemberInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "VIEWER";

        public bool IsActive { get; set; } = true;
    }

    public sealed class UpdateMemberInput
    {
        [Required]
        public long UserId { get; set; }

        [Required]
        public string Role { get; set; } = "VIEWER";

        public bool IsActive { get; set; } = true;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Url]
        public string? AvatarUrl { get; set; }
    }

    public sealed class UpdateTenantInput
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [MaxLength(10)]
        public string? ShortName { get; set; }
    }
    public sealed class ProvisionSettingsInput
    {
        [Range(1, 28)]
        public int MonthlyProvisionDay { get; set; } = 1;
        [Range(0, 9999999)]
        public decimal? MonthlyProvisionAmount { get; set; }
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
        public string Role { get; set; } = "VIEWER";
        public bool IsActive { get; set; } = true;
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(cancellationToken);
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(AddInput, nameof(AddInput)))
        {
            await LoadSafeAsync(cancellationToken);
            return Page();
        }

        try
        {
            var result = await tenantAdministrationService.AddOrUpdateMemberByEmailAsync(
                AddInput.Email,
                AddInput.Role,
                AddInput.IsActive,
                cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Lid toevoegen mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(UpdateInput, nameof(UpdateInput)))
        {
            await LoadSafeAsync(cancellationToken);
            return Page();
        }

        try
        {
            var result = await tenantAdministrationService.UpdateMemberAsync(
                UpdateInput.UserId,
                UpdateInput.Role,
                UpdateInput.IsActive,
                cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Lid bijwerken mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }

            result = await tenantAdministrationService.UpdateMemberDisplayAsync(
                UpdateInput.UserId,
                UpdateInput.FirstName,
                UpdateInput.LastName,
                UpdateInput.AvatarUrl,
                cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Lidprofiel bijwerken mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateTenantAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(TenantInput, nameof(TenantInput)))
        {
            await LoadSafeAsync(cancellationToken);
            return Page();
        }

        try
        {
            var result = await tenantAdministrationService.UpdateCurrentTenantDisplayAsync(TenantInput.Name, TenantInput.ShortName, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Tenant bijwerken mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateProvisionSettingsAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(ProvisionSettings, nameof(ProvisionSettings)))
        {
            await LoadSafeAsync(cancellationToken);
            return Page();
        }

        try
        {
            await tenantAdministrationService.GetCurrentTenantInfoAsync(cancellationToken);
            await applicationSettingsService.SetMonthlyProvisionDayAsync(ProvisionSettings.MonthlyProvisionDay, cancellationToken);
            await applicationSettingsService.SetMonthlyProvisionAmountAsync(ProvisionSettings.MonthlyProvisionAmount, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateUserAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(CreateUserInputModel, nameof(CreateUserInputModel)))
        {
            await LoadSafeAsync(cancellationToken);
            return Page();
        }

        try
        {
            var result = await tenantAdministrationService.CreateUserAndAddToCurrentTenantAsync(
                CreateUserInputModel.Email,
                CreateUserInputModel.Password,
                CreateUserInputModel.FirstName,
                CreateUserInputModel.LastName,
                CreateUserInputModel.AvatarUrl,
                CreateUserInputModel.Role,
                CreateUserInputModel.IsActive,
                cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Gebruiker maken mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await tenantAdministrationService.RemoveMemberFromCurrentTenantAsync(RemoveUserId, cancellationToken);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Lid verwijderen mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!TryValidateModel(ResetPassword, nameof(ResetPassword)))
        {
            await LoadSafeAsync(cancellationToken);
            return Page();
        }

        try
        {
            var result = await tenantAdministrationService.ResetMemberPasswordAsync(
                ResetPassword.UserId,
                ResetPassword.NewPassword,
                cancellationToken);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Wachtwoord resetten mislukt.");
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Members = await tenantAdministrationService.ListMembersForCurrentTenantAsync(cancellationToken);
        UsersNotInTenant = await tenantAdministrationService.ListUsersNotInCurrentTenantAsync(cancellationToken);
        TenantInfo = await tenantAdministrationService.GetCurrentTenantInfoAsync(cancellationToken);
        TenantInput = new UpdateTenantInput
        {
            Name = TenantInfo?.Name ?? string.Empty,
            ShortName = TenantInfo?.ShortName
        };
        MonthlyProvisionDay = await applicationSettingsService.GetMonthlyProvisionDayAsync(cancellationToken);
        MonthlyProvisionAmount = await applicationSettingsService.GetMonthlyProvisionAmountAsync(cancellationToken);
        ProvisionSettings = new ProvisionSettingsInput
        {
            MonthlyProvisionDay = MonthlyProvisionDay,
            MonthlyProvisionAmount = MonthlyProvisionAmount
        };
    }

    private async Task LoadSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            Members = [];
            UsersNotInTenant = [];
        }
    }
}
