namespace FinancialPlanningApp.Web.Data.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<long> CreateAsync(long userId, string tokenHash, DateTime expiresUtc, string? requestIp, string? userAgent, CancellationToken cancellationToken = default);
    Task<long?> GetValidUserIdAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> MarkUsedAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task ExpireOpenTokensAsync(long userId, CancellationToken cancellationToken = default);
}
