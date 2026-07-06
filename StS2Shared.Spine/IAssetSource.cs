namespace StS2Shared.Spine;

/// <summary>
/// Spine 生成が読むゲームアセットの供給源を抽象化する。パスはすべて <c>res://</c> を除いた
/// スラッシュ区切りの論理パス（例 "scenes/creature_visuals/foo.tscn"）で扱う。
///
/// これにより、開発（SiteBuilder）は展開済みディスク（<see cref="DiskAssetSource"/>）から、
/// 配布セットアップは <c>.pck</c> 直読み（StS2Shared.Assets 側の実装）から、同じ生成コードを使える。
/// </summary>
public interface IAssetSource
{
    /// <summary>指定の論理パスにファイルが存在するか。</summary>
    bool Exists(string resPath);

    /// <summary>指定の論理パスのファイル本体を読む。無ければ null。</summary>
    byte[]? Read(string resPath);

    /// <summary>
    /// 指定ディレクトリ（論理パスの接頭辞）直下のファイルを列挙する。<paramref name="suffix"/> 指定時は
    /// その末尾一致のみ。戻り値は各ファイルの論理パス（スラッシュ区切り）。
    /// </summary>
    IEnumerable<string> List(string resDir, string? suffix);
}

/// <summary>
/// 展開済みアセットルート（<c>tools/extracted</c> 相当）を起点にディスクから読む <see cref="IAssetSource"/>。
/// SiteBuilder のモンスター GIF 生成が従来どおりディスクから読むために使う。
/// </summary>
public sealed class DiskAssetSource : IAssetSource
{
    readonly string _root;

    public DiskAssetSource(string toolsRoot) => _root = toolsRoot;

    /// <summary>論理パスを実ファイルの絶対パスへ変換する（キャッシュの mtime 判定などディスク固有処理用）。</summary>
    public string FullPath(string resPath) =>
        Path.Combine(_root, resPath.Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string resPath) => File.Exists(FullPath(resPath));

    public byte[]? Read(string resPath)
    {
        var p = FullPath(resPath);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }

    public IEnumerable<string> List(string resDir, string? suffix)
    {
        var dir = FullPath(resDir);
        if (!Directory.Exists(dir)) return [];
        var prefix = resDir.TrimEnd('/');
        return Directory.GetFiles(dir)
            .Where(f => suffix is null || f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(f => prefix + "/" + Path.GetFileName(f));
    }

    /// <summary>論理パスの実ファイルの最終更新時刻（UTC）。存在しない/null なら <see cref="DateTime.MinValue"/>。</summary>
    public DateTime GetLastWriteTimeUtc(string? resPath)
    {
        if (resPath is null) return DateTime.MinValue;
        var p = FullPath(resPath);
        return File.Exists(p) ? File.GetLastWriteTimeUtc(p) : DateTime.MinValue;
    }
}
