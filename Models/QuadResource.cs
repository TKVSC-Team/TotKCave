using System.Numerics;
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
        if (_data.Length < 0x1DC || !_data[..8].SequenceEqual(CrBin.MagicBytes))
            throw new InvalidDataException("Bad magic header for quad resource.");

        Version = MemoryMarshal.Read<uint>(_data[0x0C..]);
        Id = MemoryMarshal.Read<uint>(_data[0x10..]);

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
                Transform[r, c] = MemoryMarshal.Read<float>(_data[(0x14 + (r * 4 + c) * 4)..]);

        int q = QuadMeshOffset;
        NumFarLodLevels = MemoryMarshal.Read<int>(_data[q..]);
        NumNormalLodLevels = MemoryMarshal.Read<int>(_data[(q + 4)..]);
        NumRootNodes = MemoryMarshal.Read<int>(_data[(q + 8)..]);

        int arrBase = q + 0x10;
        for (int i = 0; i < ArrayNames.Length; i++)
        {
            uint off = MemoryMarshal.Read<uint>(_data[(arrBase + i * 8)..]);
            uint cnt = MemoryMarshal.Read<uint>(_data[(arrBase + i * 8 + 4)..]);
            Arrays[ArrayNames[i]] = (off, cnt);
        }

        int sb = arrBase + ArrayNames.Length * 8;
        SingleBounds = (
            MemoryMarshal.Read<float>(_data[sb..]),
            MemoryMarshal.Read<float>(_data[(sb + 4)..]),
            MemoryMarshal.Read<float>(_data[(sb + 8)..]),
            MemoryMarshal.Read<float>(_data[(sb + 12)..]),
            MemoryMarshal.Read<float>(_data[(sb + 16)..]),
            MemoryMarshal.Read<float>(_data[(sb + 20)..])
        );

        int b = sb + 24;
        Bounds = (
            MemoryMarshal.Read<float>(_data[b..]),
            MemoryMarshal.Read<float>(_data[(b + 4)..]),
            MemoryMarshal.Read<float>(_data[(b + 8)..]),
            MemoryMarshal.Read<float>(_data[(b + 12)..]),
            MemoryMarshal.Read<float>(_data[(b + 16)..]),
            MemoryMarshal.Read<float>(_data[(b + 20)..])
        );

        int fl = sb + 48;
        for (int i = 0; i < LayoutFieldNames.Length; i++)
            FarLayout[LayoutFieldNames[i]] = MemoryMarshal.Read<int>(_data[(fl + i * 4)..]);

        int nl = fl + 0x38;
        for (int i = 0; i < LayoutFieldNames.Length; i++)
            NormalLayout[LayoutFieldNames[i]] = MemoryMarshal.Read<int>(_data[(nl + i * 4)..]);
    }

    public (ushort X, ushort Y, ushort Z, ushort Lod) GetNode(int i)
    {
        uint off = Arrays["nodes"].Offset + (uint)(i * 8);
        return (
            MemoryMarshal.Read<ushort>(_data[(int)off..]),
            MemoryMarshal.Read<ushort>(_data[(int)(off + 2)..]),
            MemoryMarshal.Read<ushort>(_data[(int)(off + 4)..]),
            MemoryMarshal.Read<ushort>(_data[(int)(off + 6)..])
        );
    }

    public (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) GetNodeBounds(int i)
    {
        uint off = Arrays["node_bounds"].Offset + (uint)(i * 24);
        return (
            MemoryMarshal.Read<float>(_data[(int)off..]),
            MemoryMarshal.Read<float>(_data[(int)(off + 4)..]),
            MemoryMarshal.Read<float>(_data[(int)(off + 8)..]),
            MemoryMarshal.Read<float>(_data[(int)(off + 12)..]),
            MemoryMarshal.Read<float>(_data[(int)(off + 16)..]),
            MemoryMarshal.Read<float>(_data[(int)(off + 20)..])
        );
    }

    public byte GetLayoutType(int i) => _data[(int)(Arrays["layout_types"].Offset + i)];

    public (uint BaseStream, uint Count) GetStreamRange(int i)
    {
        uint off = Arrays["stream_dependencies"].Offset + (uint)(i * 8);
        return (
            MemoryMarshal.Read<uint>(_data[(int)off..]),
            MemoryMarshal.Read<uint>(_data[(int)(off + 4)..])
        );
    }

    public (int PageFileIndex, ushort Flags, ushort BaseVertexIndex, ushort NumQuads) GetStream(int j)
    {
        uint off = Arrays["stream_info"].Offset + (uint)(j * 6);
        ushort pf = MemoryMarshal.Read<ushort>(_data[(int)off..]);
        ushort baseIndex = MemoryMarshal.Read<ushort>(_data[(int)(off + 2)..]);
        ushort numQuads = MemoryMarshal.Read<ushort>(_data[(int)(off + 4)..]);
        return (pf & 0x1FFF, (ushort)(pf >> 13), baseIndex, numQuads);
    }

    public (uint DecompressedSize, uint Id) GetPageFile(int i)
    {
        uint off = Arrays["page_files"].Offset + (uint)(i * 8);
        return (
            MemoryMarshal.Read<uint>(_data[(int)off..]),
            MemoryMarshal.Read<uint>(_data[(int)(off + 4)..])
        );
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
