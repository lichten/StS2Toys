using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace StS2Toys.Services;

static class RelicImageService
{
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
            var ctexDir = StS2Shared.Services.AssetLocator.GodotImportedDir();
            if (ctexDir is null || !Directory.Exists(ctexDir)) return;

            var ctexPath = Directory.EnumerateFiles(ctexDir, "relic_atlas.png-*.bptc.ctex").FirstOrDefault();
            if (ctexPath is null) return;

            var atlasesDir = StS2Shared.Services.AssetLocator.ImagesDir("atlases");
            var tpsheetPath = atlasesDir is null ? null : Path.Combine(atlasesDir, "relic_atlas.tpsheet");
            if (tpsheetPath is null || !File.Exists(tpsheetPath)) return;

            _atlas = DecodeAtlas(ctexPath);
            _regions = ParseTpsheet(tpsheetPath);
        }
        catch { _atlas = null; _regions = null; }
    }

    static string? FindExtractedDir() => StS2Shared.Services.AssetLocator.FindExtractedRoot();

    static Bitmap DecodeAtlas(string ctexPath)
    {
        // GST2(.ctex) のデコードは StS2Shared.Assets.CtexDecoder に一元化。
        var rgba = StS2Shared.Assets.CtexDecoder.DecodeRgba(File.ReadAllBytes(ctexPath), out int width, out int height);

        // CtexDecoder は RGBA(R,G,B,A) 順。GDI+ Format32bppArgb はメモリ上 BGRA なので R↔B スワップが必要。
        for (int i = 0; i < rgba.Length; i += 4)
            (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = width * 4;
            for (int row = 0; row < height; row++)
                Marshal.Copy(rgba, row * rowBytes,
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

}
