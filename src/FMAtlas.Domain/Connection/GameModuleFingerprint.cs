namespace FMAtlas.Domain.Connection;

/// <summary>
/// Static PE metadata for a game module on disk. This is an identification clue,
/// not proof that FM Atlas understands the module's memory layout.
/// </summary>
public sealed record GameModuleFingerprint(
    ushort Machine,
    uint PeTimestamp,
    uint SizeOfImage,
    long FileSize,
    string? FileVersion)
{
    public string Architecture => Machine switch
    {
        0x8664 => "x64",
        0x014c => "x86",
        0xAA64 => "ARM64",
        _ => $"Unknown (0x{Machine:X4})"
    };

    public string Summary =>
        $"{Architecture} · PE timestamp 0x{PeTimestamp:X8} · image 0x{SizeOfImage:X8} · file {FileSize:N0} bytes" +
        (string.IsNullOrWhiteSpace(FileVersion) ? string.Empty : $" · version {FileVersion}");
}
