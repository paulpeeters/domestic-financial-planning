namespace FinancialPlanningApp.Web.Data.Models;

public sealed class LoginAttempt
{
    public long Id { get; set; }
    public DateTime AttemptedUtc { get; set; }
    public string? Email { get; set; }
    public long? UserId { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
