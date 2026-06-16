using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

/// <summary>
/// レリック ID → レリック画像のソース相対パス（<c>relics/</c> 基準、例 "akabeko.png"・"beta/belt_buckle.png"）。
/// card-type-extractor が実ファイル（<c>.png.import</c>）をスキャンして生成した relic_images.json
/// （バージョンフォルダ）を参照する。「どのサブディレクトリにどのファイル名で存在するか」の対応を
/// ここに一元化する。<see cref="CardImageService"/> のレリック版。
///
/// 注: <c>StS2Toys.Services.RelicImageService</c>（atlas 描画）とは別物（名前空間が異なる）。
/// </summary>
public static class RelicImageService
{
    /// <summary>画像ソースのベースディレクトリ名（<c>tools/extracted/images/</c> 配下）。</summary>
    public const string RelicsDirName = "relics";

    static readonly IReadOnlyDictionary<string, string> _paths = Load();

    static IReadOnlyDictionary<string, string> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "relic_images.json");
        if (name is null) return result;

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    /// <summary>
    /// レリック画像のソース相対パス（例 "akabeko.png"・"beta/belt_buckle.png"）。
    /// 接頭辞なし ID（"AKABEKO"）・<c>RELIC.</c> 接頭辞付き ID どちらでも引ける。画像が無いレリックは null。
    /// </summary>
    public static string? GetRelativePath(string relicId)
    {
        if (string.IsNullOrEmpty(relicId)) return null;
        if (_paths.TryGetValue(relicId, out var p)) return p;
        if (relicId.Contains('.') &&
            _paths.TryGetValue(relicId[(relicId.IndexOf('.') + 1)..], out var p2)) return p2;
        return null;
    }

    /// <summary>
    /// <paramref name="relicsRoot"/>（= <c>relics</c> ディレクトリ）配下の画像相対パス（OS 区切り適用）。
    /// 画像が無いレリックは null。
    /// </summary>
    public static string? GetSourcePath(string relicsRoot, string relicId)
    {
        var rel = GetRelativePath(relicId);
        return rel is null ? null : Path.Combine(relicsRoot, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}
