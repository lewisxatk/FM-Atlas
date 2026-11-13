using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FmAtlas.Services;

public sealed record GameProcessStatus(
    bool IsRunning,
    int? ProcessId,
    string? ProcessName,
    bool ModuleFound,
    bool MemoryReadVerified,
    string Detail);

public static class GameProcessDetector
{
    // Read-only diagnostic: opens the selected FM process with query + VM_READ
    // permissions and reads only the first two bytes of game_plugin.dll (the PE MZ signature).
    // No player records are read and no process memory is ever written.
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint baseAddress,
        [Out] byte[] buffer, nuint size, out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    public static GameProcessStatus Detect()
    {
        var candidates = new List<Process>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var name = process.ProcessName;
                    if (string.Equals(name, "fm", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Football Manager 27", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("FootballManager27", StringComparison.OrdinalIgnoreCase))
                        candidates.Add(process);
                    else
                        process.Dispose();
                }
                catch { process.Dispose(); }
            }

            if (candidates.Count == 0)
                return new(false, null, null, false, false,
                    "Football Manager 27 not detected · start the game and load your career");

            // If multiple FM processes exist, prefer the one whose executable path identifies FM27.
            foreach (var process in candidates.OrderByDescending(IsLikelyFm27))
            {
                try
                {
                    var module = process.Modules.Cast<ProcessModule>()
                        .FirstOrDefault(m => string.Equals(Path.GetFileName(m.ModuleName),
                            "game_plugin.dll", StringComparison.OrdinalIgnoreCase));
                    if (module is null)
                        return new(true, process.Id, process.ProcessName, false, false,
                            $"FM process detected · PID {process.Id} · game module not visible yet; wait until FM27 finishes loading");

                    var handle = OpenProcess(ProcessQueryInformation | ProcessVmRead, false, process.Id);
                    if (handle == 0)
                        return new(true, process.Id, process.ProcessName, true, false,
                            $"FM27 process and game module detected · PID {process.Id} · Windows denied read-only access (error {Marshal.GetLastWin32Error()})");

                    try
                    {
                        var signature = new byte[2];
                        var ok = ReadProcessMemory(handle, module.BaseAddress, signature, 2, out var read);
                        var valid = ok && read == 2 && signature[0] == (byte)'M' && signature[1] == (byte)'Z';
                        var moduleAddress = module.BaseAddress.ToInt64().ToString("X");
                        return new(true, process.Id, process.ProcessName, true, valid,
                            valid
                                ? $"FM27 detected · PID {process.Id} · game_plugin.dll readable · read-only probe passed · module base 0x{moduleAddress}"
                                : $"FM27 detected · PID {process.Id} · game_plugin.dll found but header probe failed · no data read");
                    }
                    finally { CloseHandle(handle); }
                }
                catch (Exception ex)
                {
                    return new(true, process.Id, process.ProcessName, false, false,
                        $"FM process detected · PID {process.Id} · module inspection unavailable: {ex.GetType().Name}");
                }
            }
            return new(false, null, null, false, false, "No usable FM27 process found");
        }
        finally
        {
            foreach (var process in candidates) process.Dispose();
        }
    }

    private static bool IsLikelyFm27(Process process)
    {
        try { return process.MainModule?.FileName.Contains("Football Manager 27", StringComparison.OrdinalIgnoreCase) == true; }
        catch { return false; }
    }
}
