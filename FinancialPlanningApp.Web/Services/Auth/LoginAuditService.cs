using FinancialPlanningApp.Web.Data.Models;
using FinancialPlanningApp.Web.Data.Repositories;

namespace FinancialPlanningApp.Web.Services.Auth;

public interface ILoginAuditService
{
    Task LogAttemptAsync(string? email, long? userId, bool isSuccess, string? failureReason, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoginAttempt>> ListAsync(DateTime? fromUtc, DateTime? toUtc, string? email, bool? isSuccess, int limit, CancellationToken cancellationToken = default);
}

public sealed class LoginAuditService(ILoginAuditRepository repository) : ILoginAuditService
{
    public Task LogAttemptAsync(string? email, long? userId, bool isSuccess, string? failureReason, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => repository.AddAsync(email, userId, isSuccess, failureReason, ipAddress, userAgent, cancellationToken);

    public Task<IReadOnlyList<LoginAttempt>> ListAsync(DateTime? fromUtc, DateTime? toUtc, string? email, bool? isSuccess, int limit, CancellationToken cancellationToken = default)
        => repository.ListAsync(fromUtc, toUtc, email, isSuccess, limit, cancellationToken);
}
