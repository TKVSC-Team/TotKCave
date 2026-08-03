using System.Numerics;
using System.Runtime.InteropServices;
using TotkCave.Models;
using TotkCave.PageSource;

namespace TotkCave.Building;

public static class QuadMeshBuilder
{
    private static readonly Dictionary<(int Ns, int Top, int Right, int Bottom, int Left, int Single), int[]> IndexCache = [];
    private static readonly Dictionary<int, (int A, int B, int C)[]> FaceCache = [];

    public static CaveMesh BuildMesh(QuadResource res, IPageSource pages, int? lod = null, bool weld = true)
    {
        int targetLod = lod ?? res.MaxLod;
        CaveMesh mesh = new();
        Dictionary<Vector3, int> vmap = [];

        for (int i = 0; i < res.NodeCount; i++)
        {
            var (nx, ny, nz, nodeLod) = res.GetNode(i);
            if (nodeLod != targetLod) continue;

            var (layout, ns, cornerOff) = res.GetNodeLayout(i);
            int vps = (1 << ns) + 1;
            int nvq = vps * vps;
            int blockBytes = nvq * 4;
            var facesPat = GetFacePattern(ns);
            float sl = res.GetSidelength(targetLod);
            int nsh = res.GetNodeShift(targetLod);

            float bx = res.SingleBounds.MinX;
            float by = res.SingleBounds.MinY;
            float bz = res.SingleBounds.MinZ;

            int pa0 = layout["pos_adjust_offset0"];
            int qdo = layout["quad_data_offset"];

            var (s0, s1) = res.GetStreamRange(i);
            for (uint j = s0; j < s1; j++)
            {
                var (pfi, flags, baseVtx, nquads) = res.GetStream((int)j);
                if (flags != 0) continue;

                byte[] page = pages.GetPage(pfi);

                for (int k = 0; k < nquads; k++)
                {
                    int qi = baseVtx + k;
                    uint corner = MemoryMarshal.Read<uint>(page.AsSpan(cornerOff + qi * 4));
                    uint posFlags = MemoryMarshal.Read<uint>(page.AsSpan(qdo + qi * 8));
                    uint matFlags = MemoryMarshal.Read<uint>(page.AsSpan(qdo + qi * 8 + 4));

                    int top = (corner & 0xFF) == 0xD ? 0 : 1;
                    int right = ((corner >> 8) & 0xFF) == 0xD ? 0 : 1;
                    int bottom = ((corner >> 16) & 0xFF) == 0xD ? 0 : 1;
                    int left = ((corner >> 24) & 0xFF) == 0xD ? 0 : 1;
                    int single = (int)((matFlags >> 31) & 1);

                    int[] imap = GetIndexMap(ns, top, right, bottom, left, single);

                    ReadOnlySpan<uint> block = MemoryMarshal.Cast<byte, uint>(page.AsSpan(pa0 + qi * blockBytes, blockBytes));

                    int sh = (int)((posFlags >> 18) & 0x1F);
                    long ox = ((((nx >> nsh) << 5) + (posFlags & 0x3F)) << 13) - 0x20000;
                    long oy = ((((ny >> nsh) << 5) + ((posFlags >> 6) & 0x3F)) << 13) - 0x20000;
                    long oz = ((((nz >> nsh) << 5) + ((posFlags >> 12) & 0x3F)) << 13) - 0x20000;

                    int[] localIndices = new int[imap.Length];

                    for (int slotIdx = 0; slotIdx < imap.Length; slotIdx++)
                    {
                        int slot = imap[slotIdx];
                        uint adj = block[slot];

                        int dx = (int)(adj & 0x7FF);
                        if ((dx & 0x400) != 0) dx -= 0x800;

                        int dy = (int)((adj >> 11) & 0x3FF);
                        if ((dy & 0x200) != 0) dy -= 0x400;

                        int dz = (int)((adj >> 21) & 0x7FF);
                        if ((dz & 0x400) != 0) dz -= 0x800;

                        Vector3 v = new(
                            (ox + (dx << sh)) * sl + bx,
                            (oy + ((dy << 1) << sh)) * sl + by,
                            (oz + (dz << sh)) * sl + bz
                        );

                        if (weld)
                        {
                            if (!vmap.TryGetValue(v, out int globalIdx))
                            {
                                globalIdx = mesh.Vertices.Count;
                                vmap[v] = globalIdx;
                                mesh.Vertices.Add(v);
                            }
                            localIndices[slotIdx] = globalIdx;
                        }
                        else
                        {
                            int globalIdx = mesh.Vertices.Count;
                            mesh.Vertices.Add(v);
                            localIndices[slotIdx] = globalIdx;
                        }
                    }

                    foreach (var (a, b_, c) in facesPat)
                    {
                        int iA = localIndices[a];
                        int iB = localIndices[b_];
                        int iC = localIndices[c];

                        if (iA != iB && iB != iC && iA != iC)
                        {
                            mesh.Faces.Add((iA, iB, iC));
                            mesh.FaceMaterials.Add(0);
                        }
                    }
                }
            }
        }

        return mesh;
    }

    private static int[] GetIndexMap(int ns, int top, int right, int bottom, int left, int single)
    {
        var key = (ns, top, right, bottom, left, single);
        if (IndexCache.TryGetValue(key, out int[]? cached)) return cached;

        int vps = (1 << ns) + 1;
        int mx = 1 << ns;
        int[] outMap = new int[vps * vps];
        int idx = 0;

        for (int vy = 0; vy < vps; vy++)
        {
            for (int vx = 0; vx < vps; vx++)
            {
                int ax, ay;
                if (ns == 0)
                {
                    ax = ay = 0;
                }
                else if (single != 0)
                {
                    int diag = (vx + vy == mx) ? 1 : 0;
                    ax = (vx & diag & right) - (vx & (vy == 0 ? 1 : 0) & top);
                    ay = (vy & (vx == 0 ? 1 : 0) & left) - (vy & diag & right);
                }
                else
                {
                    ax = (vx & (vy == mx ? 1 : 0) & bottom) - (vx & (vy == 0 ? 1 : 0) & top);
                    ay = (vy & (vx == 0 ? 1 : 0) & left) - (vy & (vx == mx ? 1 : 0) & right);
                }
                outMap[idx++] = (vx + ax) + (vy + ay) * vps;
            }
        }

        IndexCache[key] = outMap;
        return outMap;
    }

    private static (int A, int B, int C)[] GetFacePattern(int ns)
    {
        if (FaceCache.TryGetValue(ns, out var cached)) return cached;

        int vps = (1 << ns) + 1;
        List<(int A, int B, int C)> faces = [];

        for (int yy = 0; yy < vps - 1; yy++)
        {
            for (int xx = 0; xx < vps - 1; xx++)
            {
                int q = xx + yy * vps;
                faces.Add((q, q + 1, q + vps));
                faces.Add((q + vps, q + 1, q + vps + 1));
            }
        }

        var res = faces.ToArray();
        FaceCache[ns] = res;
        return res;
    }
}
