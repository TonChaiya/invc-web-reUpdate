using System.Diagnostics;

namespace Invc.UnitTests;

/// <summary>
/// Regression tests for the shared release path guard (scripts/project-path-guard.ps1) and the scripts that write or
/// delete inside the project. Every candidate path is evaluated with -GuardOnly, so nothing is written anywhere, and the
/// reparse-point scenario is built and removed entirely under .work.
/// </summary>
public class ReleasePathGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Invc.slnx"))) { dir = dir.Parent; }
        return dir?.FullName ?? throw new InvalidOperationException("repository root (Invc.slnx) not found");
    }

    private static (int ExitCode, string Output) RunPowerShell(string root, string arguments)
    {
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass " + arguments)
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(90_000);
        return (p.ExitCode, output);
    }

    /// <summary>Evaluates Test-PathInsideProject from the shared helper for one candidate.</summary>
    private static (int ExitCode, string Output) Guard(string root, string candidate, bool requireFile = false)
    {
        var helper = Path.Combine(root, "scripts", "project-path-guard.ps1");
        var cmd = $"-Command \". '{helper}'; if (Test-PathInsideProject -Candidate '{candidate}' -ProjectRoot '{root}'{(requireFile ? " -RequireDescendant" : "")}) {{ 'ACCEPT'; exit 0 }} else {{ 'REJECT'; exit 2 }}\"";
        return RunPowerShell(root, cmd);
    }

    [Fact]
    public void A_prefix_collision_sibling_is_rejected()
    {
        var root = RepoRoot();
        foreach (var sibling in new[] { root + "-outside\\release-manifest.json", root + "2\\publish", root + "x" })
        {
            var (code, output) = Guard(root, sibling);
            Assert.True(code == 2, $"expected REJECT for sibling '{sibling}': {output}");
        }
    }

    [Fact]
    public void B_parent_traversal_is_rejected()
    {
        var root = RepoRoot();
        foreach (var bad in new[] { Path.Combine(root, "..", "outside", "x.json"), Path.Combine(root, ".work", "..", "..", "elsewhere") })
        {
            var (code, output) = Guard(root, bad);
            Assert.True(code == 2, $"expected REJECT for traversal '{bad}': {output}");
        }
    }

    [Fact]
    public void C_explicit_external_paths_are_rejected()
    {
        var root = RepoRoot();
        foreach (var bad in new[] { @"C:\inetpub\wwwroot\invc", @"C:\Windows\Temp\invc", @"C:\inetpub", @"D:\anything" })
        {
            var (code, output) = Guard(root, bad);
            Assert.True(code == 2, $"expected REJECT for '{bad}': {output}");
        }
    }

    [Fact]
    public void D_valid_descendants_are_accepted_and_root_itself_is_not_a_descendant_file()
    {
        var root = RepoRoot();
        Assert.Equal(0, Guard(root, Path.Combine(root, ".work", "release", "publish")).ExitCode);
        Assert.Equal(0, Guard(root, Path.Combine(root, ".work", "release", "manifest", "release-manifest.json")).ExitCode);
        Assert.Equal(0, Guard(root, Path.Combine(root, ".work", "release", "publish"), requireFile: true).ExitCode);
        Assert.Equal(2, Guard(root, root, requireFile: true).ExitCode);              // the project root is not a descendant
        Assert.Equal(2, Guard(root, root + "\\", requireFile: true).ExitCode);
    }

    [Fact]
    public void E_reparse_point_below_project_is_rejected_for_write_operations()
    {
        var root = RepoRoot();
        var sandbox = Path.Combine(root, ".work", "guard-tests");
        var target = Path.Combine(sandbox, "real-target");        // stays inside the project — no external target is created
        var junction = Path.Combine(sandbox, "junction");
        try
        {
            Directory.CreateDirectory(target);
            if (Directory.Exists(junction)) { Directory.Delete(junction); }
            var mk = RunPowerShell(root, $"-Command \"New-Item -ItemType Junction -Path '{junction}' -Target '{target}' | Out-Null; exit 0\"");
            Assert.True(mk.ExitCode == 0, "could not create the test junction under .work: " + mk.Output);
            Assert.True(new DirectoryInfo(junction).Attributes.HasFlag(FileAttributes.ReparsePoint));

            var candidate = Path.Combine(junction, "publish");
            var (code, output) = Guard(root, candidate, requireFile: true);
            Assert.True(code == 2, $"expected REJECT for a candidate below a junction: {output}");

            // The publish and manifest scripts must refuse the same candidate before writing anything, naming the reason.
            var pub = RunPowerShell(root, $"-File \"{Path.Combine(root, "scripts", "publish-iis.ps1")}\" -OutputPath \"{candidate}\" -GuardOnly");
            Assert.True(pub.ExitCode == 2, pub.Output);
            Assert.Contains("reparse", pub.Output, StringComparison.OrdinalIgnoreCase);
            var man = RunPowerShell(root, $"-File \"{Path.Combine(root, "scripts", "new-release-manifest.ps1")}\" -ManifestPath \"{Path.Combine(candidate, "m.json")}\" -GuardOnly");
            Assert.True(man.ExitCode == 2, man.Output);
            Assert.False(File.Exists(Path.Combine(candidate, "m.json")));
        }
        finally
        {
            if (Directory.Exists(junction)) { Directory.Delete(junction); }     // removes the link only, never its target's contents
            if (Directory.Exists(sandbox)) { Directory.Delete(sandbox, true); }
        }
    }

    [Fact]
    public void Manifest_script_refuses_outside_paths_without_creating_files()
    {
        var root = RepoRoot();
        var script = Path.Combine(root, "scripts", "new-release-manifest.ps1");
        var outside = root + "-outside\\release-manifest.json";
        // Never assume the sibling is absent and never touch it: compare its state before and after the refused call.
        DateTime? before = File.Exists(outside) ? File.GetLastWriteTimeUtc(outside) : null;
        var (code, output) = RunPowerShell(root, $"-File \"{script}\" -ManifestPath \"{outside}\" -GuardOnly");
        Assert.True(code == 2, output);
        DateTime? after = File.Exists(outside) ? File.GetLastWriteTimeUtc(outside) : null;
        Assert.Equal(before, after);

        var (code2, output2) = RunPowerShell(root, $"-File \"{script}\" -PublishPath \"{root}2\\publish\" -GuardOnly");
        Assert.True(code2 == 2, output2);

        var (code3, output3) = RunPowerShell(root, $"-File \"{script}\" -ManifestPath \"{root}\" -GuardOnly");
        Assert.True(code3 == 2, "manifest path must be a descendant file, not the project root: " + output3);

        var (okCode, okOutput) = RunPowerShell(root, $"-File \"{script}\" -GuardOnly");
        Assert.True(okCode == 0, okOutput);
    }

    [Fact]
    public void Publish_and_audit_scripts_refuse_prefix_sibling_paths()
    {
        var root = RepoRoot();
        var (pubCode, pubOut) = RunPowerShell(root, $"-File \"{Path.Combine(root, "scripts", "publish-iis.ps1")}\" -OutputPath \"{root}-outside\\publish\" -GuardOnly");
        Assert.True(pubCode == 2, pubOut);
        var (audCode, audOut) = RunPowerShell(root, $"-File \"{Path.Combine(root, "scripts", "test-release-artifact.ps1")}\" -PublishPath \"{root}2\\publish\"");
        Assert.True(audCode == 2, audOut);
    }
}
