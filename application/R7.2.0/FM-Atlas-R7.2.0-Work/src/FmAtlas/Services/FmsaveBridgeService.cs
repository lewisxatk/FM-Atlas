using System.Diagnostics;
using System.IO;
using System.Text;

namespace FmAtlas.Services;

public sealed record FmsaveBridgeResult(string OutputPath, string StandardOutput, string StandardError);

/// <summary>Optional local-only adapter to the MIT-licensed fmsave Python package.</summary>
public static class FmsaveBridgeService
{
    public static async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo("py") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("-3"); start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import fmsave,sys; v=tuple(int(x) for x in fmsave.__version__.split('.')[:3]); print(fmsave.__version__); sys.exit(0 if v >= (0,12,5) else 1)");
        try
        {
            using var process = Process.Start(start);
            if (process is null) return false;
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    public static async Task<FmsaveBridgeResult> ExportAsync(string savePath, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The selected FM career save does not exist.", savePath);
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
        var output = Path.Combine(folder, $"fmsave-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        var start = new ProcessStartInfo("py") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("-3"); start.ArgumentList.Add(script); start.ArgumentList.Add(savePath); start.ArgumentList.Add(output);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Python Launcher (py.exe).");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrBuffer = new StringBuilder();
        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
            {
                stderrBuffer.AppendLine(line);
                progress?.Report(line);
            }
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await Task.WhenAll(stdoutTask, stderrTask); } catch { }
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* Best-effort cleanup of a partial career export. */ }
            if (!cancellationToken.IsCancellationRequested)
                throw new TimeoutException("The save reader exceeded the 30-minute safety timeout.");
            throw;
        }
        var stdout = await stdoutTask;
        await stderrTask;
        var stderr = stderrBuffer.ToString();
        if (process.ExitCode != 0)
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* Best-effort cleanup of failed export output. */ }
            throw new InvalidOperationException($"The fmsave adapter failed (exit code {process.ExitCode}).\n{stderr}\n{stdout}".Trim());
        }
        var manifest = Path.Combine(output, "manifest.json");
        if (!Directory.Exists(output) || !File.Exists(manifest) || new FileInfo(manifest).Length < 32)
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); } catch { /* Best-effort cleanup of invalid export output. */ }
            throw new InvalidDataException("The fmsave adapter finished without creating a valid snapshot file.");
        }
        return new FmsaveBridgeResult(output, stdout, stderr);
    }
}
