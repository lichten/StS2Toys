namespace StS2Shared.Services;

/// <summary>
/// 表示用の「データバージョン」＝ 参照しているゲームデータがどのゲーム版由来かを返す（例 "v0.109.0"）。
/// アプリ自身のバージョンとは別物であることに注意（そちらは StS2Toys 側の AppVersion）。
///
/// 開発モードと配布モードで実データの出所が違うため、判定を分ける：
/// <list type="bullet">
///   <item><b>開発モード</b>: 実データは <c>tools/extracted</c> ＋ 埋め込み <c>Resources/v{version}/</c>。
///     <c>%LocalAppData%</c> 側に古い抽出物が残っていることがあり、それを出すと実態とずれるため
///     埋め込みリソースの最大バージョンを採る。</item>
///   <item><b>配布モード</b>: 初回セットアップが配置した
///     <c>%LocalAppData%\StS2Toys\assets\v{version}</c> が実データ。</item>
/// </list>
/// </summary>
public static class DataVersionService
{
    // 埋め込みリソースは不変なので一度だけ解決する。
    static readonly string? _embedded =
        ResourceResolver.LatestEmbeddedVersion(typeof(DataVersionService).Assembly);

    /// <summary>
    /// 現在のデータバージョン。解決できなければ null（配布モードで未セットアップの場合など）。
    /// セットアップウィザード完了で値が変わりうるため毎回解決する。
    /// ディレクトリ列挙を伴うので、起動時とウィザード後のみ呼ぶこと（再描画のたびに呼ばない）。
    /// </summary>
    public static string? Current =>
        AssetLocator.HasDevExtracted()
            ? _embedded
            : AssetLocator.InstalledDistributionVersion ?? _embedded;
}
