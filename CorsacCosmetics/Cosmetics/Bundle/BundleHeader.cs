using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace CorsacCosmetics.Cosmetics.Bundle;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 14)]
public readonly record struct BundleHeader
{
    public const uint ExpectedMagic = 0x434f5253; // 'CORS' in ASCII
    public const ushort CurrentVersion = 1;

    public uint Magic { get; init; }
    public ushort Version { get; init; }
    public ushort Flags { get; init; }
    public uint ManifestLength { get; init; }
    public uint DataLength { get; init; }
    
    public bool IsValid => Magic == ExpectedMagic;
    public bool IsSupportedVersion => Version <= CurrentVersion;

    public static BundleHeader Read(Stream stream)
    {
        var buffer = new byte[14];
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            throw new EndOfStreamException("Could not read the full bundle header.");
        
        return new BundleHeader
        {
            Magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0, 4)),
            Version = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(4, 2)),
            Flags = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(6, 2)),
            ManifestLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(8, 4)),
            DataLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12, 4))
        };
    }
}