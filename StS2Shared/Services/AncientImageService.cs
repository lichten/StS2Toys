using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

/// <summary>
/// Ancient ID → Ancient 画像のソース相対パス（<c>ancients_png/</c> 基準、例 "orobas_placeholder.png"）。
/// card-type-extractor が実ファイル（<c>{id}_placeholder.png.import</c>）をスキャンして生成した ancient_images.json
/// （バージョンフォルダ）を参照する。PNG 実体は <c>ctex-to-png -- ancients</c> が
/// <c>tools/extracted/images/ancients_png/</c> に変換生成する。
/// 1 Ancient 1 主画像の対応をここに一元化する。<see cref="EventImageService"/> の Ancient 版。
/// </summary>
public static class AncientImageService
{
    /// <summary>画像ソースのベースディレクトリ名（<c>tools/extracted/images/</c> 配下）。</summary>
    public const string AncientsDirName = "ancients_png";

    static readonly IReadOnlyDictionary<string, string> _paths = Load();

    static IReadOnlyDictionary<string, string> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "ancient_images.json");
        if (name is null) return result;

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    /// <summary>
    /// Ancient 画像のソース相対パス（例 "orobas_placeholder.png"）。接頭辞なし ID で引く（大文字小文字不問）。
    /// 画像が無い Ancient（NEOW / TEZCATARA 等）は null。
    /// </summary>
    public static string? GetRelativePath(string ancientId)
    {
        if (string.IsNullOrEmpty(ancientId)) return null;
        return _paths.TryGetValue(ancientId, out var p) ? p : null;
    }

    /// <summary>
    /// <paramref name="ancientsRoot"/>（= <c>ancients_png</c> ディレクトリ）配下の画像相対パス（OS 区切り適用）。
    /// 画像が無い Ancient は null。
    /// </summary>
    public static string? GetSourcePath(string ancientsRoot, string ancientId)
    {
        var rel = GetRelativePath(ancientId);
        return rel is null ? null : Path.Combine(ancientsRoot, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}
