using FinancialPlanningApp.Web.Data.Repositories;
using System.Globalization;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface IApplicationSettingsService
{
    Task<bool> GetAllowSelfRegistrationAsync(CancellationToken cancellationToken = default);
    Task SetAllowSelfRegistrationAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<int> GetMonthlyProvisionDayAsync(CancellationToken cancellationToken = default);
    Task SetMonthlyProvisionDayAsync(int day, CancellationToken cancellationToken = default);
    Task<decimal?> GetMonthlyProvisionAmountAsync(CancellationToken cancellationToken = default);
    Task SetMonthlyProvisionAmountAsync(decimal? amount, CancellationToken cancellationToken = default);
}

public sealed class ApplicationSettingsService(
    IUserRepository userRepository,
    ITenantContextService tenantContextService) : IApplicationSettingsService
{
    private const string SelfRegistrationKey = "allow_self_registration";
    private const string MonthlyProvisionDayKey = "monthly_provision_day";
    private const string MonthlyProvisionAmountKey = "monthly_provision_amount";

    public async Task<bool> GetAllowSelfRegistrationAsync(CancellationToken cancellationToken = default)
    {
        var value = await userRepository.GetAppSettingAsync(SelfRegistrationKey, cancellationToken);
        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    public Task SetAllowSelfRegistrationAsync(bool enabled, CancellationToken cancellationToken = default)
        => userRepository.SetAppSettingAsync(SelfRegistrationKey, enabled ? "true" : "false", cancellationToken);

    public async Task<int> GetMonthlyProvisionDayAsync(CancellationToken cancellationToken = default)
    {
        var value = await userRepository.GetAppSettingAsync(GetTenantSettingKey(MonthlyProvisionDayKey), cancellationToken);
        return int.TryParse(value, out var day) && day is >= 1 and <= 28 ? day : 1;
    }

    public Task SetMonthlyProvisionDayAsync(int day, CancellationToken cancellationToken = default)
        => userRepository.SetAppSettingAsync(GetTenantSettingKey(MonthlyProvisionDayKey), Math.Clamp(day, 1, 28).ToString(CultureInfo.InvariantCulture), cancellationToken);

    public async Task<decimal?> GetMonthlyProvisionAmountAsync(CancellationToken cancellationToken = default)
    {
        var value = await userRepository.GetAppSettingAsync(GetTenantSettingKey(MonthlyProvisionAmountKey), cancellationToken);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0m
            ? amount
            : null;
    }

    public Task SetMonthlyProvisionAmountAsync(decimal? amount, CancellationToken cancellationToken = default)
        => userRepository.SetAppSettingAsync(
            GetTenantSettingKey(MonthlyProvisionAmountKey),
            amount is > 0m ? amount.Value.ToString(CultureInfo.InvariantCulture) : null,
            cancellationToken);

    private string GetTenantSettingKey(string key)
        => $"tenant:{tenantContextService.GetCurrentTenantId()}:{key}";
}
