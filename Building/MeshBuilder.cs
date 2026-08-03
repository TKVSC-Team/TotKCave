using System.Numerics;
using System.Runtime.InteropServices;
using TotkCave.Decoding;
using TotkCave.Models;
using TotkCave.PageSource;

namespace TotkCave.Building;

public static class MeshBuilder
{
    public static CaveMesh BuildMesh(
        CrBin crbin,
        IPageSource pages,
        int? lod = null,
        bool weld = true,
        float clean = 0.0f)
    {
        int targetLod = lod ?? crbin.NumSubdivisions;
        if (targetLod > crbin.NumSubdivisions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lod),
                $"Requested LOD {targetLod} exceeds CRBIN max subdivisions {crbin.NumSubdivisions}.");
        }

        float sl = crbin.MinSidelength * MathF.Pow(2.0f, crbin.NumSubdivisions - targetLod);
        float scale = sl / 4096.0f;

        if (clean > 0.0f)
        {
            clean *= (sl / crbin.MinSidelength);
        }

        float cleanSq = clean * clean;
        Vector3 origin = crbin.BasePos;

        CaveMesh mesh = new()
        {
            Materials = crbin.Materials
        };

        Dictionary<object, int> vmap = [];

        foreach (CrBinNode node in crbin.Nodes)
        {
            if (node.Lod != targetLod) continue;

            Vector3 nodeBase = new(
                origin.X - sl * 0.49993896f + node.Cell.X * sl,
                origin.Y - sl * 0.49993896f + node.Cell.Y * sl,
                origin.Z - sl * 0.49993896f + node.Cell.Z * sl
            );

            int streamEnd = (int)(node.BaseStream + node.StreamCount);
            for (int sIdx = (int)node.BaseStream; sIdx < streamEnd; sIdx++)
            {
                CrBinStream stream = crbin.Streams[sIdx];
                byte[] page = pages.GetPage(stream.PageFile);

                int triCount = (int)stream.Triangles;
                int indexOffset = (int)(stream.BaseIndex * 2);
                ReadOnlySpan<ushort> indices = MemoryMarshal.Cast<byte, ushort>(page.AsSpan(indexOffset, triCount * 3 * 2));

                Dictionary<ushort, (int GlobalIdx, int MaterialIdx)> localMap = [];

                foreach (ushort v in indices)
                {
                    if (localMap.ContainsKey(v)) continue;

                    DecodedVertex dec = VertexDecoder.DecodeVertex(page, v);
                    var (qx, qy, qz) = dec.Self.QPos;
                    var (w1, w2) = dec.Self.Weights;
                    var (nu, nv) = dec.Self.NormalRaw;

                    Vector3 worldPos = nodeBase + new Vector3(qx * scale, qy * scale, qz * scale);

                    int w0 = 31 - w1 - w2;
                    int domSlot = GetDominantSlotIndex(w0, w1, w2);
                    int domMat = dec.Materials[domSlot];
                    int matIdx = node.BaseMaterial + domMat;

                    object key = weld
                        ? (MathF.Round(worldPos.X, 4), MathF.Round(worldPos.Y, 4), MathF.Round(worldPos.Z, 4))
                        : (stream.PageFile, v);

                    if (!vmap.TryGetValue(key, out int globalIdx))
                    {
                        globalIdx = mesh.Vertices.Count;
                        vmap[key] = globalIdx;

                        mesh.Vertices.Add(worldPos);
                        mesh.Normals.Add(OctNormalDecoder.Decode(nu, nv));
                        mesh.Colors.Add(dec.Self.ColorRgb);
                    }

                    localMap[v] = (globalIdx, matIdx);
                }

                for (int t = 0; t < triCount; t++)
                {
                    ushort vA = indices[t * 3];
                    ushort vB = indices[t * 3 + 1];
                    ushort vC = indices[t * 3 + 2];

                    var (gA, matA) = localMap[vA];
                    var (gB, _) = localMap[vB];
                    var (gC, _) = localMap[vC];

                    if (gA == gB || gB == gC || gA == gC) continue;

                    if (clean > 0.0f)
                    {
                        Vector3 pA = mesh.Vertices[gA];
                        Vector3 pB = mesh.Vertices[gB];
                        Vector3 pC = mesh.Vertices[gC];

                        if (Vector3.DistanceSquared(pA, pB) > cleanSq ||
                            Vector3.DistanceSquared(pB, pC) > cleanSq ||
                            Vector3.DistanceSquared(pC, pA) > cleanSq)
                        {
                            mesh.DroppedFaces++;
                            continue;
                        }
                    }

                    mesh.Faces.Add((gA, gB, gC));
                    mesh.FaceMaterials.Add(matA);
                }
            }
        }

        return mesh;
    }

    private static int GetDominantSlotIndex(int w0, int w1, int w2)
    {
        if (w0 >= w1 && w0 >= w2) return 0;
        if (w1 >= w0 && w1 >= w2) return 1;
        return 2;
    }
}
