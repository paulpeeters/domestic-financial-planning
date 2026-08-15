using Microsoft.AspNetCore.Identity;

namespace FinancialPlanningApp.Web.Services.Auth;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string hashedPassword, string providedPassword)
        => _hasher.VerifyHashedPassword(new object(), hashedPassword, providedPassword) != PasswordVerificationResult.Failed;
}
