using System.Numerics;

namespace TotkCave.Models;

public sealed class CaveMesh
{
    public List<Vector3> Vertices { get; } = [];
    public List<Vector3> Normals { get; } = [];
    public List<Vector3> Colors { get; } = [];
    public List<(int A, int B, int C)> Faces { get; } = [];
    public List<int> FaceMaterials { get; } = [];
    public List<CrBinMaterial> Materials { get; set; } = [];
    public int DroppedFaces { get; set; }
}
