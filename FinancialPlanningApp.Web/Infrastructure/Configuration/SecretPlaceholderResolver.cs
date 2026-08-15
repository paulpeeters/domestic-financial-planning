using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinancialPlanningApp.Web.Infrastructure.Configuration;

public sealed class SecretPlaceholderResolver
{
    private static readonly Regex PlaceholderPattern = new("@\\{(?<key>[A-Za-z0-9_:-]+)\\}", RegexOptions.Compiled);
    private readonly IReadOnlyDictionary<string, string> secrets;

    public SecretPlaceholderResolver(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["Secrets:Path"];
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "secrets.json")
            : configuredPath;

        secrets = LoadSecrets(path);
    }

    public string Resolve(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return PlaceholderPattern.Replace(value, match =>
        {
            var key = match.Groups["key"].Value;
            if (secrets.TryGetValue(key, out var secret) && !string.IsNullOrEmpty(secret))
            {
                return secret;
            }

            var environmentSecret = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(environmentSecret))
            {
                return environmentSecret;
            }

            throw new InvalidOperationException($"Missing secret value for placeholder '@{{{key}}}'. Add it to secrets.json or configure Secrets:Path.");
        });
    }

    private static IReadOnlyDictionary<string, string> LoadSecrets(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var stream = File.OpenRead(path);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        return new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }
}
