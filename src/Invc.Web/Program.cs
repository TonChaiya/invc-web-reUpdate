using System.Globalization;
using Invc.Infrastructure;
using Invc.Infrastructure.Data;
using Invc.Web;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Developer-local, untracked overrides (git-ignored: appsettings.*.local.json) — e.g. the application MySQL connection string.
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: false);

// Fail fast on invalid deployment configuration (shape only — never opens a database connection, so a
// temporary database outage cannot stop the application from starting).
var configProblems = ProductionConfigurationValidator.Validate(
    builder.Configuration[$"{InvDatabaseOptions.SectionName}:ConnectionString"],
    builder.Configuration["AllowedHosts"],
    builder.Environment.IsProduction());
if (configProblems.Count > 0)
{
    throw new InvalidOperationException("Invalid configuration for environment '" + builder.Environment.EnvironmentName + "': "
        + string.Join(" | ", configProblems));
}

// Read-only data access to the production INV database. There is deliberately no
// EF Core, no migrations and no database initialisation anywhere in this application.
builder.Services.AddInvcReadOnlyData(builder.Configuration);
// Application-owned MySQL database (invc_web) — the Borrow module's writable mirror. Schema comes from db/mysql via
// scripts/mysql-migrate.ps1, never from startup. SQL Server INV stays SELECT-only.
builder.Services.AddInvcAppData(builder.Configuration);
// External (DDNS) hosting of the same release runs anonymously and may only read; the internal Windows-authenticated
// application leaves the flag off. Enforced by the guard middleware below, not only in the UI.
builder.Services.AddSingleton(new ExternalAccessMode(builder.Configuration.GetValue<bool>(ExternalAccessMode.ConfigurationKey)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Invc.Web.Borrow.BorrowScreenService>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseInvcSecurityHeaders();

// External read-only hosting: refuse every write before routing (no-op for the internal application).
app.UseInvcExternalReadOnlyGuard();

// Legacy pages render Thai text and Buddhist-era years; pin the culture so output does not
// depend on the hosting server's regional settings.
var thai = new CultureInfo("th-TH");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(thai),
    SupportedCultures = [thai],
    SupportedUICultures = [thai],
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

/// <summary>Marker for test hosting (WebApplicationFactory).</summary>
public partial class Program;
