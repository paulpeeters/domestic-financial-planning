using FinancialPlanningApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class DesktopDataModel(IDesktopDataService desktopDataService) : PageModel
{
    public DesktopDataInfo DataInfo { get; private set; } = new();

    public IActionResult OnGet()
    {
        if (!desktopDataService.IsAvailable)
        {
            return NotFound();
        }

        DataInfo = desktopDataService.GetInfo();
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
}
