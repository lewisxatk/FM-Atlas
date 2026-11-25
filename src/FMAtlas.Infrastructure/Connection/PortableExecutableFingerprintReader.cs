using System.Diagnostics;
using System.Text;
using FMAtlas.Domain.Connection;

namespace FMAtlas.Infrastructure.Connection;

/// <summary>
/// Reads only PE metadata from the module file on disk. It does not open the game
/// process or read/write process memory.
/// </summary>
public static class PortableExecutableFingerprintReader
{
    private const ushort DosSignature = 0x5A4D;
    private const uint PeSignature = 0x00004550;
    private const ushort Pe32Magic = 0x010B;
    private const ushort Pe32PlusMagic = 0x020B;
    private const int PeHeaderOffsetLocation = 0x3C;
    private const int SizeOfImageOffset = 56;

    public static GameModuleFingerprint? TryRead(string modulePath)
    {
        if (string.IsNullOrWhiteSpace(modulePath) || !File.Exists(modulePath))
            return null;

        try
        {
            using var stream = new FileStream(
                modulePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            if (stream.Length < 0x40 || reader.ReadUInt16() != DosSignature)
                return null;

            stream.Position = PeHeaderOffsetLocation;
            var peOffset = reader.ReadInt32();
            if (peOffset < 0 || peOffset > stream.Length - 24)
                return null;

            stream.Position = peOffset;
            if (reader.ReadUInt32() != PeSignature)
                return null;

            var machine = reader.ReadUInt16();
            _ = reader.ReadUInt16(); // NumberOfSections
            var timestamp = reader.ReadUInt32();
            _ = reader.ReadUInt32(); // PointerToSymbolTable
            _ = reader.ReadUInt32(); // NumberOfSymbols
            var optionalHeaderSize = reader.ReadUInt16();
            _ = reader.ReadUInt16(); // Characteristics

            var optionalHeaderStart = (long)peOffset + 24;
            if (optionalHeaderSize < SizeOfImageOffset + sizeof(uint) ||
                optionalHeaderStart + optionalHeaderSize > stream.Length)
                return null;

            stream.Position = optionalHeaderStart;
            var optionalMagic = reader.ReadUInt16();
            if (optionalMagic is not (Pe32Magic or Pe32PlusMagic))
                return null;

            stream.Position = optionalHeaderStart + SizeOfImageOffset;
            var sizeOfImage = reader.ReadUInt32();
            var fileVersion = TryGetFileVersion(modulePath);

            return new GameModuleFingerprint(
                machine,
                timestamp,
                sizeOfImage,
                stream.Length,
                fileVersion);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryGetFileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch
        {
            return null;
        }
    }
}
