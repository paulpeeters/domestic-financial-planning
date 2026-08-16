using FinancialPlanningApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Pages.Account;

[Authorize]
public class ShutdownModel(
    IOptions<ApplicationModeOptions> applicationMode,
    IHostApplicationLifetime applicationLifetime) : PageModel
{
    public IActionResult OnGet()
        => applicationMode.Value.IsSingleUserDesktop ? Page() : NotFound();

    public IActionResult OnPost()
    {
        if (!applicationMode.Value.IsSingleUserDesktop)
        {
            return NotFound();
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            applicationLifetime.StopApplication();
        });

        return Page();
    }
}
