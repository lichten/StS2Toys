using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace StS2Toys.Services;

static class EnchantmentIconService
{
    static string? _extractedDir;
    static bool _dirSearched;
    static readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    static readonly Regex _ctexPathRe =
        new(@"path\.bptc=""(res://[^""]+)""", RegexOptions.Compiled);

    public static Bitmap? GetEnchantmentBitmap(string enchantmentId)
    {
        var name = ToName(enchantmentId);
        if (_cache.TryGetValue(name, out var cached)) return cached;
        return _cache[name] = LoadIcon(name);
    }

    static string ToName(string id)
    {
        var raw = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        return raw.ToLowerInvariant();
    }

    static Bitmap? LoadIcon(string name)
    {
        try
        {
            var dir = GetExtractedDir();
            if (dir is null) return null;

            var importPath = Path.Combine(dir, "images", "enchantments", $"{name}.png.import");
            if (!File.Exists(importPath)) return null;

            string? ctexRel = null;
            foreach (var line in File.ReadLines(importPath))
            {
                var m = _ctexPathRe.Match(line);
                if (m.Success) { ctexRel = m.Groups[1].Value; break; }
            }
            if (ctexRel is null) return null;

            // "res://.godot/imported/..." → 絶対パス
            var ctexPath = Path.Combine(dir,
                ctexRel["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(ctexPath)) return null;

            return DecodeCtex(ctexPath);
        }
        catch { return null; }
    }

    static Bitmap DecodeCtex(string ctexPath)
    {
        // GST2(.ctex) のデコードは StS2Shared.Assets.CtexDecoder に一元化。
        var rgba = StS2Shared.Assets.CtexDecoder.DecodeRgba(File.ReadAllBytes(ctexPath), out int width, out int height);

        // CtexDecoder は RGBA(R,G,B,A) 順。GDI+ Format32bppArgb はメモリ上 BGRA なので R↔B スワップが必要。
        for (int i = 0; i < rgba.Length; i += 4)
            (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bd  = bmp.LockBits(new Rectangle(0, 0, width, height),
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

    static string? GetExtractedDir()
    {
        if (_dirSearched) return _extractedDir;
        _dirSearched = true;
        return _extractedDir = StS2Shared.Services.AssetLocator.FindExtractedRoot();
    }
}
