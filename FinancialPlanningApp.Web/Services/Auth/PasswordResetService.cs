using FinancialPlanningApp.Web.Data.Repositories;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface IPasswordResetService
{
    Task<(bool Success, string? Error)> RequestResetAsync(string email, string resetBaseUrl, string? requestIp, string? userAgent, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}

public sealed class PasswordResetService(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IMailSettingsService mailSettingsService,
    IEmailSender emailSender,
    IPasswordService passwordService) : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task<(bool Success, string? Error)> RequestResetAsync(string email, string resetBaseUrl, string? requestIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return (true, null);
        }

        var settings = await mailSettingsService.GetGlobalAsync(cancellationToken);
        if (!settings.IsEnabled)
        {
            return (false, "Mailverzending is uitgeschakeld.");
        }

        var token = CreateToken();
        var tokenHash = HashToken(token);
        await tokenRepository.ExpireOpenTokensAsync(user.Id, cancellationToken);
        await tokenRepository.CreateAsync(user.Id, tokenHash, DateTime.UtcNow.Add(TokenLifetime), requestIp, userAgent, cancellationToken);

        var resetUrl = QueryHelpers.AddQueryString(resetBaseUrl, "token", token);
        var request = new EmailSendRequest(
            normalizedEmail,
            "Reset your FinancialPlanningApp password",
            $"Use this link to reset your password. The link expires in 30 minutes: {resetUrl}",
            $"""
            <p>Use this link to reset your FinancialPlanningApp password.</p>
            <p><a href="{resetUrl}">Reset password</a></p>
            <p>This link expires in 30 minutes.</p>
            """);

        return await emailSender.SendAsync(settings, request, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "Resettoken ontbreekt.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return (false, "Het wachtwoord moet minstens 8 tekens lang zijn.");
        }

        var tokenHash = HashToken(token);
        var userId = await tokenRepository.GetValidUserIdAsync(tokenHash, cancellationToken);
        if (userId is null)
        {
            return (false, "De resetlink is ongeldig of vervallen.");
        }

        var passwordHash = passwordService.Hash(newPassword);
        var updated = await userRepository.UpdatePasswordHashAsync(userId.Value, passwordHash, cancellationToken);
        if (!updated)
        {
            return (false, "Wachtwoord bijwerken mislukt.");
        }

        await tokenRepository.MarkUsedAsync(tokenHash, cancellationToken);
        await tokenRepository.ExpireOpenTokensAsync(userId.Value, cancellationToken);
        return (true, null);
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
