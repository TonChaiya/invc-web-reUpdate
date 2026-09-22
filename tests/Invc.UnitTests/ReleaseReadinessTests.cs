using System.Diagnostics;
using System.Text.RegularExpressions;
using Invc.Core.Diagnostics;
using Invc.Infrastructure.Data;
using Invc.Web;
using Microsoft.AspNetCore.Http;

namespace Invc.UnitTests;

/// <summary>Phase 7 — production configuration, disclosure, headers, publish specification and release SQL assertion.</summary>
public class ProductionConfigurationValidatorTests
{
    private const string Good = "Server=db01;Database=INV;User ID=invc_web_ro;Password=x;Encrypt=true";

    [Fact]
    public void Valid_production_configuration_has_no_problems()
        => Assert.Empty(ProductionConfigurationValidator.Validate(Good, "invc.example.local;invc", true));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_is_reported(string? cs)
        => Assert.Contains(ProductionConfigurationValidator.Validate(cs, "host", true), p => p.Contains("missing"));

    [Fact]
    public void Sa_login_is_rejected()
        => Assert.Contains(ProductionConfigurationValidator.Validate("Server=db01;Database=INV;User ID=sa;Password=x", "host", true), p => p.Contains("'sa'"));

    [Fact]
    public void Missing_database_is_rejected()
        => Assert.Contains(ProductionConfigurationValidator.Validate("Server=db01;Integrated Security=true", "host", true), p => p.Contains("Initial Catalog"));

    [Fact]
    public void Malformed_connection_string_is_rejected()
        => Assert.Contains(ProductionConfigurationValidator.Validate("this is not a connection string ;;== ", "host", true), p => p.Contains("not a valid"));

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invc.local;*")]
    public void Wildcard_or_missing_AllowedHosts_rejected_in_production(string? hosts)
        => Assert.Contains(ProductionConfigurationValidator.Validate(Good, hosts, true), p => p.Contains("AllowedHosts"));

    [Fact]
    public void Wildcard_AllowedHosts_allowed_outside_production()
        => Assert.Empty(ProductionConfigurationValidator.Validate(Good, "*", false));

    [Fact]
    public void Validation_does_not_touch_the_network()
    {
        var sw = Stopwatch.StartNew();
        ProductionConfigurationValidator.Validate("Server=10.255.255.1,1;Database=INV;Integrated Security=true;Connect Timeout=30", "host", true);
        Assert.True(sw.ElapsedMilliseconds < 1000, "validator must be pure configuration checking");
    }
}

public class ConnectionFactoryReleaseTests
{
    [Fact]
    public void Configured_ReadWrite_intent_cannot_override_ReadOnly()
    {
        var cs = ReadOnlySqlConnectionFactory.BuildReadOnlyConnectionString("Server=.;Database=INV;Integrated Security=true;ApplicationIntent=ReadWrite;MultipleActiveResultSets=true");
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs);
        Assert.Equal(Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly, b.ApplicationIntent);
        Assert.False(b.MultipleActiveResultSets);
        Assert.Equal("INVC Web (read-only)", b.ApplicationName);
    }

    [Fact]
    public void Sa_still_rejected_and_database_still_required()
    {
        Assert.Throws<InvalidOperationException>(() => ReadOnlySqlConnectionFactory.BuildReadOnlyConnectionString("Server=.;Database=INV;User ID=SA;Password=x"));
        Assert.Throws<InvalidOperationException>(() => ReadOnlySqlConnectionFactory.BuildReadOnlyConnectionString("Server=.;Integrated Security=true"));
    }
}

public class HealthPresentationTests
{
    private static readonly DatabaseHealthResult Unhealthy = new(false, "DESKTOP-SECRET", "INV", "DOMAIN\\svc", "16.0", TimeSpan.FromMilliseconds(12),
        "Cannot open database \"INV\" requested by the login. Server=DESKTOP-SECRET;Password=hunter2");
    private static readonly DatabaseHealthResult Healthy = new(true, "DESKTOP-SECRET", "INV", "DOMAIN\\svc", "16.0", TimeSpan.FromMilliseconds(5), null);

    [Fact]
    public void Production_redacts_all_technical_details()
    {
        var v = HealthPresentation.From(Unhealthy, isDevelopment: false, environmentName: "Production");
        Assert.False(v.IsHealthy);
        Assert.False(v.ShowTechnicalDetails);
        Assert.Null(v.ServerName); Assert.Null(v.DatabaseName); Assert.Null(v.LoginName); Assert.Null(v.ProductVersion);
        Assert.Equal(HealthPresentation.GenericFailureMessage, v.Error);
        Assert.DoesNotContain("DESKTOP-SECRET", v.Error);
        Assert.DoesNotContain("hunter2", v.Error);
        Assert.Equal("Production", v.EnvironmentName);
        Assert.Equal(12, v.Elapsed.TotalMilliseconds);
    }

    [Fact]
    public void Production_healthy_has_no_error_and_no_details()
    {
        var v = HealthPresentation.From(Healthy, false, "Production");
        Assert.True(v.IsHealthy);
        Assert.Null(v.Error);
        Assert.Null(v.ServerName);
    }

    [Fact]
    public void Development_keeps_diagnostics()
    {
        var v = HealthPresentation.From(Unhealthy, isDevelopment: true, environmentName: "Development");
        Assert.True(v.ShowTechnicalDetails);
        Assert.Equal("DESKTOP-SECRET", v.ServerName);
        Assert.Equal("DOMAIN\\svc", v.LoginName);
        Assert.Contains("Cannot open database", v.Error);
    }
}

public class SecurityHeadersTests
{
    [Fact]
    public void Apply_sets_the_baseline_headers()
    {
        var headers = new HeaderDictionary();
        SecurityHeaders.Apply(headers);
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("same-origin", headers["Referrer-Policy"]);
        Assert.Equal("SAMEORIGIN", headers["X-Frame-Options"]);
        Assert.Equal("camera=(), microphone=(), geolocation=()", headers["Permissions-Policy"]);
        Assert.False(headers.ContainsKey("Content-Security-Policy"));   // documented as future hardening
    }

    [Fact]
    public void Apply_does_not_duplicate_or_override_host_supplied_headers()
    {
        var headers = new HeaderDictionary { ["X-Frame-Options"] = "DENY" };
        SecurityHeaders.Apply(headers);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Single(headers["X-Frame-Options"].ToArray());
    }
}

public class PublishSpecificationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) { dir = dir.Parent; }
        return dir?.FullName ?? throw new InvalidOperationException("repository root (Invc.slnx) not found");
    }

    [Fact]
    public void Web_project_excludes_development_settings_from_publish()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Invc.Web", "Invc.Web.csproj"));
        Assert.Matches(new Regex(@"<Content\s+Update=""appsettings\.Development\.json""\s+CopyToPublishDirectory=""Never""", RegexOptions.IgnoreCase), csproj);
        Assert.Contains("<SelfContained>false</SelfContained>", csproj);
    }

    [Fact]
    public void Release_builds_do_not_produce_symbols()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Directory.Build.props"));
        Assert.Contains("'$(Configuration)' == 'Release'", props);
        Assert.Contains("<DebugType>none</DebugType>", props);
    }

    [Fact]
    public void Production_settings_carry_no_secret_and_base_settings_carry_no_connection_string()
    {
        var root = RepoRoot();
        foreach (var file in new[] { "appsettings.json", "appsettings.Production.json" })
        {
            var text = File.ReadAllText(Path.Combine(root, "src", "Invc.Web", file));
            Assert.DoesNotMatch(new Regex(@"(?i)password\s*=|pwd\s*=|User ID\s*=\s*sa"), text);
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("InvDatabase", out var inv) && inv.TryGetProperty("ConnectionString", out var cs))
            {
                Assert.True(string.IsNullOrWhiteSpace(cs.GetString()), $"{file} must not ship a populated connection string");
            }
        }
    }

    [Fact]
    public void Publish_script_refuses_paths_outside_the_project()
    {
        var root = RepoRoot();
        // %TEMP% is inside the project when run through scripts/dotnet-local.ps1, so only include it when it is genuinely outside.
        var outside = new List<string> { @"C:\inetpub\wwwroot\invc", @"C:\Windows\Temp\invc", Path.Combine(root, "..", "elsewhere") };
        var temp = Path.Combine(Path.GetTempPath(), "invc-publish");
        if (!Path.GetFullPath(temp).StartsWith(root, StringComparison.OrdinalIgnoreCase)) { outside.Add(temp); }
        foreach (var bad in outside)
        {
            var (code, output) = RunPowerShell(root, $"-File \"{Path.Combine(root, "scripts", "publish-iis.ps1")}\" -OutputPath \"{bad}\" -GuardOnly");
            Assert.True(code == 2, $"expected refusal (exit 2) for '{bad}' but got {code}: {output}");
            Assert.Contains("REFUSED", output);
            Assert.False(Directory.Exists(bad) && bad.Contains("invc", StringComparison.OrdinalIgnoreCase) && bad.StartsWith(@"C:\inetpub", StringComparison.OrdinalIgnoreCase), "must not create directories under inetpub");
        }
        var (okCode, okOutput) = RunPowerShell(root, $"-File \"{Path.Combine(root, "scripts", "publish-iis.ps1")}\" -OutputPath \"{Path.Combine(root, ".work", "release", "publish")}\" -GuardOnly");
        Assert.True(okCode == 0, okOutput);
        Assert.Contains("accepted", okOutput);
    }

    private static (int ExitCode, string Output) RunPowerShell(string workingDirectory, string arguments)
    {
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass " + arguments)
        {
            WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(60_000);
        return (p.ExitCode, output);
    }
}

public class ReadOnlySqlReleaseAssertionTests
{
    [Fact]
    public void Every_known_application_sql_statement_is_accepted_by_the_guard()
    {
        var statements = new List<(string Source, string Sql)>();
        void Add(string source, IEnumerable<string> sqls) { foreach (var s in sqls) statements.Add((source, s)); }

        var infra = typeof(ReadOnlySql).Assembly;
        foreach (var typeName in new[] { "Invc.Infrastructure.Inventory.InventorySql", "Invc.Infrastructure.Reorder.ReorderSql" })
        {
            var t = infra.GetType(typeName)!;
            Add(typeName, t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string) && f.Name != "EligibilityFilter").Select(f => (string)f.GetValue(null)!));
        }
        foreach (var typeName in new[] { "Invc.Infrastructure.PurchaseOrders.PurchaseOrderSql", "Invc.Infrastructure.Receipts.NonPoReceiptSql", "Invc.Infrastructure.Dashboard.DashboardSql" })
        {
            var t = infra.GetType(typeName)!;
            var all = (IEnumerable<string>)t.GetMethod("AllStatements")!.Invoke(null, null)!;
            Add(typeName, all);
        }
        statements.Add(("DatabaseHealth", "SELECT 1"));

        Assert.True(statements.Count >= 30, $"expected the full application SQL inventory, found {statements.Count}");
        foreach (var (source, sql) in statements)
        {
            Assert.Equal(sql, ReadOnlySql.Ensure(sql));
            Assert.DoesNotMatch(new Regex(@"(?i)\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|CREATE|ALTER|DROP|GRANT|DENY|REVOKE|EXEC)\b"), sql);
        }
    }

    [Fact]
    public void Application_source_contains_no_write_sql_or_credential_literals()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Invc.slnx"))) { root = root.Parent; }
        Assert.NotNull(root);
        var src = Path.Combine(root!.FullName, "src");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            // The ONLY files allowed to contain write SQL are the two MySQL repositories of the application-owned invc_web database
            // (Borrow module): the mirror (borrow_source_*, pinned by BorrowInfrastructureTests) and the append-only return trail
            // (INSERT into borrow_return_event only — never UPDATE/DELETE). Everything else stays SELECT-only.
            var isMySqlMirror = file.EndsWith(Path.Combine("Infrastructure", "Borrow", "BorrowMirrorRepository.cs"), StringComparison.OrdinalIgnoreCase);
            var isReturnTrail = file.EndsWith(Path.Combine("Infrastructure", "Borrow", "BorrowReturnRepository.cs"), StringComparison.OrdinalIgnoreCase);
            if (!isMySqlMirror && !isReturnTrail && Regex.IsMatch(text, @"(?im)^\s*(INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|MERGE\s+INTO|TRUNCATE\s+TABLE|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b")) offenders.Add($"write SQL: {file}");
            if ((isMySqlMirror || isReturnTrail) && Regex.IsMatch(text, @"(?i)\b(dbo\.|MERGE|TRUNCATE|CREATE\s+TABLE|ALTER|DROP)\b")) offenders.Add($"MySQL repository must not reference INV objects or DDL: {file}");
            if (isReturnTrail && Regex.IsMatch(text, @"(?im)^\s*(UPDATE\s+\w+\s+SET|DELETE\s+FROM)\b")) offenders.Add($"return trail must be append-only (no UPDATE/DELETE): {file}");
            if (isReturnTrail && Regex.IsMatch(text, @"(?im)^\s*INSERT\s+INTO\s+(?!borrow_return_event\b)\w+")) offenders.Add($"return trail may only insert into borrow_return_event: {file}");
            if (Regex.IsMatch(text, @"(?i)password\s*=\s*[A-Za-z0-9]{4,}") && !file.EndsWith("ProductionConfigurationValidator.cs")) offenders.Add($"credential literal: {file}");
        }
        Assert.Empty(offenders);
    }
}
