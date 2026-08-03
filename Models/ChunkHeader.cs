using System.Runtime.InteropServices;

namespace TotkCave.Models;

/// <summary>
/// Represents the header of a MeshCodec compressed chunk file (ResChunkHeader).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct ChunkHeader(
    uint ChunkHash,
    uint VertexOutputSize,
    uint IndexOutputSize,
    uint DecompressedSize,
    uint WorkMemSize
)
{
    public uint TotalDataOutputSize => VertexOutputSize + IndexOutputSize;

    public static ChunkHeader Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 0x1C)
            throw new ArgumentException("Buffer too small for ResChunkHeader", nameof(buffer));

        return MemoryMarshal.Read<ChunkHeader>(buffer);
    }
}
