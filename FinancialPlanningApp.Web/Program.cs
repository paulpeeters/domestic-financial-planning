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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Serilog;
using System.Globalization;

SqlMapper.AddTypeHandler(new SqlDateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<SecretPlaceholderResolver>();
builder.Services.ConfigureOptions<DatabaseOptionsPostConfigure>();
builder.Services.Configure<ReconciliationOptions>(builder.Configuration.GetSection("Reconciliation"));
builder.Services.AddSingleton<IDbConnectionFactory, MySqlDbConnectionFactory>();

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
builder.Services.AddHttpClient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<ITenantSessionService, TenantSessionService>();
builder.Services.AddScoped<ILoginAuditService, LoginAuditService>();
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

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseRequestLocalization(requestLocalizationOptions);
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
