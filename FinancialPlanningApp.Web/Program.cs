using Dapper;
using FinancialPlanningApp.Web.BackgroundServices;
using FinancialPlanningApp.Web.Data.Repositories;
using FinancialPlanningApp.Web.Extensions;
using FinancialPlanningApp.Web.Infrastructure.BankImport;
using FinancialPlanningApp.Web.Infrastructure.Configuration;
using FinancialPlanningApp.Web.Infrastructure.Database;
using FinancialPlanningApp.Web.Infrastructure.PdfParsing;
using FinancialPlanningApp.Web.Services.Auth;
using FinancialPlanningApp.Web.Services;
using FinancialPlanningApp.Web.Services.Imports;
using FinancialPlanningApp.Web.Services.Payments;
using FinancialPlanningApp.Web.Services.Planning;
using FinancialPlanningApp.Web.Services.Reconciliation;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

SqlMapper.AddTypeHandler(new SqlDateOnlyTypeHandler());
SqlMapper.AddTypeHandler(new SqlDecimalTypeHandler());

var desktopModeMarkerPath = Path.Combine(AppContext.BaseDirectory, "desktop.mode");
var isDesktopEnvironment = File.Exists(desktopModeMarkerPath)
    || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Desktop", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Desktop", StringComparison.OrdinalIgnoreCase);
if (File.Exists(desktopModeMarkerPath))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Desktop");
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Desktop");
}

Mutex? desktopSingleInstanceMutex = null;
var desktopSingleInstanceAcquired = false;
if (isDesktopEnvironment)
{
    desktopSingleInstanceMutex = new Mutex(
        initiallyOwned: true,
        name: OperatingSystem.IsWindows() ? @"Local\DomesticFinancialPlanning.Desktop" : "DomesticFinancialPlanning.Desktop",
        createdNew: out desktopSingleInstanceAcquired);

    if (!desktopSingleInstanceAcquired)
    {
        TryOpenExistingDesktopInstance();
        desktopSingleInstanceMutex.Dispose();
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

var isSingleUserDesktopConfiguration = string.Equals(
    builder.Configuration.GetValue<string>($"{ApplicationModeOptions.SectionName}:Mode"),
    ApplicationModes.SingleUserDesktop,
    StringComparison.OrdinalIgnoreCase);
if (isSingleUserDesktopConfiguration)
{
    var preferredPort = builder.Configuration.GetValue<int?>($"{ApplicationModeOptions.SectionName}:PreferredDesktopPort") ?? 5196;
    var desktopPort = DesktopRuntime.ChoosePort(preferredPort);
    builder.WebHost.UseUrls($"http://127.0.0.1:{desktopPort}");
}

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
    configuration.ReadFrom.Services(services);
    configuration.Enrich.FromLogContext();
    configuration.WriteTo.Console();
});

builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        options.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(ApplicationInfo.FromAssembly(typeof(Program).Assembly));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication("AppCookie")
    .AddCookie("AppCookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireGlobalAdmin", policy =>
        policy.RequireClaim(AuthClaimTypes.GlobalAdmin, "true"));
});

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<ApplicationModeOptions>(builder.Configuration.GetSection(ApplicationModeOptions.SectionName));
builder.Services.Configure<UpdateCheckOptions>(builder.Configuration.GetSection(UpdateCheckOptions.SectionName));
builder.Services.AddSingleton<SecretPlaceholderResolver>();
builder.Services.ConfigureOptions<DatabaseOptionsPostConfigure>();
builder.Services.Configure<ReconciliationOptions>(builder.Configuration.GetSection("Reconciliation"));
builder.Services.AddSingleton<MySqlDbConnectionFactory>();
builder.Services.AddSingleton<SqliteDbConnectionFactory>();
builder.Services.AddSingleton<IDbConnectionFactory, ProviderDbConnectionFactory>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRecurringPaymentTemplateRepository, RecurringPaymentTemplateRepository>();
builder.Services.AddScoped<IPaymentTrackingRepository, PaymentTrackingRepository>();
builder.Services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
builder.Services.AddScoped<IImportSourceRegistryRepository, ImportSourceRegistryRepository>();
builder.Services.AddScoped<IAccountMonthlyBalanceRepository, AccountMonthlyBalanceRepository>();
builder.Services.AddScoped<ILoginAuditRepository, LoginAuditRepository>();
builder.Services.AddScoped<IMailSettingsRepository, MailSettingsRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantContextService, TenantContextService>();
builder.Services.AddScoped<ITenantMembershipService, TenantMembershipService>();
builder.Services.AddScoped<ITenantAdministrationService, TenantAdministrationService>();
builder.Services.AddScoped<IGlobalAdminService, GlobalAdminService>();
builder.Services.AddScoped<IHeaderContextService, HeaderContextService>();
builder.Services.AddScoped<IApplicationSettingsService, ApplicationSettingsService>();
builder.Services.AddScoped<IMailSettingsService, MailSettingsService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IEmailSender, EmailSender>();
builder.Services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<ITenantSessionService, TenantSessionService>();
builder.Services.AddScoped<ILoginAuditService, LoginAuditService>();
builder.Services.AddScoped<IDesktopBootstrapService, DesktopBootstrapService>();
builder.Services.AddScoped<IDesktopDataService, DesktopDataService>();
builder.Services.AddScoped<IRecurringPaymentService, RecurringPaymentService>();
builder.Services.AddScoped<IAnnualPlanningService, AnnualPlanningService>();
builder.Services.AddScoped<IPaymentTrackingService, PaymentTrackingService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<IImportSourceRegistryService, ImportSourceRegistryService>();
builder.Services.AddScoped<IAccountMonthlyBalanceService, AccountMonthlyBalanceService>();

builder.Services.AddScoped<IBankImportAdapter, CodaBankImportAdapter>();
builder.Services.AddScoped<IBankImportAdapter, EuropabankVisaPdfImportAdapter>();
builder.Services.AddScoped<IBankImportService, BankImportService>();

builder.Services.AddHostedService<DatabaseMigrationHostedService>();
builder.Services.AddHostedService<DesktopRuntimeHostedService>();

var app = builder.Build();
var isSingleUserDesktop = isSingleUserDesktopConfiguration;

var supportedCultures = new[] { new CultureInfo("nl-BE"), new CultureInfo("fr-BE"), new CultureInfo("en-BE") };
var requestLocalizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("nl-BE"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Docker/NPM deployments often use dynamic internal proxy IP ranges.
// Clearing these lets ASP.NET process proxy headers from the reverse proxy chain.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!isSingleUserDesktop)
{
    app.UseHttpsRedirection();
}
app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseRequestLocalization(requestLocalizationOptions);
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

try
{
    app.Run();
}
finally
{
    if (desktopSingleInstanceAcquired)
    {
        desktopSingleInstanceMutex?.ReleaseMutex();
    }

    desktopSingleInstanceMutex?.Dispose();
}

static void TryOpenExistingDesktopInstance()
{
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < deadline)
    {
        var runtimeInfo = TryReadDesktopRuntimeInfo();
        if (runtimeInfo is not null && IsProcessRunning(runtimeInfo.ProcessId))
        {
            if (ShouldOpenBrowserOnSecondStart())
            {
                TryOpenBrowser(runtimeInfo.Url);
            }
            return;
        }

        Thread.Sleep(250);
    }
}

static bool ShouldOpenBrowserOnSecondStart()
    => !string.Equals(Environment.GetEnvironmentVariable("Application__OpenBrowserOnStart"), "false", StringComparison.OrdinalIgnoreCase);

static DesktopRuntimeInfo? TryReadDesktopRuntimeInfo()
{
    try
    {
        var path = DesktopRuntime.GetRuntimeInfoPath();
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DesktopRuntimeInfo>(json);
    }
    catch
    {
        return null;
    }
}

static bool IsProcessRunning(int processId)
{
    try
    {
        return !Process.GetProcessById(processId).HasExited;
    }
    catch
    {
        return false;
    }
}

static void TryOpenBrowser(string url)
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return;
    }

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch
    {
        // Best effort only: the second instance should still exit.
    }
}
