namespace FinancialPlanningApp.Web.Services.Auth;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hashedPassword, string providedPassword);
}
