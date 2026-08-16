using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FinancialPlanningApp.Web.Services;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class UpdateCheckService(
    IHttpClientFactory httpClientFactory,
    IOptions<UpdateCheckOptions> options,
    ApplicationInfo applicationInfo,
    ILogger<UpdateCheckService> logger) : IUpdateCheckService
{
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private UpdateCheckResult? cachedResult;
    private DateTime cacheExpiresUtc;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.LatestReleaseUrl))
        {
            return UpdateCheckResult.Disabled(applicationInfo.Version);
        }

        if (cachedResult is not null && DateTime.UtcNow < cacheExpiresUtc)
        {
            return cachedResult;
        }

        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (cachedResult is not null && DateTime.UtcNow < cacheExpiresUtc)
            {
                return cachedResult;
            }

            cachedResult = await FetchLatestAsync(settings, cancellationToken);
            cacheExpiresUtc = DateTime.UtcNow.AddMinutes(Math.Max(settings.CacheMinutes, 5));
            return cachedResult;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private async Task<UpdateCheckResult> FetchLatestAsync(UpdateCheckOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 30)));

            var httpClient = httpClientFactory.CreateClient(nameof(UpdateCheckService));
            using var request = new HttpRequestMessage(HttpMethod.Get, settings.LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("DomesticFinancialPlanning");

            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return NotAvailable();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var latest = await JsonSerializer.DeserializeAsync<LatestReleaseInfo>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                timeoutCts.Token);

            if (latest is null || string.IsNullOrWhiteSpace(latest.Version))
            {
                return NotAvailable();
            }

            var currentVersion = NormalizeVersion(applicationInfo.Version);
            var latestVersion = NormalizeVersion(latest.Version);
            var updateAvailable = latestVersion is not null
                && currentVersion is not null
                && latestVersion > currentVersion;

            return new UpdateCheckResult
            {
                IsEnabled = true,
                IsAvailable = updateAvailable,
                CurrentVersion = applicationInfo.Version,
                LatestVersion = latest.Version,
                DownloadUrl = string.IsNullOrWhiteSpace(latest.DownloadUrl) ? null : latest.DownloadUrl,
                ReleaseNotesUrl = string.IsNullOrWhiteSpace(latest.ReleaseNotesUrl) ? settings.ReleasePageUrl : latest.ReleaseNotesUrl,
                Sha256 = string.IsNullOrWhiteSpace(latest.Sha256) ? null : latest.Sha256,
                CheckedUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            logger.LogDebug(ex, "Update check failed.");
            return NotAvailable();
        }
    }

    private UpdateCheckResult NotAvailable()
        => new()
        {
            IsEnabled = true,
            CurrentVersion = applicationInfo.Version,
            CheckedUtc = DateTime.UtcNow
        };

    private static Version? NormalizeVersion(string value)
    {
        var plusIndex = value.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            value = value[..plusIndex];
        }

        return Version.TryParse(value, out var version) ? version : null;
    }

    private sealed class LatestReleaseInfo
    {
        public string Version { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public string? ReleaseNotesUrl { get; set; }
        public string? Sha256 { get; set; }
    }
}
