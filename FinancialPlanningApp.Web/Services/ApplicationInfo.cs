using System.Reflection;

namespace FinancialPlanningApp.Web.Services;

public sealed class ApplicationInfo
{
    public string Product { get; init; } = "Domestic Financial Planning";
    public string Company { get; init; } = "PWARE";
    public string Copyright { get; init; } = "© 2026 PWARE";
    public string Version { get; init; } = "1.0.0.0";
    public string Framework { get; init; } = "ASP.NET Razor Pages";

    public static ApplicationInfo FromAssembly(Assembly assembly)
    {
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();

        return new ApplicationInfo
        {
            Product = string.IsNullOrWhiteSpace(product) ? "Domestic Financial Planning" : product,
            Company = string.IsNullOrWhiteSpace(company) ? "PWARE" : company,
            Copyright = string.IsNullOrWhiteSpace(copyright) ? "© 2026 PWARE" : copyright,
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0.0" : version
        };
    }
}
