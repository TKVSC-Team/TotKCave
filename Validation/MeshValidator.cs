using System.Numerics;
using TotkCave.Models;

namespace TotkCave.Validation;

public record ValidationStats(
    int Vertices,
    int Faces,
    int DroppedFaces,
    int OutOfBoundsVerts,
    bool IsSuspect
);

public static class MeshValidator
{
    public static ValidationStats ValidateMesh(CaveMesh mesh, CrBin crbin, float edgeLimit = 8.0f)
    {
        int oob = 0;
        Vector3 min = crbin.Aabb.Min;
        Vector3 max = crbin.Aabb.Max;

        foreach (Vector3 v in mesh.Vertices)
        {
            if (v.X < min.X - 4.0f || v.X > max.X + 4.0f ||
                v.Y < min.Y - 4.0f || v.Y > max.Y + 4.0f ||
                v.Z < min.Z - 4.0f || v.Z > max.Z + 4.0f)
            {
                oob++;
            }
        }

        int totalFaces = mesh.Faces.Count + mesh.DroppedFaces;
        bool isSuspect = (mesh.DroppedFaces > Math.Max(10, 0.001f * totalFaces)) ||
                         (oob > 0.01f * Math.Max(1, mesh.Vertices.Count));

        return new ValidationStats(
            mesh.Vertices.Count,
            mesh.Faces.Count,
            mesh.DroppedFaces,
            oob,
            isSuspect
        );
    }
}
