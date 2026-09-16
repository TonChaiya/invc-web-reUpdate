using System.Globalization;
using Invc.Infrastructure;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Read-only data access to the production INV database. There is deliberately no
// EF Core, no migrations and no database initialisation anywhere in this application.
builder.Services.AddInvcReadOnlyData(builder.Configuration);
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

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
