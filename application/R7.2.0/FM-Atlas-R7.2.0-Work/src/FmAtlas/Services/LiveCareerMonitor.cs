using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FmAtlas.Services;

/// <summary>
/// Read-only detector for the currently loaded FM27 career. It uses the exact storefront
/// profile and the game's own GAME_UI save-load counter; it does not write to game memory.
/// </summary>
public sealed record LiveCareerProbe(
    bool IsGameRunning,
    bool IsSupportedBuild,
    bool IsCareerLoaded,
    int ProcessId,
    string? ProfileId,
    string? ProfileName,
    ulong SaveLoadCounter,
    int PlayerSlots,
    int StaffSlots,
    int ClubSlots,
    string Status)
{
    public string SessionKey => IsCareerLoaded
        ? $"{ProcessId}:{ProfileId}:{SaveLoadCounter.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : string.Empty;

    public bool MatchesSession(string? sessionKey)
        => IsCareerLoaded && !string.IsNullOrWhiteSpace(sessionKey)
           && string.Equals(SessionKey, sessionKey, StringComparison.Ordinal);
}

public static class LiveCareerMonitor
{
    private const uint QueryInformation = 0x0400;
    private const uint VmRead = 0x0010;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint address, [Out] byte[] buffer, nuint size, out nuint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    /// <summary>
    /// FMSuperScout-style process discovery: an executable named fm.exe is not enough.
    /// Confirm the adjacent fm_Data directory before treating it as Football Manager.
    /// </summary>
    public static bool IsFootballManagerInstallPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        try
        {
            var fullPath = Path.GetFullPath(executablePath);
            if (!string.Equals(Path.GetFileName(fullPath), "fm.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            var directory = Path.GetDirectoryName(fullPath);
            return !string.IsNullOrWhiteSpace(directory)
                && Directory.Exists(Path.Combine(directory, "fm_Data"));
        }
        catch { return false; }
    }

    public static LiveCareerProbe Probe()
    {
        var processes = Process.GetProcessesByName("fm");
        if (processes.Length == 0)
            return new(false, false, false, 0, null, null, 0, 0, 0, 0,
                "Waiting for Football Manager 27. Leave Atlas open; it will connect when a career is loaded.");

        var supported = new List<(Process Process, ProcessModule Module, Fm27BuildProfile Profile)>();
        var sawRealGame = false;
        var sawGameModule = false;
        var accessDenied = false;
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    string? executablePath;
                    try { executablePath = process.MainModule?.FileName; }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
                    {
                        accessDenied = true;
                        continue;
                    }

                    // Do not attach to unrelated programs which happen to use the process
                    // name "fm". FMSuperScout uses this adjacent installation marker too.
                    if (!IsFootballManagerInstallPath(executablePath)) continue;
                    sawRealGame = true;

                    ProcessModule? module;
                    try
                    {
                        module = process.Modules.Cast<ProcessModule>().FirstOrDefault(x =>
                            string.Equals(Path.GetFileName(x.ModuleName), "game_plugin.dll", StringComparison.OrdinalIgnoreCase));
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
                    {
                        accessDenied = true;
                        continue;
                    }

                    if (module is null) continue;
                    sawGameModule = true;
                    using var readHandle = new SafeProcessReadHandle(OpenProcess(QueryInformation | VmRead, false, process.Id));
                    if (readHandle.Value == 0)
                    {
                        accessDenied |= Marshal.GetLastWin32Error() == 5;
                        continue;
                    }
                    var profile = MatchProfile(readHandle.Value, (ulong)module.BaseAddress.ToInt64());
                    if (profile is not null) supported.Add((process, module, profile));
                }
                catch (InvalidOperationException) { /* Process exited during discovery. */ }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 5) accessDenied = true;
                }
                catch { /* A partially started or inaccessible game process is not attachable. */ }
            }

            if (supported.Count == 0)
                return new(true, false, false, 0, null, null, 0, 0, 0, 0,
                    accessDenied
                        ? "Football Manager is running, but Windows denied process access. Run FM Atlas and Football Manager at the same permission level."
                        : sawGameModule
                            ? "Football Manager is running, but this game build is not in Atlas's verified profile list."
                            : sawRealGame
                                ? "Football Manager is starting. Waiting for game_plugin.dll and a supported build profile."
                                : "A process named fm.exe was found, but it is not a recognised Football Manager installation.");

            // Match FMSuperScout's deterministic multi-process rule: use the most recently
            // started real FM process. If two valid installations are open, the active newer
            // session is the likely user target; the chosen PID is still session-keyed.
            var current = supported.OrderByDescending(x =>
            {
                try { return x.Process.StartTime; }
                catch { return DateTime.MinValue; }
            }).First();

            if (supported.Count > 1)
                AtlasLog.Warning($"Multiple supported FM27 processes found ({supported.Count}); selecting newest process PID {current.Process.Id}, matching FMSuperScout's process-selection strategy.");

            var pid = current.Process.Id;
            using var handle = new SafeProcessReadHandle(OpenProcess(QueryInformation | VmRead, false, pid));
            if (handle.Value == 0)
                return new(true, true, false, pid, current.Profile.Id, current.Profile.DisplayName, 0, 0, 0, 0,
                    Marshal.GetLastWin32Error() == 5
                        ? "FM27 was found, but Windows denied read-only access. Run Atlas and Football Manager at the same permission level."
                        : "FM27 was found, but its process handle could not be opened. Atlas will retry automatically.");

            var baseAddress = (ulong)current.Module.BaseAddress.ToInt64();
            if (current.Profile.GameUiRva == 0)
                return new(true, true, false, pid, current.Profile.Id, current.Profile.DisplayName, 0, 0, 0, 0,
                    "This launch-preview build has no verified save-load counter. Automatic career detection is unavailable for it.");

            ulong counter;
            try { counter = ReadSaveLoadCounter(handle.Value, baseAddress, current.Profile); }
            catch
            {
                return new(true, true, false, pid, current.Profile.Id, current.Profile.DisplayName, 0, 0, 0, 0,
                    "FM27 is running. Waiting for its career identity object to become readable.");
            }

            var players = ReadSlotCount(handle.Value, baseAddress, current.Profile.SizeOfImage, current.Profile.PlayersArrayRva);
            var staff = ReadSlotCount(handle.Value, baseAddress, current.Profile.SizeOfImage, current.Profile.StaffArrayRva);
            var clubs = ReadSlotCount(handle.Value, baseAddress, current.Profile.SizeOfImage, current.Profile.ClubsArrayRva);
            var loaded = counter > 0 && players >= 20_000 && staff >= 1_000 && clubs >= 500;
            var status = loaded
                ? $"FM27 career detected · {current.Profile.DisplayName} · {players:N0} player slots. Connecting automatically…"
                : counter == 0
                    ? "FM27 is open. Load your career and Atlas will connect automatically when the save generation is ready."
                    : "FM27 has registered a career load. Waiting for its player, staff and club slot counts to stabilise.";
            return new(true, true, loaded, pid, current.Profile.Id, current.Profile.DisplayName, counter, players, staff, clubs, status);
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static Fm27BuildProfile? MatchProfile(nint handle, ulong moduleBase)
    {
        var pe = Read(handle, (nint)moduleBase, 0x40);
        if (pe[0] != 'M' || pe[1] != 'Z') return null;
        var peOffset = BitConverter.ToInt32(pe, 0x3C);
        if (peOffset < 0x40 || peOffset > 0x1000) return null;
        var nt = Read(handle, (nint)(moduleBase + (ulong)peOffset), 24);
        if (nt[0] != 'P' || nt[1] != 'E' || BitConverter.ToUInt16(nt, 4) != 0x8664) return null;
        var timestamp = BitConverter.ToUInt32(nt, 8);
        var optionalSize = BitConverter.ToUInt16(nt, 20);
        var optional = Read(handle, (nint)(moduleBase + (ulong)peOffset + 24), optionalSize);
        return Fm27BuildProfiles.Match(BitConverter.ToUInt32(optional, 56), timestamp);
    }

    private static ulong ReadSaveLoadCounter(nint handle, ulong moduleBase, Fm27BuildProfile profile)
    {
        if (profile.GameUiRva == 0 || profile.GameUiRva >= profile.SizeOfImage)
            throw new InvalidOperationException("This profile does not locate the FM game-session counter.");
        var gameUi = U64(handle, (nint)(moduleBase + profile.GameUiRva));
        if (!Pointer(gameUi)) throw new InvalidOperationException("The FM game-session object is not readable.");
        return U64(handle, (nint)(gameUi + Fm27BuildProfile.SaveLoadCounterOffset));
    }

    private static int ReadSlotCount(nint handle, ulong moduleBase, uint imageSize, ulong rva)
    {
        if (rva == 0 || rva + 16 > imageSize) return 0;
        try
        {
            var begin = U64(handle, (nint)(moduleBase + rva));
            var end = U64(handle, (nint)(moduleBase + rva + 8));
            if (!Pointer(begin) || !Pointer(end) || end <= begin || (end - begin) % 8 != 0) return 0;
            var count = (end - begin) / 8;
            return count <= 200_000 ? (int)count : 0;
        }
        catch { return 0; }
    }

    private static bool Pointer(ulong value) => value >= 0x10000 && value <= 0x7FFF_FFFF_FFFF;
    private static ulong U64(nint handle, nint address) => BitConverter.ToUInt64(Read(handle, address, 8), 0);

    private static byte[] Read(nint handle, nint address, int length)
    {
        var buffer = new byte[length];
        if (!ReadProcessMemory(handle, address, buffer, (nuint)length, out var read) || read != (nuint)length)
            throw new InvalidOperationException("A read-only FM process read was incomplete.");
        return buffer;
    }

    private sealed class SafeProcessReadHandle : IDisposable
    {
        public nint Value { get; }
        public SafeProcessReadHandle(nint value) => Value = value;
        public void Dispose() { if (Value != 0) CloseHandle(Value); }
    }
}
