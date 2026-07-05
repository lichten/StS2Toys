using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace StS2Toys.Services;

static class CardAtlasService
{
    record AtlasEntry(string AtlasPngPath, Rectangle Region);

    static bool _loadAttempted;
    static Dictionary<string, AtlasEntry>? _entries;
    static readonly Dictionary<string, Bitmap?> _atlasCache = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Bitmap?> _cropCache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? GetCardBitmap(string cardId)
    {
        var raw = cardId.Contains('.') ? cardId[(cardId.LastIndexOf('.') + 1)..] : cardId;
        var filename = raw.ToLowerInvariant() + ".png";

        if (_cropCache.TryGetValue(filename, out var cached)) return cached;
        if (!_loadAttempted) Load();
        if (_entries is null || !_entries.TryGetValue(filename, out var entry))
            return _cropCache[filename] = null;

        var atlas = LoadAtlas(entry.AtlasPngPath);
        if (atlas is null) return _cropCache[filename] = null;

        try
        {
            var crop = new Bitmap(entry.Region.Width, entry.Region.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(crop);
            g.DrawImage(atlas,
                new Rectangle(0, 0, entry.Region.Width, entry.Region.Height),
                entry.Region, GraphicsUnit.Pixel);
            return _cropCache[filename] = crop;
        }
        catch { return _cropCache[filename] = null; }
    }

    static Bitmap? LoadAtlas(string path)
    {
        if (_atlasCache.TryGetValue(path, out var cached)) return cached;
        try { return _atlasCache[path] = new Bitmap(path); }
        catch { return _atlasCache[path] = null; }
    }

    static void Load()
    {
        _loadAttempted = true;
        try
        {
            var spritesDir = FindSpritesDir();
            if (spritesDir is null) return;

            var atlasesDir = Path.GetDirectoryName(spritesDir)!;
            _entries = new Dictionary<string, AtlasEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var tresPath in Directory.EnumerateFiles(spritesDir, "*.tres", SearchOption.AllDirectories))
            {
                var entry = ParseTres(tresPath, atlasesDir);
                if (entry is null) continue;
                var filename = Path.GetFileNameWithoutExtension(tresPath).ToLowerInvariant() + ".png";
                _entries.TryAdd(filename, entry);
            }
        }
        catch { _entries = null; }
    }

    static AtlasEntry? ParseTres(string tresPath, string atlasesDir)
    {
        try
        {
            var text = File.ReadAllText(tresPath);

            var pathMatch = Regex.Match(text, @"path=""res://images/atlases/([^""]+)""");
            if (!pathMatch.Success) return null;

            var atlasPngPath = Path.Combine(atlasesDir, pathMatch.Groups[1].Value);
            if (!File.Exists(atlasPngPath)) return null;

            var regionMatch = Regex.Match(text,
                @"region = Rect2\((\d+(?:\.\d+)?),\s*(\d+(?:\.\d+)?),\s*(\d+(?:\.\d+)?),\s*(\d+(?:\.\d+)?)\)");
            if (!regionMatch.Success) return null;

            static int ToInt(string s) =>
                (int)float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

            return new AtlasEntry(atlasPngPath, new Rectangle(
                ToInt(regionMatch.Groups[1].Value),
                ToInt(regionMatch.Groups[2].Value),
                ToInt(regionMatch.Groups[3].Value),
                ToInt(regionMatch.Groups[4].Value)));
        }
        catch { return null; }
    }

    static string? FindSpritesDir()
    {
        var atlasesDir = StS2Shared.Services.AssetLocator.ImagesDir("atlases");
        if (atlasesDir is null) return null;
        var candidate = Path.Combine(atlasesDir, "card_atlas.sprites");
        return Directory.Exists(candidate) ? candidate : null;
    }
}
