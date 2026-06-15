using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

/// <summary>
/// カード ID → カード画像のソース相対パス（<c>card_portraits_png/</c> 基準、例 "silent/abrasive.png"）。
/// card-type-extractor が実ファイルをスキャンして生成した card_images.json（バージョンフォルダ）を参照する。
/// 「どのサブディレクトリにどのファイル名で存在するか」の対応をここに一元化し、
/// StS2SiteBuilder / StS2Toys 双方が利用する。
/// </summary>
public static class CardImageService
{
    /// <summary>画像ソースのベースディレクトリ名（<c>tools/extracted/images/</c> 配下）。</summary>
    public const string PortraitsDirName = "card_portraits_png";

    static readonly IReadOnlyDictionary<string, string> _paths = Load();

    static IReadOnlyDictionary<string, string> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "card_images.json");
        if (name is null) return result;

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    /// <summary>
    /// カード画像のソース相対パス（例 "silent/abrasive.png"・"event/mad_science_attack.png"）。
    /// <c>CARD.</c> 接頭辞の有無どちらの ID でも引ける。画像が無いカードは null。
    /// </summary>
    public static string? GetRelativePath(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;
        if (_paths.TryGetValue(cardId, out var p)) return p;
        if (!cardId.Contains('.') && _paths.TryGetValue("CARD." + cardId, out var p2)) return p2;
        return null;
    }

    /// <summary>
    /// <paramref name="portraitsRoot"/>（= <c>card_portraits_png</c> ディレクトリ）配下の画像絶対パス。
    /// 画像が無いカードは null。
    /// </summary>
    public static string? GetSourcePath(string portraitsRoot, string cardId)
    {
        var rel = GetRelativePath(cardId);
        return rel is null ? null : Path.Combine(portraitsRoot, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}
