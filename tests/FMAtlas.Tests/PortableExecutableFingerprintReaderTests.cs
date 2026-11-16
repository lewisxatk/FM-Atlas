using System.Buffers.Binary;
using FMAtlas.Infrastructure.Connection;
using Xunit;

namespace FMAtlas.Tests;

public sealed class PortableExecutableFingerprintReaderTests
{
    [Fact]
    public void ReadsMachineTimestampAndImageSizeFromPeMetadata()
    {
        var bytes = CreateMinimalPeImage();
        var path = Path.Combine(Path.GetTempPath(), $"fmatlas-pe-{Guid.NewGuid():N}.bin");

        try
        {
            File.WriteAllBytes(path, bytes);

            var fingerprint = PortableExecutableFingerprintReader.TryRead(path);

            Assert.NotNull(fingerprint);
            Assert.Equal((ushort)0x8664, fingerprint!.Machine);
            Assert.Equal(0x6AE8CF77u, fingerprint.PeTimestamp);
            Assert.Equal(0x17969000u, fingerprint.SizeOfImage);
            Assert.Equal(512, fingerprint.FileSize);
            Assert.Equal("x64", fingerprint.Architecture);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ReturnsNullForNonPeFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fmatlas-not-pe-{Guid.NewGuid():N}.bin");

        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
            Assert.Null(PortableExecutableFingerprintReader.TryRead(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ReturnsNullForMissingFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fmatlas-missing-{Guid.NewGuid():N}.dll");
        Assert.Null(PortableExecutableFingerprintReader.TryRead(path));
    }

    private static byte[] CreateMinimalPeImage()
    {
        var bytes = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), 0x5A4D); // MZ
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3C, 4), 0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x80, 4), 0x00004550); // PE\0\0

        var coff = 0x84;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(coff, 2), 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(coff + 2, 2), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(coff + 4, 4), 0x6AE8CF77);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(coff + 16, 2), 0xF0);

        var optional = 0x98;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(optional, 2), 0x020B); // PE32+
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(optional + 56, 4), 0x17969000);

        return bytes;
    }
}
