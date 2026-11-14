using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FmAtlas.Services;

/// <summary>
/// Read-only FM27 module fingerprint and PE-layout diagnostics.
/// This deliberately does not assume uncalibrated FM27 entity RVAs or write to the game.
/// </summary>
public static class GameModuleDiagnostics
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;

    // Candidate FM27 Steam RVAs from independent Proton research. These are
    // deliberately treated as candidates until this Windows process validates.
    private const ulong PlayerDataRva = 0x57037D0;
    private const ulong PlayerEndRva = 0x57037D8;
    private const ulong StaffDataRva = 0x5703818;
    private const ulong StaffEndRva = 0x5703820;
    private const ulong ClubDataRva = 0x5703988;
    private const ulong ClubEndRva = 0x5703990;
    private const ulong HumanManagerVtableRva = 0x4CC086C;
    private const string ManagerWitness = "Lewis Atkinson";
    private static readonly (string Name, string ExpectedClub)[] PlayerWitnesses =
    {
        ("Bradley Barcola", "Liverpool"),
        ("Cole Palmer", "Chelsea")
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process, nint address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    public static string CreateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("FM ATLAS — LIVE MODULE DIAGNOSTICS");
        sb.AppendLine("Read-only inspection. No game memory is modified.");
        sb.AppendLine();
        var candidates = Process.GetProcessesByName("fm");
        if (candidates.Length == 0)
        {
            sb.AppendLine("FM process: NOT FOUND");
            return sb.ToString();
        }

        foreach (var process in candidates)
        {
            using (process)
            {
                sb.AppendLine($"Process ID: {process.Id}");
                sb.AppendLine($"Process name: {process.ProcessName}");
                string? exe = null;
                try { exe = process.MainModule?.FileName; } catch { }
                sb.AppendLine($"Executable: {exe ?? "unavailable"}");
                sb.AppendLine($"Executable version: {ReadVersion(exe)}");
                ProcessModule? module = null;
                try { module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase)); }
                catch (Exception ex) { sb.AppendLine($"Module enumeration: {ex.GetType().Name}"); }
                if (module is null) { sb.AppendLine("game_plugin.dll: NOT VISIBLE"); continue; }

                var path = module.FileName;
                sb.AppendLine($"Module path: {path}");
                sb.AppendLine($"Module base: 0x{module.BaseAddress.ToInt64():X}");
                sb.AppendLine($"Module size (OS): 0x{module.ModuleMemorySize:X}");
                try
                {
                    using var stream = File.OpenRead(path);
                    sb.AppendLine($"Disk SHA-256: {Convert.ToHexString(SHA256.HashData(stream))}");
                }
                catch (Exception ex) { sb.AppendLine($"Disk hash: unavailable ({ex.GetType().Name})"); }

                var handle = OpenProcess(ProcessQueryInformation | ProcessVmRead, false, process.Id);
                if (handle == 0) { sb.AppendLine($"Read access: DENIED (Win32 {Marshal.GetLastWin32Error()})"); continue; }
                try
                {
                    var dos = Read(handle, module.BaseAddress, 0x40);
                    if (dos[0] != (byte)'M' || dos[1] != (byte)'Z') { sb.AppendLine("In-memory PE: invalid DOS signature"); continue; }
                    var peOffset = BitConverter.ToInt32(dos, 0x3C);
                    if (peOffset < 0x40 || peOffset > 0x1000) { sb.AppendLine("In-memory PE: invalid header offset"); continue; }
                    var nt = Read(handle, module.BaseAddress + peOffset, 24);
                    if (nt[0] != (byte)'P' || nt[1] != (byte)'E' || nt[2] != 0 || nt[3] != 0) { sb.AppendLine("In-memory PE: invalid signature"); continue; }
                    var machine = BitConverter.ToUInt16(nt, 4);
                    var sectionCount = BitConverter.ToUInt16(nt, 6);
                    var timestamp = BitConverter.ToUInt32(nt, 8);
                    var optionalSize = BitConverter.ToUInt16(nt, 20);
                    var optional = Read(handle, module.BaseAddress + peOffset + 24, optionalSize);
                    var magic = BitConverter.ToUInt16(optional, 0);
                    var imageSize = BitConverter.ToUInt32(optional, 56);
                    sb.AppendLine("In-memory PE: VALID");
                    sb.AppendLine($"Machine: 0x{machine:X4} ({(machine == 0x8664 ? "x64" : "other")})");
                    sb.AppendLine($"Optional header: 0x{magic:X4}");
                    sb.AppendLine($"PE timestamp: 0x{timestamp:X8}");
                    sb.AppendLine($"SizeOfImage: 0x{imageSize:X}");
                    sb.AppendLine($"Section count: {sectionCount}");
                    if (sectionCount > 0 && sectionCount <= 96)
                    {
                        var sectionTable = module.BaseAddress + peOffset + 24 + optionalSize;
                        var sections = Read(handle, sectionTable, sectionCount * 40);
                        sb.AppendLine("Sections (name | RVA | virtual size | characteristics):");
                        for (var i = 0; i < sectionCount; i++)
                        {
                            var o = i * 40;
                            var name = Encoding.ASCII.GetString(sections, o, 8).TrimEnd('\0');
                            var virtualSize = BitConverter.ToUInt32(sections, o + 8);
                            var rva = BitConverter.ToUInt32(sections, o + 12);
                            var characteristics = BitConverter.ToUInt32(sections, o + 36);
                            sb.AppendLine($"  {name} | 0x{rva:X} | 0x{virtualSize:X} | 0x{characteristics:X8}");
                        }
                    }
                    ValidateCandidateArrays(sb, handle, module.BaseAddress, imageSize);
                }
                catch (Exception ex) { sb.AppendLine($"In-memory PE read: FAILED ({ex.Message})"); }
                finally { CloseHandle(handle); }
                sb.AppendLine();
            }
        }
        sb.AppendLine("Safety gate: candidate arrays are diagnostic-only. The DLL hash is not calibrated; no game memory is written and no records are imported into the database.");
        return sb.ToString();
    }


    /// <summary>
    /// Produces a shareable diagnostic report. The deep probe is read-only; personal
    /// witness names and absolute Windows paths are removed unless the user opts in.
    /// </summary>
    public static string CreateSupportReport(bool includePersonalDetails = false)
        => RedactForSupport(CreateReport(), includePersonalDetails);

    public static string RedactForSupport(string report, bool includePersonalDetails)
    {
        if (includePersonalDetails) return report;
        var safe = report;
        safe = safe.Replace(ManagerWitness, "[redacted manager]", StringComparison.OrdinalIgnoreCase);
        foreach (var witness in PlayerWitnesses)
            safe = safe.Replace(witness.Name, "[redacted player]", StringComparison.OrdinalIgnoreCase);
        safe = Regex.Replace(safe, @"(?i)(common|first|last|display|joined)='[^']*'", "$1='[redacted]'");
        // Redact absolute paths, including install paths and user-profile paths.
        safe = Regex.Replace(safe, @"(?i)\\\\[^\r\n]+", "[path redacted]");
        safe = Regex.Replace(safe, @"(?i)\b[A-Z]:\\[^\r\n]+", "[path redacted]");
        return safe;
    }

    private static void ValidateCandidateArrays(StringBuilder sb, nint handle, nint moduleBase, uint imageSize)
    {
        sb.AppendLine();
        sb.AppendLine("FM27 WINDOWS CANDIDATE ARRAY VALIDATION (READ ONLY)");
        var player = ReadArrayBounds(sb, handle, moduleBase, imageSize, "Players", PlayerDataRva, PlayerEndRva, 20_000, 200_000);
        var staff = ReadArrayBounds(sb, handle, moduleBase, imageSize, "Staff", StaffDataRva, StaffEndRva, 5_000, 100_000);
        var clubs = ReadArrayBounds(sb, handle, moduleBase, imageSize, "Clubs", ClubDataRva, ClubEndRva, 1_000, 100_000);

        if (player is { } p)
        {
            sb.AppendLine("Player pointer sample:");
            var good = 0;
            var playerSampleIndices = SampleIndices(p.Count).Concat(new[] { p.Count / 4, (p.Count * 3) / 4, p.Count / 8, (p.Count * 7) / 8, Math.Min(1, p.Count - 1), Math.Max(0, p.Count - 2) }).Distinct().Take(9).ToArray();
            var decodedNames = 0;
            foreach (var index in playerSampleIndices)
            {
                try
                {
                    var address = ReadU64(handle, (nint)(p.Data + (ulong)index * 8));
                    var plausible = IsUserPointer(address);
                    if (plausible) good++;
                    var common = plausible ? ReadNameSub(handle, address + 0x2E8) : null;
                    var first = plausible ? ReadNameSub(handle, address + 0x2D8) : null;
                    var last = plausible ? ReadNameSub(handle, address + 0x2E0) : null;
                    var name = common ?? JoinNames(first, last);
                    if (!string.IsNullOrWhiteSpace(name)) decodedNames++;
                    sb.AppendLine($"  [{index}] 0x{address:X} {(plausible ? "pointer-shaped" : "INVALID")} common='{common ?? "<null>"}' first='{first ?? "<null>"}' last='{last ?? "<null>"}' display='{name ?? "<not decoded>"}'");
                }
                catch (Exception ex) { sb.AppendLine($"  [{index}] READ FAILED: {ex.Message}"); }
            }
            sb.AppendLine($"Player pointer sample result: {good}/{playerSampleIndices.Length} plausible; {decodedNames}/{playerSampleIndices.Length} names decoded");
            ScanPlayerWitnesses(sb, handle, p, clubs);
        }

        if (staff is { } st)
        {
            sb.AppendLine("Staff / manager witness scan:");
            var managerClassCount = 0;
            var witnessFound = false;
            var sampledNames = new List<string>();
            // The published layout identifies the human manager by a dedicated vtable.
            // Scan bounded staff pointers, but only inspect the special class.
            for (var i = 0; i < st.Count; i++)
            {
                try
                {
                    var address = ReadU64(handle, (nint)(st.Data + (ulong)i * 8));
                    if (!IsUserPointer(address)) continue;
                    var vtable = ReadU64(handle, (nint)address);
                    if (vtable != (ulong)moduleBase + HumanManagerVtableRva) continue;
                    managerClassCount++;
                    // Human-manager identity fields are shifted by +0x340.
                    // Do not prefer the first-name component alone: the previous
                    // diagnostic did that and consequently reported only "Lewis".
                    var common = ReadNameSub(handle, address + 0x4A0);
                    var first = ReadNameSub(handle, address + 0x490);
                    var last = ReadNameSub(handle, address + 0x498);
                    var name = !string.IsNullOrWhiteSpace(common) ? common : JoinNames(first, last);
                    var fmStringPointer = ReadU64(handle, (nint)(address + 0x480));
                    var dobBytes = "unreadable";
                    try { dobBytes = Convert.ToHexString(Read(handle, (nint)(address + 0x4C8), 8)); } catch { }
                    sb.AppendLine($"  special manager-class entry [{i}] common='{common ?? "<null>"}', first='{first ?? "<null>"}', last='{last ?? "<null>"}', joined='{name ?? "<unreadable>"}', name-fmstring-ptr=0x{fmStringPointer:X}, DOB bytes={dobBytes}");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        sampledNames.Add(name);
                        if (string.Equals(NormalizeName(name), NormalizeName(ManagerWitness), StringComparison.OrdinalIgnoreCase))
                            witnessFound = true;
                    }
                }
                catch { /* A single bad candidate must not abort the bounded scan. */ }
            }
            sb.AppendLine($"Human-manager class entries: {managerClassCount}");
            sb.AppendLine($"Manager witness '{ManagerWitness}': {(witnessFound ? "MATCH" : "NOT MATCHED")}");
            if (managerClassCount == 0) sb.AppendLine("Manager validation: NOT VERIFIED — no matching manager-class vtable found.");
            else if (!witnessFound) sb.AppendLine("Manager validation: FAILED CLOSED — special-class entries found, but witness name did not match.");
            else sb.AppendLine("Manager validation: WITNESS MATCH — manager identity layout has a Windows candidate match; independent player-name validation is still required.");
        }
    }


    private static void ScanPlayerWitnesses(StringBuilder sb, nint handle, (ulong Data, int Count) players, (ulong Data, int Count)? clubs)
    {
        sb.AppendLine();
        sb.AppendLine("PLAYER AND CLUB WITNESS SCAN (READ ONLY)");
        sb.AppendLine("Club-link probe: player +0x130 -> team; team +0x30 -> club. Read-only pointer-chain validation only.");
        sb.AppendLine("Expected club labels are user-supplied comparisons. Club names are decoded only when a bounded length-prefixed UTF-8 string is readable.");
        var matches = PlayerWitnesses.ToDictionary(x => NormalizeName(x.Name), _ => new List<(int Index, ulong Address, string Name, uint Uid, ulong Team, ulong Club, int ClubIndex, ulong ClubAddress)>());
        var decoded = 0;
        var unreadable = 0;
        for (var i = 0; i < players.Count; i++)
        {
            try
            {
                var address = ReadU64(handle, (nint)(players.Data + (ulong)i * 8));
                if (!IsUserPointer(address)) { unreadable++; continue; }
                var common = ReadNameSub(handle, address + 0x2E8);
                var first = ReadNameSub(handle, address + 0x2D8);
                var last = ReadNameSub(handle, address + 0x2E0);
                var name = !string.IsNullOrWhiteSpace(common) ? common : JoinNames(first, last);
                if (string.IsNullOrWhiteSpace(name)) { unreadable++; continue; }
                decoded++;
                var normalized = NormalizeName(name);
                if (matches.TryGetValue(normalized, out var found))
                {
                    uint uid = 0;
                    ulong team = 0, club = 0;
                    var clubIndex = -1;
                    ulong clubAddress = 0;
                    try
                    {
                        uid = BitConverter.ToUInt32(Read(handle, (nint)(address + 0x294), 4), 0);
                        team = ReadU64(handle, (nint)(address + 0x130));
                        if (IsUserPointer(team)) club = ReadU64(handle, (nint)(team + 0x30));
                        if (clubs is { } c && IsUserPointer(club))
                        {
                            // Compare against the bounded club pointer array; do not dereference names yet.
                            for (var ci = 0; ci < c.Count; ci++)
                            {
                                var candidateClub = ReadU64(handle, (nint)(c.Data + (ulong)ci * 8));
                                if (candidateClub == club) { clubIndex = ci; clubAddress = candidateClub; break; }
                            }
                        }
                    }
                    catch { /* Keep the name match even if a secondary field is unreadable. */ }
                    found.Add((i, address, name, uid, team, club, clubIndex, clubAddress));
                }
            }
            catch { unreadable++; }
        }

        sb.AppendLine($"Player entries scanned: {players.Count}");
        sb.AppendLine($"Entries with a decoded name: {decoded}");
        sb.AppendLine($"Entries without a decoded name / unreadable: {unreadable}");
        foreach (var witness in PlayerWitnesses)
        {
            var found = matches[NormalizeName(witness.Name)];
            sb.AppendLine($"Witness '{witness.Name}' (expected club: {witness.ExpectedClub}): {(found.Count == 0 ? "NOT FOUND" : $"FOUND {found.Count} MATCH(ES)")}");
            foreach (var match in found.Take(5))
            {
                var clubStatus = match.ClubIndex >= 0 ? $"club-array-index={match.ClubIndex}" : "club-array-match=NOT VERIFIED";
                sb.AppendLine($"  index={match.Index}, address=0x{match.Address:X}, decoded='{match.Name}', uid={match.Uid}, team=0x{match.Team:X}, club=0x{match.Club:X}, {clubStatus}");
                if (match.ClubAddress != 0) ProbeClubIdentity(sb, handle, match.ClubAddress, match.ClubIndex, witness.ExpectedClub);
            }
        }
    }

    private static void ProbeClubIdentity(StringBuilder sb, nint handle, ulong clubAddress, int clubIndex, string expectedClub)
    {
        sb.AppendLine($"    CLUB PROBE [{clubIndex}] expected='{expectedClub}' address=0x{clubAddress:X}");
        try
        {
            var vtable = ReadU64(handle, (nint)clubAddress);
            var uid = BitConverter.ToUInt32(Read(handle, (nint)(clubAddress + 0x0C), 4), 0);
            var fullNameField = ReadU64(handle, (nint)(clubAddress + 0xC0));
            var shortNameField = ReadU64(handle, (nint)(clubAddress + 0xC8));
            sb.AppendLine($"      vtable=0x{vtable:X}, club-uid={uid}");
            sb.AppendLine($"      full-name-field(+0xC0)=0x{fullNameField:X}; short-name-field(+0xC8)=0x{shortNameField:X}");
            AppendPointerProbe(sb, handle, "full-name", fullNameField);
            AppendPointerProbe(sb, handle, "short-name", shortNameField);
        }
        catch (Exception ex) { sb.AppendLine($"      club probe failed: {ex.Message}"); }
    }

    private static void AppendPointerProbe(StringBuilder sb, nint handle, string label, ulong pointer)
    {
        if (!IsUserPointer(pointer)) { sb.AppendLine($"      {label}: null/invalid pointer"); return; }
        try
        {
            // FM27 candidate FmString storage observed in this save: [u32 byte length][UTF-8 bytes].
            var lengthBytes = Read(handle, (nint)pointer, 4);
            var length = BitConverter.ToUInt32(lengthBytes, 0);
            if (length == 0 || length > 96)
            {
                sb.AppendLine($"      {label}: pointer readable, but length prefix {length} is outside the safe 1..96 range");
                return;
            }
            var textBytes = Read(handle, (nint)(pointer + 4), (int)length);
            var value = Encoding.UTF8.GetString(textBytes);
            var safe = new string(value.Where(c => !char.IsControl(c)).ToArray());
            sb.AppendLine($"      {label}: length={length}, decoded='{safe}'");
        }
        catch (Exception ex) { sb.AppendLine($"      {label}: read/decode failed ({ex.Message})"); }
    }

    private static (ulong Data, int Count)? ReadArrayBounds(StringBuilder sb, nint handle, nint moduleBase, uint imageSize,
        string label, ulong dataRva, ulong endRva, int minCount, int maxCount)
    {
        try
        {
            if (dataRva + 8 > imageSize || endRva + 8 > imageSize) throw new InvalidOperationException("RVA falls outside SizeOfImage");
            var data = ReadU64(handle, (nint)((ulong)moduleBase + dataRva));
            var end = ReadU64(handle, (nint)((ulong)moduleBase + endRva));
            if (!IsUserPointer(data) || !IsUserPointer(end) || end <= data) throw new InvalidOperationException($"invalid bounds data=0x{data:X}, end=0x{end:X}");
            var bytes = end - data;
            if (bytes % 8 != 0) throw new InvalidOperationException($"array byte length {bytes} is not divisible by 8");
            var count64 = bytes / 8;
            if (count64 < (ulong)minCount || count64 > (ulong)maxCount) throw new InvalidOperationException($"implausible pointer count {count64}");
            sb.AppendLine($"{label} candidate: BOUNDS READ; data=0x{data:X}, end=0x{end:X}, count={count64}");
            return (data, (int)count64);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{label} candidate: REJECTED — {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<int> SampleIndices(int count)
    {
        yield return 0;
        yield return count / 2;
        yield return count - 1;
    }

    private static ulong ReadU64(nint handle, nint address) => BitConverter.ToUInt64(Read(handle, address, 8), 0);
    private static bool IsUserPointer(ulong value) => value >= 0x10000 && value <= 0x7FFF_FFFF_FFFF;

    private static string? ReadNameSub(nint handle, ulong subRecordAddress)
    {
        try
        {
            var block = ReadU64(handle, (nint)subRecordAddress);
            if (!IsUserPointer(block)) return null;
            var nameData = ReadU64(handle, (nint)block);
            if (!IsUserPointer(nameData)) return null;
            var prefix = Read(handle, (nint)nameData, 4);
            var length = BitConverter.ToUInt32(prefix, 0);
            if (length == 0 || length > 64) return null;
            var bytes = Read(handle, (nint)(nameData + 4), (int)length);
            var value = Encoding.UTF8.GetString(bytes);
            return string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) ? null : value;
        }
        catch { return null; }
    }

    private static string? JoinNames(string? first, string? last)
    {
        if (string.IsNullOrWhiteSpace(first)) return last;
        if (string.IsNullOrWhiteSpace(last)) return first;
        return first + " " + last;
    }

    private static string NormalizeName(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static byte[] Read(nint handle, nint address, int length)
    {
        var bytes = new byte[length];
        if (!ReadProcessMemory(handle, address, bytes, (nuint)length, out var read) || read != (nuint)length)
            throw new InvalidOperationException($"ReadProcessMemory failed (Win32 {Marshal.GetLastWin32Error()}, {read}/{length} bytes)");
        return bytes;
    }

    private static string ReadVersion(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "unavailable";
        try { return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "not embedded"; }
        catch { return "unavailable"; }
    }
}
