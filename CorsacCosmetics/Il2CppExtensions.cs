using System;
using System.Buffers;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CorsacCosmetics;

public static class Il2CppExtensions
{
    extension(Il2CppStructArray<byte> destination)
    {
        public unsafe void CopyFrom(byte[] source, int length)
        {
            fixed (byte* sourcePtr = source)
            {
                var destPtr = (byte*)IntPtr.Add(destination.Pointer, 4 * IntPtr.Size).ToPointer();
                Buffer.MemoryCopy(sourcePtr, destPtr, length, length);
            }
        }

        public void CopyFromStream(Stream stream, int length)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                if (stream.Read(buffer, 0, length) != length)
                    throw new EndOfStreamException("Could not read the expected number of bytes from the stream.");
                destination.CopyFrom(buffer, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}