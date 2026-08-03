using System.Numerics;

namespace TotkCave.Models;

public sealed record CrBinNode(
    (ushort X, ushort Y, ushort Z) Cell,
    ushort Lod,
    (uint First, uint End) Children,
    (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) Aabb,
    uint ConnectionMask,
    uint BaseStream,
    ushort BaseMaterial,
    byte StreamCount
);

public sealed record CrBinStream(
    uint BaseIndex,
    uint Triangles,
    ushort Flags,
    ushort PageFile
);

public sealed record CrBinMaterial(
    Vector3 UBias,
    float ArrayLayer,
    Vector3 VBias,
    float UvScale
);

public sealed record CrBinPageFile(
    ushort PageFileIndex,
    ushort BlockCount,
    ushort Index
);
