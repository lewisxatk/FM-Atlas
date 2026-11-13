using System.Diagnostics;
using System.IO;

namespace FmAtlas.Services;

public sealed record FmsaveBridgeResult(string OutputJsonPath, string StandardOutput, string StandardError);

/// <summary>Optional local-only adapter to the MIT-licensed fmsave Python package.</summary>
public static class FmsaveBridgeService
{
    public const string SupportedPackageVersion = "0.12.3";

    public static async Task<string?> GetInstalledVersionAsync(CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo("py")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-3");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import fmsave; print(getattr(fmsave, '__version__', 'unknown'))");

        try
        {
            using var process = Process.Start(start);
            if (process is null) return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            _ = await stderrTask;
            if (process.ExitCode != 0) return null;
            var version = stdout.Trim().Split(new[] { (char)13, (char)10 }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        }
        catch { return null; }
    }

    public static async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => string.Equals(await GetInstalledVersionAsync(cancellationToken), SupportedPackageVersion, StringComparison.Ordinal);

    public static async Task<FmsaveBridgeResult> ExportAsync(string savePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The selected FM career save does not exist.", savePath);
        var installedVersion = await GetInstalledVersionAsync(cancellationToken);
        if (!string.Equals(installedVersion, SupportedPackageVersion, StringComparison.Ordinal))
        {
            var found = string.IsNullOrWhiteSpace(installedVersion) ? "not installed or not importable" : installedVersion;
            throw new InvalidOperationException($"FM Atlas expects fmsave {SupportedPackageVersion}, but Python reports {found}. Run Setup-FMAtlas-Save-Bridge.ps1 and restart Atlas.");
        }

        var script = Path.Combine(AppContext.BaseDirectory, "tools", "Export-FMAtlas-Fmsave.py");
        if (!File.Exists(script))
        {
            // Development builds may be launched from the source tree rather than publish output.
            var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "Export-FMAtlas-Fmsave.py"));
            if (File.Exists(candidate)) script = candidate;
            else throw new FileNotFoundException("The local fmsave adapter script is missing from this FM Atlas build.", script);
        }

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "imports");
        Directory.CreateDirectory(folder);
        var output = Path.Combine(folder, $"fmsave-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        var start = new ProcessStartInfo("py")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-3");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(savePath);
        start.ArgumentList.Add(output);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Python Launcher (py.exe).");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { if (File.Exists(output)) File.Delete(output); } catch { }
            if (!cancellationToken.IsCancellationRequested)
                throw new TimeoutException("The save reader exceeded the 30-minute safety timeout.");
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"The fmsave adapter failed (exit code {process.ExitCode}).{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}".Trim());
        if (!File.Exists(output) || new FileInfo(output).Length < 32)
            throw new InvalidDataException("The fmsave adapter finished without creating a valid snapshot file.");

        return new FmsaveBridgeResult(output, stdout, stderr);
    }
}
