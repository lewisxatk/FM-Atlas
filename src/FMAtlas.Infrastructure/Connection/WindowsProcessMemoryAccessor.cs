using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Read-only process-memory adapter. The handle requests only VM_READ and
/// QUERY_INFORMATION; there is deliberately no write or terminate API here.
/// Each read validates its user-mode address and relies on ReadProcessMemory's
/// exact-byte-count result for page/region safety.
/// </summary>
internal sealed class WindowsProcessMemoryAccessor : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;

    private readonly IntPtr _handle;
    private bool _disposed;
    private long _readOperations;
    private long _bytesRead;

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
        if (length < 0 || length > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(length), "Memory read length is outside the safe limit.");

        var buffer = new byte[length];
        ReadBytes(address, buffer);
        return buffer;
    }

    /// <summary>
    /// Reads one complete range directly from the target process. This mirrors the
    /// working FMLibreEditor reader: ReadProcessMemory performs the actual access
    /// check and reports partial/unreadable regions. A VirtualQueryEx call before
    /// every distinct heap/string pointer was adding a large cost to a 40k-player
    /// scan without making the read itself any safer.
    /// </summary>
    public void ReadBytes(ulong address, byte[] buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);

        var length = buffer.Length;
        if (length > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(buffer), "Memory read length is outside the safe limit.");
        if (length == 0)
            return;

        ValidateUserRange(address, length);

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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseHandle(_handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

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
