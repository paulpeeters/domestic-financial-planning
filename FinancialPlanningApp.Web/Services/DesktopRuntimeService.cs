using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace FinancialPlanningApp.Web.Services;

public sealed class DesktopRuntimeInfo
{
    public string Url { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public DateTime StartedUtc { get; init; }
}

public static class DesktopRuntime
{
    public static int ChoosePort(int preferredPort)
    {
        var port = preferredPort is >= 1024 and <= 65535 ? preferredPort : 5196;
        return IsPortAvailable(port) ? port : FindAvailablePort();
    }

    public static string GetDataDirectory()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%");
        }

        return Path.Combine(localAppData, "DomesticFinancialPlanning");
    }

    public static string GetRuntimeInfoPath()
        => Path.Combine(GetDataDirectory(), "runtime.json");

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

public sealed class DesktopRuntimeHostedService(
    IOptions<ApplicationModeOptions> applicationMode,
    IHostApplicationLifetime lifetime,
    IServer server,
    ILogger<DesktopRuntimeHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!applicationMode.Value.IsSingleUserDesktop)
        {
            return Task.CompletedTask;
        }

        lifetime.ApplicationStarted.Register(() =>
        {
            var url = GetApplicationUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            WriteRuntimeInfo(url);
            if (applicationMode.Value.OpenBrowserOnStart)
            {
                OpenBrowser(url);
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (applicationMode.Value.IsSingleUserDesktop)
        {
            TryDeleteRuntimeInfo();
        }

        return Task.CompletedTask;
    }

    private string? GetApplicationUrl()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        return addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
    }

    private void WriteRuntimeInfo(string url)
    {
        try
        {
            var path = DesktopRuntime.GetRuntimeInfoPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var info = new DesktopRuntimeInfo
            {
                Url = url,
                ProcessId = Environment.ProcessId,
                StartedUtc = DateTime.UtcNow
            };
            File.WriteAllText(path, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write desktop runtime info.");
        }
    }

    private void TryDeleteRuntimeInfo()
    {
        try
        {
            var path = DesktopRuntime.GetRuntimeInfoPath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete desktop runtime info.");
        }
    }

    private void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open browser for desktop app at {Url}.", url);
        }
    }
}
