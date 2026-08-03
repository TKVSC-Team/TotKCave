using System.Diagnostics;
using System.Runtime.InteropServices;
using TotkCave.Models;

namespace TotkCave.PageSource;

public sealed class CavePageSource : IPageSource
{
    public const string CacheStamp = ".decompressed-by";
    public const string CacheTag = "totk-cave-tools meshcodec_fixes/1";

    private readonly CrBin _crbin;
    private readonly string? _pagesDir;
    private readonly string? _mcTool;
    private readonly Dictionary<int, byte[]> _cache = [];
    private readonly Dictionary<int, ushort> _blocks = [];

    public string SourceKind { get; private set; } = "unknown";

    public CavePageSource(CrBin crbin, string? caveDir = null, string? pagesDir = null, string? mcTool = null)
    {
        _crbin = crbin;
        _pagesDir = pagesDir;
        _mcTool = mcTool ?? Environment.GetEnvironmentVariable("MC_TOOL") ?? FindMcTool();

        foreach (CrBinPageFile pf in crbin.PageFiles)
        {
            _blocks[pf.PageFileIndex] = pf.BlockCount;
        }
    }

    public int GetExpectedSize(int fid)
    {
        ushort blocks = _blocks.GetValueOrDefault(fid, (ushort)0);
        return 0x10000 * blocks;
    }

    public bool IsDecompressedPage(string path, int fid)
    {
        try
        {
            FileInfo info = new(path);
            if (!info.Exists) return false;

            int exp = GetExpectedSize(fid);
            if (exp > 0 && info.Length != exp) return false;

            using FileStream stream = File.OpenRead(path);
            Span<byte> head = stackalloc byte[4];
            if (stream.Read(head) < 4) return false;

            uint chunkHash = MemoryMarshal.Read<uint>(head);
            if (chunkHash == _crbin.CaveId) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public byte[] GetPage(int fid)
    {
        if (_cache.TryGetValue(fid, out byte[]? cached))
            return cached;

        string chunkDir = _crbin.ChunkDirPath;
        string chunkFile = Path.Combine(chunkDir, $"{fid:D6}.chunk");

        if (File.Exists(chunkFile) && IsDecompressedPage(chunkFile, fid))
        {
            return Store(fid, File.ReadAllBytes(chunkFile), "console");
        }

        if (!string.IsNullOrEmpty(_pagesDir))
        {
            string cand1 = Path.Combine(_pagesDir, $"{fid:D6}");
            string cand2 = Path.Combine(_pagesDir, $"{fid:D6}.chunk");

            if (File.Exists(cand1)) return Store(fid, File.ReadAllBytes(cand1), "pages");
            if (File.Exists(cand2)) return Store(fid, File.ReadAllBytes(cand2), "pages");
        }

        string decDir = Path.Combine(chunkDir, "Decompressed");
        string decCand = Path.Combine(decDir, $"{fid:D6}");

        if (File.Exists(decCand))
        {
            if (IsCacheStamped(decDir))
            {
                return Store(fid, File.ReadAllBytes(decCand), "decompressed-dir");
            }
            if (string.IsNullOrEmpty(_mcTool))
            {
                return Store(fid, File.ReadAllBytes(decCand), "decompressed-dir");
            }
        }

        if (!string.IsNullOrEmpty(_mcTool) && File.Exists(_mcTool))
        {
            byte[] decompressed = DecompressWithTool(fid, decDir);
            return Store(fid, decompressed, "meshcodec");
        }

        throw new FileNotFoundException(
            $"No decompressed page available for chunk {fid:D6}. " +
            $"MeshCodec CLI tool not found and no console dump was provided.");
    }

    private byte[] Store(int fid, byte[] data, string kind)
    {
        _cache[fid] = data;
        if (SourceKind == "unknown")
            SourceKind = kind;
        return data;
    }

    private byte[] DecompressWithTool(int fid, string decDir)
    {
        Directory.CreateDirectory(decDir);

        ProcessStartInfo startInfo = new()
        {
            FileName = _mcTool!,
            Arguments = $"\"{_crbin.ChunkDirPath}\" \"{decDir}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start MeshCodec process '{_mcTool}'.");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            string err = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"MeshCodec decompression failed (Exit code {process.ExitCode}): {err}");
        }

        try
        {
            string stampFile = Path.Combine(decDir, CacheStamp);
            File.WriteAllText(stampFile, $"{CacheTag}\ntool: {_mcTool}\n");
        }
        catch { }

        string outPath = Path.Combine(decDir, $"{fid:D6}");
        if (!File.Exists(outPath))
            throw new FileNotFoundException($"Decompressed page output missing: {outPath}");

        return File.ReadAllBytes(outPath);
    }

    private static bool IsCacheStamped(string decDir)
    {
        string stampFile = Path.Combine(decDir, CacheStamp);
        try
        {
            if (!File.Exists(stampFile)) return false;
            string firstLine = File.ReadLines(stampFile).FirstOrDefault() ?? "";
            return firstLine.Trim() == CacheTag;
        }
        catch
        {
            return false;
        }
    }

    public static string? FindMcTool()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string cwd = Directory.GetCurrentDirectory();

        string[] candidates = [
            Path.Combine(cwd, "meshcodec-fix-tools", "mc_test_fixed.exe"),
            Path.Combine(cwd, "bin", "mc_decompress.exe"),
            Path.Combine(cwd, "MeshCodec", "build", "Release", "mc_test.exe"),
            Path.Combine(cwd, "MeshCodec", "build", "Release", "mc_decompress.exe"),
            Path.Combine(baseDir, "meshcodec-fix-tools", "mc_test_fixed.exe"),
            Path.Combine(baseDir, "bin", "mc_decompress.exe")
        ];

        foreach (string cand in candidates)
        {
            if (File.Exists(cand)) return cand;
        }

        return null;
    }
}
