using FinancialPlanningApp.Web.Data.Models;

namespace FinancialPlanningApp.Web.Data.Repositories;

public interface IMailSettingsRepository
{
    Task<MailSettings> GetGlobalAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveGlobalAsync(MailSettings settings, CancellationToken cancellationToken = default);
}
