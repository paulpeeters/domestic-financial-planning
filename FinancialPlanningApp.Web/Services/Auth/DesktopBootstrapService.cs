using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Services.Auth;

public sealed record DesktopBootstrapResult(
    bool Success,
    long UserId,
    long TenantId,
    string Email,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    string? RecoveryCode,
    string? Error);

public interface IDesktopBootstrapService
{
    bool IsEnabled { get; }
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);
    Task<DesktopBootstrapResult> BootstrapAsync(
        string email,
        string password,
        string? firstName,
        string? lastName,
        string tenantName,
        string? tenantShortName,
        string recoveryQuestion,
        string recoveryAnswer,
        CancellationToken cancellationToken = default);
}

public sealed class DesktopBootstrapService(
    IOptions<ApplicationModeOptions> applicationMode,
    IUserRepository userRepository,
    IPasswordService passwordService,
    IDesktopPasswordRecoveryService desktopPasswordRecoveryService) : IDesktopBootstrapService
{
    public bool IsEnabled => applicationMode.Value.IsSingleUserDesktop;

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
        => IsEnabled && await userRepository.CountUsersAsync(cancellationToken) == 0;

    public async Task<DesktopBootstrapResult> BootstrapAsync(
        string email,
        string password,
        string? firstName,
        string? lastName,
        string tenantName,
        string? tenantShortName,
        string recoveryQuestion,
        string recoveryAnswer,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Failure("Desktop setup is alleen beschikbaar in SingleUserDesktop modus.");
        }

        if (await userRepository.CountUsersAsync(cancellationToken) > 0)
        {
            return Failure("Desktop setup is al uitgevoerd.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var passwordHash = passwordService.Hash(password);
        var userId = await userRepository.CreateAsync(
            normalizedEmail,
            passwordHash,
            Normalize(firstName),
            Normalize(lastName),
            null,
            cancellationToken);

        await userRepository.EnsureDefaultTenantForUserAsync(userId, normalizedEmail, cancellationToken);
        await userRepository.SetGlobalAdminAsync(userId, true, cancellationToken);

        var tenantId = await userRepository.GetDefaultTenantIdAsync(userId, cancellationToken);
        if (tenantId is null)
        {
            return Failure("De lokale tenant kon niet worden aangemaakt.");
        }

        await userRepository.SetPreferredTenantAsync(userId, tenantId.Value, cancellationToken);
        await userRepository.UpdateTenantDisplayAsync(
            tenantId.Value,
            string.IsNullOrWhiteSpace(tenantName) ? "Lokaal huishouden" : tenantName.Trim(),
            Normalize(tenantShortName),
            cancellationToken);

        var recoveryCode = desktopPasswordRecoveryService.GenerateRecoveryCode();
        await desktopPasswordRecoveryService.ConfigureAsync(
            recoveryQuestion,
            recoveryAnswer,
            recoveryCode,
            cancellationToken);

        return new DesktopBootstrapResult(
            true,
            userId,
            tenantId.Value,
            normalizedEmail,
            Normalize(firstName),
            Normalize(lastName),
            null,
            recoveryCode,
            null);
    }

    private static DesktopBootstrapResult Failure(string error)
        => new(false, 0, 0, string.Empty, null, null, null, null, error);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
