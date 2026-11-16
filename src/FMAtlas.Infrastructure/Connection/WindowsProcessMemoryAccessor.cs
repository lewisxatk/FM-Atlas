using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Read-only process-memory adapter. The handle requests only VM_READ and
/// QUERY_INFORMATION; there is deliberately no write or terminate API here.
/// Every requested range must first pass a VirtualQueryEx committed/readable-page check.
/// </summary>
internal sealed class WindowsProcessMemoryAccessor : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;

    private const uint MemCommit = 0x1000;
    private const uint PageNoAccess = 0x01;
    private const uint PageReadOnly = 0x02;
    private const uint PageReadWrite = 0x04;
    private const uint PageWriteCopy = 0x08;
    private const uint PageExecuteRead = 0x20;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint PageExecuteWriteCopy = 0x80;
    private const uint PageGuard = 0x100;
    private const ulong PageSize = 0x1000;

    private readonly IntPtr _handle;
    private readonly Dictionary<ulong, ReadableRegion> _readablePageCache = [];
    private readonly object _regionCacheLock = new();
    private bool _disposed;
    private long _readOperations;
    private long _bytesRead;

    private readonly record struct ReadableRegion(ulong Start, ulong EndExclusive, uint Protection)
    {
        public bool Contains(ulong address) => address >= Start && address < EndExclusive;
    }

    // MEMORY_BASIC_INFORMATION on 64-bit Windows (48 bytes). PartitionId and
    // explicit padding preserve the native layout used by Windows 10/11.
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public ushort Reserved;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint Reserved2;
    }

    public WindowsProcessMemoryAccessor(int processId)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("Live Football Manager memory reading requires 64-bit Windows.");

        _handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, processId);
        if (_handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Could not open FM process {processId} for read-only access.");
    }

    public long ReadOperations => Interlocked.Read(ref _readOperations);
    public long BytesRead => Interlocked.Read(ref _bytesRead);

    public byte[] ReadBytes(ulong address, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (length < 0 || length > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(length), "Memory read length is outside the safe limit.");
        if (length == 0)
            return [];

        ValidateUserRange(address, length);
        EnsureReadableRange(address, length);

        var buffer = new byte[length];
        Interlocked.Increment(ref _readOperations);
        var ok = ReadProcessMemory(_handle, new IntPtr(unchecked((long)address)),
            buffer, (nuint)length, out var bytesRead);
        if (!ok || bytesRead != (nuint)length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error,
                $"Could not read {length:N0} bytes from the FM process (Windows error {error}).");
        }

        Interlocked.Add(ref _bytesRead, (long)bytesRead);
        return buffer;
    }

    public byte ReadByte(ulong address) => ReadBytes(address, 1)[0];
    public ushort ReadUInt16(ulong address) => BitConverter.ToUInt16(ReadBytes(address, 2));
    public short ReadInt16(ulong address) => BitConverter.ToInt16(ReadBytes(address, 2));
    public uint ReadUInt32(ulong address) => BitConverter.ToUInt32(ReadBytes(address, 4));
    public int ReadInt32(ulong address) => BitConverter.ToInt32(ReadBytes(address, 4));
    public ulong ReadUInt64(ulong address) => BitConverter.ToUInt64(ReadBytes(address, 8));

    public static bool IsUserPointer(ulong address) =>
        address >= 0x10000 && address <= 0x00007FFFFFFFFFFF;

    public static void ValidateUserPointer(ulong address, string description)
    {
        if (!IsUserPointer(address))
            throw new InvalidDataException(
                $"The {description} 0x{address:X} is not a plausible 64-bit user-mode pointer.");
    }

    private static void ValidateUserRange(ulong address, int length)
    {
        ValidateUserPointer(address, "read address");
        var lastAddress = checked(address + (ulong)length - 1UL);
        ValidateUserPointer(lastAddress, "read end address");
    }

    private void EnsureReadableRange(ulong address, int length)
    {
        var endExclusive = checked(address + (ulong)length);
        var cursor = address;

        while (cursor < endExclusive)
        {
            var region = GetReadableRegion(cursor);
            if (!region.Contains(cursor))
                throw new InvalidDataException("The FM memory query returned a region that does not contain the requested address.");

            var next = Math.Min(endExclusive, region.EndExclusive);
            if (next <= cursor)
                throw new InvalidDataException("The FM memory query returned an invalid or overflowing region boundary.");

            cursor = next;
        }
    }

    private ReadableRegion GetReadableRegion(ulong address)
    {
        var page = address / PageSize;
        lock (_regionCacheLock)
        {
            if (_readablePageCache.TryGetValue(page, out var cached) && cached.Contains(address))
                return cached;

            var queried = VirtualQueryEx(
                _handle,
                new IntPtr(unchecked((long)address)),
                out var information,
                (nuint)Marshal.SizeOf<MemoryBasicInformation>());

            if (queried == 0)
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error,
                    $"FM memory region inspection failed (Windows error {error}); the requested memory was not read.");
            }

            var start = unchecked((ulong)information.BaseAddress.ToInt64());
            var regionSize = (ulong)information.RegionSize;
            if (regionSize == 0)
                throw new InvalidDataException("FM returned a zero-length memory region.");

            var endExclusive = checked(start + regionSize);
            var region = new ReadableRegion(start, endExclusive, information.Protect);
            if (!region.Contains(address))
                throw new InvalidDataException("FM returned an inconsistent memory-region boundary.");

            var baseProtection = information.Protect & 0xFF;
            if (information.State != MemCommit ||
                (information.Protect & PageGuard) != 0 ||
                !IsReadableProtection(baseProtection))
            {
                throw new InvalidDataException(
                    $"FM memory at 0x{address:X} is not committed readable memory (state 0x{information.State:X}, protection 0x{information.Protect:X}). The scan stopped before reading it.");
            }

            _readablePageCache[page] = region;
            return region;
        }
    }

    private static bool IsReadableProtection(uint protection) =>
        protection is PageReadOnly or PageReadWrite or PageWriteCopy
            or PageExecuteRead or PageExecuteReadWrite or PageExecuteWriteCopy;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseHandle(_handle);
        lock (_regionCacheLock)
            _readablePageCache.Clear();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualQueryEx(
        IntPtr process,
        IntPtr address,
        out MemoryBasicInformation information,
        nuint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
