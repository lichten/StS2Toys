namespace StS2Toys.Services;

/// <summary>
/// モンスター dir 名（例 "two_tailed_rat"）→ <c>tools/extracted/images/monsters/{dir}.png</c> を読み込む。
/// RelicImageService.GetRelicPng と同方式（個別 PNG・上方探索・キャッシュ）。画像が無ければ null。
/// </summary>
static class MonsterImageService
{
    static readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? GetMonsterPng(string dirName)
    {
        if (string.IsNullOrEmpty(dirName)) return null;
        if (_cache.TryGetValue(dirName, out var cached)) return cached;

        var root = FindExtractedDir();
        if (root is null) return _cache[dirName] = null;

        var path = Path.Combine(root, "images", "monsters", dirName + ".png");
        if (!File.Exists(path)) return _cache[dirName] = null;

        try
        {
            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            return _cache[dirName] = new Bitmap(img);
        }
        catch { return _cache[dirName] = null; }
    }

    static string? FindExtractedDir() => StS2Shared.Services.AssetLocator.FindExtractedRoot();
}
