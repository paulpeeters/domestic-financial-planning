using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FinancialPlanningApp.Web.Services.Auth;

public sealed record DesktopRecoverySettings(bool IsConfigured, string? Question);
public sealed record DesktopRecoveryResult(bool Success, string? Error);

public interface IDesktopPasswordRecoveryService
{
    bool IsEnabled { get; }
    string GenerateRecoveryCode();
    Task<DesktopRecoverySettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task ConfigureAsync(string question, string answer, string recoveryCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> ListLocalUsersAsync(CancellationToken cancellationToken = default);
    Task<DesktopRecoveryResult> ResetPasswordAsync(
        string email,
        string recoveryAnswerOrCode,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed class DesktopPasswordRecoveryService(
    IOptions<ApplicationModeOptions> applicationMode,
    IUserRepository userRepository,
    IPasswordService passwordService) : IDesktopPasswordRecoveryService
{
    private const string RecoveryQuestionKey = "desktop_recovery_question";
    private const string RecoveryAnswerHashKey = "desktop_recovery_answer_hash";
    private const string RecoveryCodeHashKey = "desktop_recovery_code_hash";

    public bool IsEnabled => applicationMode.Value.IsSingleUserDesktop;

    public string GenerateRecoveryCode()
    {
        Span<byte> bytes = stackalloc byte[9];
        RandomNumberGenerator.Fill(bytes);
        var value = Convert.ToHexString(bytes);
        return $"DFP-{value[..6]}-{value[6..12]}-{value[12..18]}";
    }

    public async Task<DesktopRecoverySettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new DesktopRecoverySettings(false, null);
        }

        var question = await userRepository.GetAppSettingAsync(RecoveryQuestionKey, cancellationToken);
        var answerHash = await userRepository.GetAppSettingAsync(RecoveryAnswerHashKey, cancellationToken);
        var codeHash = await userRepository.GetAppSettingAsync(RecoveryCodeHashKey, cancellationToken);
        return new DesktopRecoverySettings(
            !string.IsNullOrWhiteSpace(question)
            && !string.IsNullOrWhiteSpace(answerHash)
            && !string.IsNullOrWhiteSpace(codeHash),
            question);
    }

    public async Task ConfigureAsync(string question, string answer, string recoveryCode, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        await userRepository.SetAppSettingAsync(RecoveryQuestionKey, question.Trim(), cancellationToken);
        await userRepository.SetAppSettingAsync(RecoveryAnswerHashKey, passwordService.Hash(NormalizeAnswer(answer)), cancellationToken);
        await userRepository.SetAppSettingAsync(RecoveryCodeHashKey, passwordService.Hash(NormalizeRecoveryCode(recoveryCode)), cancellationToken);
    }

    public async Task<IReadOnlyList<AppUser>> ListLocalUsersAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Array.Empty<AppUser>();
        }

        var users = await userRepository.ListUsersAsync(cancellationToken);
        return users.Where(user => user.IsActive).ToList();
    }

    public async Task<DesktopRecoveryResult> ResetPasswordAsync(
        string email,
        string recoveryAnswerOrCode,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return Failure("Lokaal wachtwoordherstel is alleen beschikbaar in desktopmodus.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.IsConfigured)
        {
            return Failure("Wachtwoordherstel is nog niet ingesteld voor deze lokale installatie.");
        }

        var user = await userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Failure("Geen actieve lokale gebruiker gevonden voor deze aanmelding.");
        }

        var answerHash = await userRepository.GetAppSettingAsync(RecoveryAnswerHashKey, cancellationToken);
        var codeHash = await userRepository.GetAppSettingAsync(RecoveryCodeHashKey, cancellationToken);
        var providedAnswer = NormalizeAnswer(recoveryAnswerOrCode);
        var providedCode = NormalizeRecoveryCode(recoveryAnswerOrCode);

        var answerMatches = !string.IsNullOrWhiteSpace(answerHash)
            && passwordService.Verify(answerHash, providedAnswer);
        var codeMatches = !string.IsNullOrWhiteSpace(codeHash)
            && passwordService.Verify(codeHash, providedCode);
        if (!answerMatches && !codeMatches)
        {
            return Failure("Het herstelantwoord of de herstelcode is niet correct.");
        }

        await userRepository.UpdatePasswordHashAsync(user.Id, passwordService.Hash(newPassword), cancellationToken);
        return new DesktopRecoveryResult(true, null);
    }

    private static DesktopRecoveryResult Failure(string error) => new(false, error);

    private static string NormalizeAnswer(string value)
        => value.Trim().ToUpperInvariant();

    private static string NormalizeRecoveryCode(string value)
        => value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
