using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TotkCave.Models;

public sealed class QuadResource
{
    public const int QuadMeshOffset = 0xE4;

    public string Path { get; }
    public uint Version { get; }
    public uint Id { get; }
    public float[,] Transform { get; } = new float[3, 4];

    public int NumFarLodLevels { get; }
    public int NumNormalLodLevels { get; }
    public int NumRootNodes { get; }
    public (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) SingleBounds { get; }
    public (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) Bounds { get; }

    public Dictionary<string, (uint Offset, uint Count)> Arrays { get; } = [];
    public Dictionary<string, int> FarLayout { get; } = [];
    public Dictionary<string, int> NormalLayout { get; } = [];
    public int NodeCount => (int)Arrays["nodes"].Count;

    private readonly byte[] _data;
    
    private readonly int _nodesOff;
    private readonly int _nodeBoundsOff;
    private readonly int _layoutTypesOff;
    private readonly int _streamDepsOff;
    private readonly int _streamInfoOff;
    private readonly int _pageFilesOff;

    private static readonly string[] ArrayNames = [
        "nodes", "child_nodes", "stream_dependencies", "layout_types",
        "_30", "file_dependencies", "node_bounds", "stream_info", "page_files"
    ];

    private static readonly string[] LayoutFieldNames = [
        "file_size", "_04", "_08", "_0c", "_10", "quad_data_offset",
        "pos_adjust_offset0", "pos_adjust_offset1", "lod_far_corner_info_offset",
        "ci0", "ci1", "ci2", "_30", "_34"
    ];

    public QuadResource(string path)
    {
        Path = path;
        _data = File.ReadAllBytes(path);

        ReadOnlySpan<byte> d = _data;
        if (d.Length < 0x1DC || !d[..8].SequenceEqual(CrBin.MagicBytes))
            throw new InvalidDataException("Bad magic header for quad resource.");

        Version = MemoryMarshal.Read<uint>(d[0x0C..]);
        Id = MemoryMarshal.Read<uint>(d[0x10..]);

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
                Transform[r, c] = MemoryMarshal.Read<float>(d[(0x14 + (r * 4 + c) * 4)..]);

        int q = QuadMeshOffset;
        NumFarLodLevels = MemoryMarshal.Read<int>(d[q..]);
        NumNormalLodLevels = MemoryMarshal.Read<int>(d[(q + 4)..]);
        NumRootNodes = MemoryMarshal.Read<int>(d[(q + 8)..]);

        int arrBase = q + 0x10;
        for (int i = 0; i < ArrayNames.Length; i++)
        {
            uint off = MemoryMarshal.Read<uint>(d[(arrBase + i * 8)..]);
            uint cnt = MemoryMarshal.Read<uint>(d[(arrBase + i * 8 + 4)..]);
            Arrays[ArrayNames[i]] = (off, cnt);
        }

        int sb = arrBase + ArrayNames.Length * 8;
        SingleBounds = (
            MemoryMarshal.Read<float>(d[sb..]),
            MemoryMarshal.Read<float>(d[(sb + 4)..]),
            MemoryMarshal.Read<float>(d[(sb + 8)..]),
            MemoryMarshal.Read<float>(d[(sb + 12)..]),
            MemoryMarshal.Read<float>(d[(sb + 16)..]),
            MemoryMarshal.Read<float>(d[(sb + 20)..])
        );

        int b = sb + 24;
        Bounds = (
            MemoryMarshal.Read<float>(d[b..]),
            MemoryMarshal.Read<float>(d[(b + 4)..]),
            MemoryMarshal.Read<float>(d[(b + 8)..]),
            MemoryMarshal.Read<float>(d[(b + 12)..]),
            MemoryMarshal.Read<float>(d[(b + 16)..]),
            MemoryMarshal.Read<float>(d[(b + 20)..])
        );

        int fl = sb + 48;
        for (int i = 0; i < LayoutFieldNames.Length; i++)
            FarLayout[LayoutFieldNames[i]] = MemoryMarshal.Read<int>(d[(fl + i * 4)..]);

        int nl = fl + 0x38;
        for (int i = 0; i < LayoutFieldNames.Length; i++)
            NormalLayout[LayoutFieldNames[i]] = MemoryMarshal.Read<int>(d[(nl + i * 4)..]);

        _nodesOff = (int)Arrays["nodes"].Offset;
        _nodeBoundsOff = (int)Arrays["node_bounds"].Offset;
        _layoutTypesOff = (int)Arrays["layout_types"].Offset;
        _streamDepsOff = (int)Arrays["stream_dependencies"].Offset;
        _streamInfoOff = (int)Arrays["stream_info"].Offset;
        _pageFilesOff = (int)Arrays["page_files"].Offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> At(int offset, int length) => _data.AsSpan(offset, length);

    public (ushort X, ushort Y, ushort Z, ushort Lod) GetNode(int i)
    {
        ReadOnlySpan<ushort> s = MemoryMarshal.Cast<byte, ushort>(At(_nodesOff + i * 8, 8));
        return (s[0], s[1], s[2], s[3]);
    }

    /// <summary>LOD level of node <paramref name="i"/> without decoding its coordinates.</summary>
    public ushort GetNodeLod(int i) => MemoryMarshal.Read<ushort>(At(_nodesOff + i * 8 + 6, 2));

    public (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) GetNodeBounds(int i)
    {
        ReadOnlySpan<float> s = MemoryMarshal.Cast<byte, float>(At(_nodeBoundsOff + i * 24, 24));
        return (s[0], s[1], s[2], s[3], s[4], s[5]);
    }

    public byte GetLayoutType(int i) => _data[_layoutTypesOff + i];

    public (uint BaseStream, uint EndStream) GetStreamRange(int i)
    {
        ReadOnlySpan<uint> s = MemoryMarshal.Cast<byte, uint>(At(_streamDepsOff + i * 8, 8));
        return (s[0], s[1]);
    }

    public (int PageFileIndex, ushort Flags, ushort BaseVertexIndex, ushort NumQuads) GetStream(int j)
    {
        ReadOnlySpan<byte> s = At(_streamInfoOff + j * 6, 6);
        ushort pf = MemoryMarshal.Read<ushort>(s);
        ushort baseIndex = MemoryMarshal.Read<ushort>(s[2..]);
        ushort numQuads = MemoryMarshal.Read<ushort>(s[4..]);
        return (pf & 0x1FFF, (ushort)(pf >> 13), baseIndex, numQuads);
    }

    public (uint DecompressedSize, uint Id) GetPageFile(int i)
    {
        ReadOnlySpan<uint> s = MemoryMarshal.Cast<byte, uint>(At(_pageFilesOff + i * 8, 8));
        return (s[0], s[1]);
    }

    public int MaxLod => NumFarLodLevels + NumNormalLodLevels - 2;

    public float GetSidelength(int lod)
    {
        int ml = MaxLod;
        int nl = (lod - NumFarLodLevels > -1) ? (lod - NumFarLodLevels + 1) : 0;
        return ((SingleBounds.MaxX - SingleBounds.MinX) / (float)(1 << ml)
                * (float)(1 << (ml - (lod - nl))) / (float)(1 << 18));
    }

    public int GetNodeShift(int lod) => Math.Max(0, lod - (NumFarLodLevels - 1));

    public (Dictionary<string, int> Layout, int GridSubdivision, int CornerOffset) GetNodeLayout(int i)
    {
        if (GetLayoutType(i) == 0)
        {
            return (FarLayout, 0, FarLayout["lod_far_corner_info_offset"]);
        }
        return (NormalLayout, 2, NormalLayout["ci0"]);
    }
}
