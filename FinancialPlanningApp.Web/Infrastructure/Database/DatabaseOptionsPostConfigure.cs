using FinancialPlanningApp.Web.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialPlanningApp.Web.Infrastructure.Database;

public sealed class DatabaseOptionsPostConfigure(SecretPlaceholderResolver resolver) : IPostConfigureOptions<DatabaseOptions>
{
    public void PostConfigure(string? name, DatabaseOptions options)
        => options.ConnectionString = resolver.Resolve(options.ConnectionString);
}
