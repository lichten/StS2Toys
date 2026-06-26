using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using Pfim;

namespace StS2Toys.Services;

static class RelicImageService
{
    const int CtexHeaderOffset = 52; // GST2ヘッダーサイズ

    static bool _loadAttempted;
    static Bitmap? _atlas;
    static Dictionary<string, Rectangle>? _regions;
    static readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Bitmap?> _pngCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// レリックの個別 PNG（<c>tools/extracted/images/relics_png/</c> 配下）を読む。
    /// パスは <see cref="StS2Shared.Services.RelicImageService.GetRelativePath"/>（relic_images.json）で解決するため、
    /// レリック追加・画像差し替えは JSON/PNG の更新だけで追従する（atlas 切り出しの <see cref="GetRelicBitmap"/> とは別系統）。
    /// 画像が無い（未マッピング／未生成）レリックは null。
    /// </summary>
    public static Bitmap? GetRelicPng(string relicId)
    {
        if (_pngCache.TryGetValue(relicId, out var cached)) return cached;

        var rel = StS2Shared.Services.RelicImageService.GetRelativePath(relicId);
        var root = FindExtractedDir();
        if (rel is null || root is null) return _pngCache[relicId] = null;

        var path = Path.Combine(root, "images",
            StS2Shared.Services.RelicImageService.RelicsDirName,
            rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return _pngCache[relicId] = null;

        try
        {
            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            return _pngCache[relicId] = new Bitmap(img);
        }
        catch { return _pngCache[relicId] = null; }
    }

    public static Bitmap? GetRelicBitmap(string relicId)
    {
        var key = ToFilename(relicId);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        if (!_loadAttempted) Load();

        if (_atlas is null || _regions is null || !_regions.TryGetValue(key, out var rect))
            return _cache[key] = null;

        var crop = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(crop);
        g.DrawImage(_atlas, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
        return _cache[key] = crop;
    }

    static string ToFilename(string relicId)
    {
        var raw = relicId.Contains('.') ? relicId[(relicId.LastIndexOf('.') + 1)..] : relicId;
        return raw.ToLowerInvariant() + ".png";
    }

    static void Load()
    {
        _loadAttempted = true;
        try
        {
            var extractedDir = FindExtractedDir();
            if (extractedDir is null) return;

            var ctexDir = Path.Combine(extractedDir, ".godot", "imported");
            var ctexPath = Directory.EnumerateFiles(ctexDir, "relic_atlas.png-*.bptc.ctex").FirstOrDefault();
            if (ctexPath is null) return;

            var tpsheetPath = Path.Combine(extractedDir, "images", "atlases", "relic_atlas.tpsheet");
            if (!File.Exists(tpsheetPath)) return;

            _atlas = DecodeAtlas(ctexPath);
            _regions = ParseTpsheet(tpsheetPath);
        }
        catch { _atlas = null; _regions = null; }
    }

    static string? FindExtractedDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "extracted");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    static Bitmap DecodeAtlas(string ctexPath)
    {
        using var fs = File.OpenRead(ctexPath);
        using var br = new BinaryReader(fs, System.Text.Encoding.UTF8, leaveOpen: true);

        // GST2ヘッダーからアトラスのサイズを読む（offset 8 = width, 12 = height）
        fs.Seek(8, SeekOrigin.Begin);
        int width  = (int)br.ReadUInt32();
        int height = (int)br.ReadUInt32();

        // offset 52 以降が生BC7データ
        fs.Seek(CtexHeaderOffset, SeekOrigin.Begin);
        var bc7 = new byte[fs.Length - CtexHeaderOffset];
        fs.ReadExactly(bc7);

        // DDSコンテナを構築してPfimに渡す
        using var ms = new MemoryStream();
        ms.Write(BuildDdsHeader(width, height, bc7.Length));
        ms.Write(bc7);
        ms.Position = 0;

        using var image = Pfimage.FromStream(ms);

        // Pfim: RGBA32 (R=byte0, G=byte1, B=byte2, A=byte3)
        // GDI+: Format32bppArgb = BGRA in memory → R↔B スワップが必要
        var px = image.Data;
        for (int i = 0; i < px.Length; i += 4)
            (px[i], px[i + 2]) = (px[i + 2], px[i]);

        var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = image.Width * 4;
            for (int row = 0; row < image.Height; row++)
                Marshal.Copy(px, row * image.Stride,
                    IntPtr.Add(bd.Scan0, row * bd.Stride), rowBytes);
        }
        finally { bmp.UnlockBits(bd); }

        return bmp;
    }

    static Dictionary<string, Rectangle> ParseTpsheet(string tpsheetPath)
    {
        var result = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
        using var stream = File.OpenRead(tpsheetPath);
        var doc = JsonDocument.Parse(stream);
        foreach (var tex in doc.RootElement.GetProperty("textures").EnumerateArray())
        {
            foreach (var sprite in tex.GetProperty("sprites").EnumerateArray())
            {
                var filename = sprite.GetProperty("filename").GetString() ?? "";
                var region = sprite.GetProperty("region");
                result[filename] = new Rectangle(
                    region.GetProperty("x").GetInt32(),
                    region.GetProperty("y").GetInt32(),
                    region.GetProperty("w").GetInt32(),
                    region.GetProperty("h").GetInt32());
            }
        }
        return result;
    }

    // DDS + DX10拡張ヘッダーを構築（合計148バイト）
    static byte[] BuildDdsHeader(int width, int height, int dataSize)
    {
        using var ms = new MemoryStream(148);
        using var bw = new BinaryWriter(ms);

        bw.Write(0x20534444u);   // magic: 'DDS '

        // DDS_HEADER (124 bytes)
        bw.Write(124u);          // dwSize
        bw.Write(0x81007u);      // dwFlags: CAPS|HEIGHT|WIDTH|PIXELFORMAT|LINEARSIZE
        bw.Write((uint)height);
        bw.Write((uint)width);
        bw.Write((uint)dataSize);
        bw.Write(0u);            // dwDepth
        bw.Write(1u);            // dwMipMapCount
        for (int i = 0; i < 11; i++) bw.Write(0u); // dwReserved1[11]

        // DDS_PIXELFORMAT (32 bytes)
        bw.Write(32u);           // dwSize
        bw.Write(0x4u);          // dwFlags: DDPF_FOURCC
        bw.Write(0x30315844u);   // dwFourCC: 'DX10'
        bw.Write(0u); bw.Write(0u); bw.Write(0u); bw.Write(0u); bw.Write(0u); // unused

        // Caps
        bw.Write(0x1000u);       // dwCaps: DDSCAPS_TEXTURE
        bw.Write(0u); bw.Write(0u); bw.Write(0u); bw.Write(0u); // dwCaps2-4, reserved

        // DDS_HEADER_DX10 (20 bytes)
        bw.Write(98u);           // dxgiFormat: DXGI_FORMAT_BC7_UNORM
        bw.Write(3u);            // resourceDimension: D3D10_RESOURCE_DIMENSION_TEXTURE2D
        bw.Write(0u);            // miscFlag
        bw.Write(1u);            // arraySize
        bw.Write(0u);            // miscFlags2

        return ms.ToArray();
    }
}
