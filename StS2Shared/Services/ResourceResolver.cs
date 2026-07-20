using System.Reflection;
using System.Text.RegularExpressions;

namespace StS2Shared.Services;

/// <summary>
/// バージョン別フォルダ（Resources/v{version}/）に埋め込まれた JSON リソースを
/// 「最新バージョン」で決定論的に解決するヘルパー。
///
/// 各サービスが従来使っていた <c>GetManifestResourceNames().FirstOrDefault(n =&gt; n.EndsWith("xxx.json"))</c>
/// は、(1) 複数バージョン（v0.106.1 / v0.107.0 …）が同名で埋め込まれた場合に列挙順依存になり、
/// (2) 同名のローカライズ埋め込み（StS2Shared.localization.eng.card_keywords.json 等）まで誤って一致しうる。
/// 本ヘルパーは <c>.Resources.v{version}.{fileName}</c> 形式のみを対象とし、最大バージョンを選ぶことで両問題を解消する。
/// </summary>
internal static class ResourceResolver
{
    /// <summary>
    /// 指定ファイル名のバージョン別リソースのうち、最新バージョンのリソース名を返す。
    /// 該当が無ければ null。
    /// </summary>
    public static string? ResolveVersioned(Assembly asm, string fileName)
    {
        // 例: "StS2Shared.Resources.v0._107._0.card_costs.json"
        // フォルダ "v0.107.0" は数値セグメントが識別子化されて "v0._107._0" になる。
        var rx = new Regex(@"\.Resources\.(v.+?)\." + Regex.Escape(fileName) + "$");
        return asm.GetManifestResourceNames()
            .Select(n => (name: n, m: rx.Match(n)))
            .Where(x => x.m.Success)
            .OrderByDescending(x => VersionKey(x.m.Groups[1].Value), StringComparer.Ordinal)
            .Select(x => x.name)
            .FirstOrDefault();
    }

    /// <summary>
    /// 埋め込まれたバージョン別リソースのうち、最大バージョンのフォルダ名（例 "v0.109.0"）。無ければ null。
    /// リソース名では数値セグメントが識別子化されて "v0._109._0" になるため、'_' を除去して復元する
    /// （バージョン文字列自体に '_' は現れないため単純除去で安全）。
    /// </summary>
    public static string? LatestEmbeddedVersion(Assembly asm)
    {
        // "…\.Resources\.{バージョン}\.{英字で始まるファイル名 or サブフォルダ}" のみを対象にする。
        var rx = new Regex(@"\.Resources\.(v[0-9_.]+?)\.(?=[A-Za-z])");
        return asm.GetManifestResourceNames()
            .Select(n => rx.Match(n))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value.Replace("_", ""))
            .OrderByDescending(VersionKey, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>最新バージョンのリソースストリームを直接開く。該当が無ければ null。</summary>
    public static Stream? OpenVersioned(Assembly asm, string fileName)
        => ResolveVersioned(asm, fileName) is { } name ? asm.GetManifestResourceStream(name) : null;

    /// <summary>
    /// ゲームテキスト系リソースを「埋め込み優先 → 外部（配布モードの抽出済みファイル）フォールバック」で開く。
    /// 開発ビルドでは埋め込みが存在するため現状どおり埋め込みを読む。配布ビルドで埋め込みを除外した場合のみ
    /// <see cref="AssetLocator.FindExtractedRoot"/> 配下の外部ファイルを読む。どちらも無ければ null。
    /// </summary>
    public static Stream? OpenText(Assembly asm, string fileName)
    {
        var embedded = OpenVersioned(asm, fileName);
        if (embedded is not null) return embedded;

        var root = AssetLocator.FindExtractedRoot();
        if (root is null) return null;

        var path = Path.Combine(root, ToExternalRelative(fileName));
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    // 埋め込みファイル名 → 抽出ルートからの相対パス。
    // "localization.eng.relics.json" → "localization/eng/relics.json"、"card_database.json" → "card_database.json"
    // （最後のドット以外を区切りに変換。ルート直下ファイルは内部ドットを持たないためそのまま）。
    static string ToExternalRelative(string fileName)
    {
        var parts = fileName.Split('.');
        return string.Join(Path.DirectorySeparatorChar, parts[..^1]) + "." + parts[^1];
    }

    // バージョン token（例 "v0._107._0"）内の整数列をゼロ埋め連結し、文字列比較で数値順になるキーを作る。
    // セグメント数に依存しない（"v0.107.0" → "00000000.00000107.00000000"）。
    static string VersionKey(string token) =>
        string.Join('.', Regex.Matches(token, @"\d+").Select(m => m.Value.PadLeft(8, '0')));
}
