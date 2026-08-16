using FinancialPlanningApp.Web.Services;
using FinancialPlanningApp.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class DesktopDataModel(
    IDesktopDataService desktopDataService,
    IApplicationSettingsService applicationSettingsService) : PageModel
{
    public DesktopDataInfo DataInfo { get; private set; } = new();

    [BindProperty]
    public ProvisionSettingsInput ProvisionSettings { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public sealed class ProvisionSettingsInput
    {
        [Range(1, 28)]
        public int MonthlyProvisionDay { get; set; } = 1;

        [Range(0, 9999999)]
        public decimal? MonthlyProvisionAmount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!desktopDataService.IsAvailable)
        {
            return NotFound();
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostBackupAsync(CancellationToken cancellationToken)
    {
        if (!desktopDataService.IsAvailable)
        {
            return NotFound();
        }

        var backupPath = await desktopDataService.CreateBackupAsync(cancellationToken);
        var fileName = Path.GetFileName(backupPath);
        return PhysicalFile(backupPath, "application/vnd.sqlite3", fileName);
    }

    public async Task<IActionResult> OnPostProvisionSettingsAsync(CancellationToken cancellationToken)
    {
        if (!desktopDataService.IsAvailable)
        {
            return NotFound();
        }

        ModelState.Clear();
        if (!TryValidateModel(ProvisionSettings, nameof(ProvisionSettings)))
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        await applicationSettingsService.SetMonthlyProvisionDayAsync(ProvisionSettings.MonthlyProvisionDay, cancellationToken);
        await applicationSettingsService.SetMonthlyProvisionAmountAsync(ProvisionSettings.MonthlyProvisionAmount, cancellationToken);
        StatusMessage = "Provisie-instellingen opgeslagen.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        DataInfo = desktopDataService.GetInfo();
        ProvisionSettings = new ProvisionSettingsInput
        {
            MonthlyProvisionDay = await applicationSettingsService.GetMonthlyProvisionDayAsync(cancellationToken),
            MonthlyProvisionAmount = await applicationSettingsService.GetMonthlyProvisionAmountAsync(cancellationToken)
        };
    }
}
