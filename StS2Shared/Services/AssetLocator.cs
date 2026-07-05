using System.Text.RegularExpressions;

namespace StS2Shared.Services;

/// <summary>
/// ゲームアセット（<c>tools/extracted</c> 相当のレイアウト）を格納したルートディレクトリを解決する。
///
/// 従来は各サービスが個別に「exe の親を遡って <c>tools/extracted</c> を探す」ウォークアップを実装していた
/// （StS2Toys / StS2Capture.Core / StS2SiteBuilder / ctex-to-png に計 12 箇所分散）。本クラスはその解決を一元化し、
/// 2 つのモードを順に試す：
/// <list type="number">
///   <item><b>開発モード</b>: 起点（既定 <see cref="AppContext.BaseDirectory"/>）から親を遡って <c>tools/extracted</c> を探す。</item>
///   <item><b>配布モード</b>: <c>%LocalAppData%\StS2Toys\assets\v{version}</c> のうち最新バージョンを使う
///     （初回セットアップウィザードが抽出物を配置する先。レイアウトは <c>tools/extracted</c> と一致）。</item>
/// </list>
/// どちらも見つからない場合は「未セットアップ」を表す null を返す。
/// </summary>
public static class AssetLocator
{
    /// <summary>抽出アセットのルート直下にある固定サブディレクトリ名。</summary>
    const string ExtractedFolderName = "extracted";
    const string ToolsFolderName = "tools";
    const string ImagesFolderName = "images";
    const string GodotImportedRelative = ".godot/imported";

    /// <summary>配布モードのアセット格納ベース（<c>%LocalAppData%\StS2Toys\assets</c>）。バージョン別サブフォルダを持つ。</summary>
    public static string DistributionAssetsRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StS2Toys", "assets");

    /// <summary>
    /// 抽出アセットのルート（<c>tools/extracted</c> 相当）を返す。開発モード → 配布モードの順に解決し、
    /// どちらも無ければ null。<paramref name="startDir"/> はウォークアップの起点（既定は実行 exe のディレクトリ）。
    /// </summary>
    public static string? FindExtractedRoot(string? startDir = null)
        => FindByWalkUp(startDir) ?? LatestDistributionVersionDir;

    /// <summary>
    /// <see cref="FindExtractedRoot"/> と同じだが、見つからなければ案内文つきの例外を投げる（抽出物が必須のツール用）。
    /// </summary>
    public static string RequireExtractedRoot(string? startDir = null)
        => FindExtractedRoot(startDir) ?? throw new DirectoryNotFoundException(
            "アセットのルート（tools/extracted）が見つかりません。リポジトリルートからツールを実行するか、" +
            "配布モードでは初回セットアップを完了してください。");

    /// <summary>抽出ルート配下の <c>images/{subDir}</c> を返す。ルート未解決なら null。</summary>
    public static string? ImagesDir(string subDir, string? startDir = null)
    {
        var root = FindExtractedRoot(startDir);
        return root is null ? null : Path.Combine(root, ImagesFolderName, subDir);
    }

    /// <summary>抽出ルート配下の <c>.godot/imported</c>（Godot がインポート済みの .ctex 置き場）を返す。ルート未解決なら null。</summary>
    public static string? GodotImportedDir(string? startDir = null)
    {
        var root = FindExtractedRoot(startDir);
        return root is null
            ? null
            : Path.Combine(root, GodotImportedRelative.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>配布モードの最新バージョンディレクトリ（<c>%LocalAppData%\StS2Toys\assets\v{最大}</c>）。無ければ null。</summary>
    public static string? LatestDistributionVersionDir
    {
        get
        {
            if (!Directory.Exists(DistributionAssetsRoot)) return null;
            return Directory.GetDirectories(DistributionAssetsRoot, "v*")
                .OrderByDescending(d => VersionKey(Path.GetFileName(d)), StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }

    /// <summary>アセットが解決可能か（開発モードまたは配布モードのいずれかでルートが見つかるか）。</summary>
    public static bool IsConfigured => FindExtractedRoot() is not null;

    static string? FindByWalkUp(string? startDir)
    {
        var dir = new DirectoryInfo(startDir ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ToolsFolderName, ExtractedFolderName);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // バージョン名（例 "v0.107.1"）内の整数列をゼロ埋め連結し、文字列比較で数値順になるキーを作る。
    // ResourceResolver.VersionKey と同方針（セグメント数非依存）。
    static string VersionKey(string token) =>
        string.Join('.', Regex.Matches(token, @"\d+").Select(m => m.Value.PadLeft(8, '0')));
}
