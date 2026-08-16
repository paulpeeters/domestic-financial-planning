using System.Reflection;

namespace FinancialPlanningApp.Web.Services;

public sealed class ApplicationInfo
{
    public string Product { get; init; } = "Domestic Financial Planning";
    public string Company { get; init; } = "PWARE";
    public string Copyright { get; init; } = "© 2026 PWARE";
    public string Version { get; init; } = "1.0.2.0";
    public string Framework { get; init; } = "ASP.NET Razor Pages";
    public string License { get; init; } = "GNU AGPL v3.0 or later";
    public string SourceUrl { get; init; } = "https://github.com/paulpeeters/domestic-financial-planning";

    public static ApplicationInfo FromAssembly(Assembly assembly)
    {
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        var version = assembly.GetName().Version?.ToString();

        return new ApplicationInfo
        {
            Product = string.IsNullOrWhiteSpace(product) ? "Domestic Financial Planning" : product,
            Company = string.IsNullOrWhiteSpace(company) ? "PWARE" : company,
            Copyright = string.IsNullOrWhiteSpace(copyright) ? "© 2026 PWARE" : copyright,
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.2.0" : version
        };
    }
}
