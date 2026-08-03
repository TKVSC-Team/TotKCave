using System.Runtime.InteropServices;
using TotkCave.Models;

namespace TotkCave.PageSource;

public sealed class QuadPageSource : IPageSource
{
    private readonly QuadResource _resource;
    private readonly string? _pagesDir;
    private readonly Dictionary<int, byte[]> _cache = [];

    public string SourceKind { get; private set; } = "quad";

    public QuadPageSource(QuadResource resource, string? pagesDir = null)
    {
        _resource = resource;
        _pagesDir = pagesDir;
    }

    public byte[] GetPage(int fid)
    {
        if (_cache.TryGetValue(fid, out byte[]? cached))
            return cached;

        (uint decompressedSize, uint pageId) = _resource.GetPageFile(fid);

        string pageDir = _resource.Path + $".{_resource.Id:x8}";
        string cand1 = Path.Combine(pageDir, $"{fid:D6}.quad");
        string cand2 = Path.Combine(pageDir, $"{fid:D6}");
        if (!string.IsNullOrEmpty(_pagesDir))
        {
            string cand3 = Path.Combine(_pagesDir, $"{fid:D6}.quad");
            string cand4 = Path.Combine(_pagesDir, $"{fid:D6}");
            if (File.Exists(cand3)) cand1 = cand3;
            else if (File.Exists(cand4)) cand1 = cand4;
        }

        if (!File.Exists(cand1) && File.Exists(cand2))
            cand1 = cand2;

        if (!File.Exists(cand1))
            throw new FileNotFoundException($"Quad page file missing for page {fid}: {cand1}");

        byte[] raw = File.ReadAllBytes(cand1);
        if (raw.Length == decompressedSize)
        {
            _cache[fid] = raw;
            return raw;
        }

        if (raw.Length > 4 && MemoryMarshal.Read<uint>(raw) == _resource.Id)
        {
            byte[] decompressed = DecompressZstdFrame(raw.AsSpan(4), (int)decompressedSize);
            _cache[fid] = decompressed;
            return decompressed;
        }

        _cache[fid] = raw;
        return raw;
    }

    private static byte[] DecompressZstdFrame(ReadOnlySpan<byte> compressedFrame, int expectedSize)
    {
        try
        {
            var zstdType = Type.GetType("ZStandard.ZstandardStream, ZStandard")
                ?? Type.GetType("Zstandard.Net.ZstandardStream, Zstandard.Net");

            if (zstdType != null)
            {
                using MemoryStream ms = new(compressedFrame.ToArray());
                using Stream zstream = (Stream)Activator.CreateInstance(zstdType, ms)!;
                byte[] outBuf = new byte[expectedSize];
                int read = zstream.Read(outBuf, 0, expectedSize);
                return outBuf;
            }
        }
        catch { }

        throw new NotSupportedException(
            "Compressed .quad page encountered. Please install the ZStandard NuGet package " +
            "or use pre-decompressed console dump quad pages.");
    }
}
