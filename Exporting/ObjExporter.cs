using System.Globalization;
using System.Numerics;
using System.Text;
using TotkCave.Models;

namespace TotkCave.Exporting;

public record ObjExportOptions(
    bool IncludeColors = true,
    bool IncludeNormals = true,
    bool IncludeGroups = false,
    bool IncludeMaterials = false,
    string TextureDir = "textures",
    string HeaderComment = ""
);

public static class ObjExporter
{
    public static void WriteObj(CaveMesh mesh, string path, ObjExportOptions options)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        bool exportMaterials = options.IncludeMaterials;
        bool exportGroups = options.IncludeGroups || exportMaterials;

        if (exportMaterials)
        {
            string mtlPath = Path.ChangeExtension(path, ".mtl");
            WriteMtl(mesh, mtlPath, options.TextureDir);
        }

        using StreamWriter writer = new(path, false, Encoding.UTF8);
        writer.WriteLine("# Exported by TotkCave (.NET 10)");
        if (!string.IsNullOrEmpty(options.HeaderComment))
        {
            writer.WriteLine($"# {options.HeaderComment}");
        }

        string baseName = Path.GetFileNameWithoutExtension(path);
        if (exportMaterials)
        {
            writer.WriteLine($"mtllib {baseName}.mtl");
        }

        CultureInfo ci = CultureInfo.InvariantCulture;

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            Vector3 v = mesh.Vertices[i];
            if (options.IncludeColors && i < mesh.Colors.Count)
            {
                Vector3 c = mesh.Colors[i];
                writer.WriteLine(string.Create(ci, $"v {v.X:F4} {v.Y:F4} {v.Z:F4} {c.X:F3} {c.Y:F3} {c.Z:F3}"));
            }
            else
            {
                writer.WriteLine(string.Create(ci, $"v {v.X:F4} {v.Y:F4} {v.Z:F4}"));
            }
        }

        if (options.IncludeNormals)
        {
            foreach (Vector3 n in mesh.Normals)
            {
                writer.WriteLine(string.Create(ci, $"vn {n.X:F4} {n.Y:F4} {n.Z:F4}"));
            }
        }

        if (exportGroups)
        {
            Dictionary<int, List<(int A, int B, int C)>> byMat = [];
            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                int m = f < mesh.FaceMaterials.Count ? mesh.FaceMaterials[f] : 0;
                if (!byMat.TryGetValue(m, out var list))
                {
                    list = [];
                    byMat[m] = list;
                }
                list.Add(mesh.Faces[f]);
            }

            int vtCount = 0;
            foreach (int m in byMat.Keys.OrderBy(k => k))
            {
                CrBinMaterial? mat = m < mesh.Materials.Count ? mesh.Materials[m] : null;
                int layer = mat != null ? (int)mat.ArrayLayer : 0;

                writer.WriteLine($"g mat_{m}_layer{layer}");
                writer.WriteLine($"usemtl mat_{m}_layer{layer}");

                foreach (var (a, b, c) in byMat[m])
                {
                    if (exportMaterials && mat != null)
                    {
                        Vector2 uvA = CalculateTriplanarUv(mat, mesh.Vertices[a]);
                        Vector2 uvB = CalculateTriplanarUv(mat, mesh.Vertices[b]);
                        Vector2 uvC = CalculateTriplanarUv(mat, mesh.Vertices[c]);

                        writer.WriteLine(string.Create(ci, $"vt {uvA.X:F5} {uvA.Y:F5}"));
                        writer.WriteLine(string.Create(ci, $"vt {uvB.X:F5} {uvB.Y:F5}"));
                        writer.WriteLine(string.Create(ci, $"vt {uvC.X:F5} {uvC.Y:F5}"));

                        int t0 = vtCount + 1;
                        vtCount += 3;

                        if (options.IncludeNormals)
                        {
                            writer.WriteLine($"f {a + 1}/{t0}/{a + 1} {b + 1}/{t0 + 1}/{b + 1} {c + 1}/{t0 + 2}/{c + 1}");
                        }
                        else
                        {
                            writer.WriteLine($"f {a + 1}/{t0} {b + 1}/{t0 + 1} {c + 1}/{t0 + 2}");
                        }
                    }
                    else
                    {
                        WriteFace(writer, a, b, c, options.IncludeNormals);
                    }
                }
            }
        }
        else
        {
            foreach (var (a, b, c) in mesh.Faces)
            {
                WriteFace(writer, a, b, c, options.IncludeNormals);
            }
        }
    }

    private static void WriteFace(StreamWriter writer, int a, int b, int c, bool includeNormals)
    {
        if (includeNormals)
        {
            writer.WriteLine($"f {a + 1}//{a + 1} {b + 1}//{b + 1} {c + 1}//{c + 1}");
        }
        else
        {
            writer.WriteLine($"f {a + 1} {b + 1} {c + 1}");
        }
    }

    public static List<int> WriteMtl(CaveMesh mesh, string path, string textureDir = "textures")
    {
        List<int> used = mesh.FaceMaterials.Distinct().OrderBy(x => x).ToList();
        using StreamWriter writer = new(path, false, Encoding.UTF8);
        writer.WriteLine("# totk-cave-tools materials (triplanar; layer = texture array index)");

        CultureInfo ci = CultureInfo.InvariantCulture;

        foreach (int m in used)
        {
            CrBinMaterial? mat = m < mesh.Materials.Count ? mesh.Materials[m] : null;
            int layer = mat != null ? (int)mat.ArrayLayer : 0;
            Vector3 col = GetMaterialColor(layer);

            writer.WriteLine($"newmtl mat_{m}_layer{layer}");
            writer.WriteLine(string.Create(ci, $"Kd {col.X:F3} {col.Y:F3} {col.Z:F3}"));
            writer.WriteLine("Ka 0.1 0.1 0.1");
            writer.WriteLine("Ks 0 0 0");
            writer.WriteLine($"map_Kd {textureDir}/layer_{layer:D3}.png");
            writer.WriteLine();
        }

        return used;
    }

    public static Vector2 CalculateTriplanarUv(CrBinMaterial mat, Vector3 pos)
    {
        float u = Vector3.Dot(pos, mat.UBias) * mat.UvScale;
        float v = Vector3.Dot(pos, mat.VBias) * mat.UvScale;
        return new Vector2(u, v);
    }

    private static Vector3 GetMaterialColor(int layer)
    {
        float h = (layer * 47) % 360 / 360.0f;
        int i = (int)(h * 6.0f);
        float f = h * 6.0f - i;
        float p = 0.35f;
        float q = 0.85f - 0.5f * f;
        float t = 0.35f + 0.5f * f;

        return (i % 6) switch
        {
            0 => new Vector3(0.85f, t, p),
            1 => new Vector3(q, 0.85f, p),
            2 => new Vector3(p, 0.85f, t),
            3 => new Vector3(p, q, 0.85f),
            4 => new Vector3(t, p, 0.85f),
            _ => new Vector3(0.85f, p, q),
        };
    }
}
