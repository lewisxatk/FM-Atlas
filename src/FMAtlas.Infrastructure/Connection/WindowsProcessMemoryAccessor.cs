using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Small read-only wrapper around ReadProcessMemory. It deliberately exposes no write operation.
/// </summary>
internal sealed class WindowsProcessMemoryAccessor : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private readonly IntPtr _handle;
    private bool _disposed;

    public WindowsProcessMemoryAccessor(int processId)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Live Football Manager memory reading requires Windows.");

        _handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, processId);
        if (_handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open FM process {processId} for read-only access.");
    }

    public byte[] ReadBytes(ulong address, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (length < 0 || length > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(length), "Memory read length is outside the safe limit.");
        ValidateUserPointer(address, "read address");
        if (length == 0)
            return [];

        var buffer = new byte[length];
        var ok = ReadProcessMemory(_handle, new IntPtr(unchecked((long)address)), buffer, (nuint)length, out var bytesRead);
        if (!ok || bytesRead != (nuint)length)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Could not read {length:N0} bytes at 0x{address:X} from the FM process.");
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
            throw new InvalidDataException($"The {description} 0x{address:X} is not a plausible 64-bit user-mode pointer.");
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
