namespace TotkCave.Utils;

public static class CaveFinder
{
    public static IEnumerable<(string Name, string Path)> FindCaves(string rootDir)
    {
        if (!Directory.Exists(rootDir))
            yield break;

        foreach (string crbinFile in Directory.EnumerateFiles(rootDir, "C.crbin", SearchOption.AllDirectories))
        {
            string? dirPath = Path.GetDirectoryName(crbinFile);
            if (!string.IsNullOrEmpty(dirPath))
            {
                yield return (Path.GetFileName(dirPath), dirPath);
            }
        }
    }
}
