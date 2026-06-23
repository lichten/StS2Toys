using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

/// <summary>
/// ポーション ID → ポーション画像のソース相対パス（<c>potions_png/</c> 基準、例 "fire_potion.png"）。
/// card-type-extractor が実ファイル（<c>.png.import</c>）をスキャンして生成した potion_images.json
/// （バージョンフォルダ）を参照する。PNG 実体は <c>ctex-to-png -- potions</c> が
/// <c>tools/extracted/images/potions_png/</c> に変換生成する。
/// <see cref="RelicImageService"/> のポーション版。
/// </summary>
public static class PotionImageService
{
    /// <summary>画像ソースのベースディレクトリ名（<c>tools/extracted/images/</c> 配下）。</summary>
    public const string PotionsDirName = "potions_png";

    static readonly IReadOnlyDictionary<string, string> _paths = Load();

    static IReadOnlyDictionary<string, string> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "potion_images.json");
        if (name is null) return result;

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    /// <summary>収録されている全ポーション ID（接頭辞なし大文字）。DB 構築用。</summary>
    public static IEnumerable<string> Ids => _paths.Keys;

    /// <summary>
    /// ポーション画像のソース相対パス（例 "fire_potion.png"）。接頭辞なし ID（"FIRE_POTION"）・
    /// <c>POTION.</c> 接頭辞付き ID どちらでも引ける。画像が無いポーションは null。
    /// </summary>
    public static string? GetRelativePath(string potionId)
    {
        if (string.IsNullOrEmpty(potionId)) return null;
        if (_paths.TryGetValue(potionId, out var p)) return p;
        if (potionId.Contains('.') &&
            _paths.TryGetValue(potionId[(potionId.IndexOf('.') + 1)..], out var p2)) return p2;
        return null;
    }

    /// <summary>
    /// <paramref name="potionsRoot"/>（= <c>potions_png</c> ディレクトリ）配下の画像相対パス（OS 区切り適用）。
    /// 画像が無いポーションは null。
    /// </summary>
    public static string? GetSourcePath(string potionsRoot, string potionId)
    {
        var rel = GetRelativePath(potionId);
        return rel is null ? null : Path.Combine(potionsRoot, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}
