namespace StS2Shared.Assets;

/// <summary>抽出の進捗（グループ名と、そのグループ内の完了数 / 総数）。</summary>
public sealed record ExtractProgress(string Group, int Done, int Total);

/// <summary>
/// <see cref="PckReader"/> と <see cref="CtexDecoder"/> を用い、StS2Toys が実行時に参照するアセットだけを
/// ユーザーの <c>.pck</c> から抽出し、出力ルート配下に <c>tools/extracted</c> と同一レイアウトで書き出す。
///
/// 出力先を <see cref="StS2Shared.Services.AssetLocator.DistributionAssetsRoot"/> 配下のバージョンフォルダに
/// すれば、配布モードのアプリがそのまま解決できる。進捗は <see cref="IProgress{T}"/>、キャンセルは
/// <see cref="CancellationToken"/> で受ける（初回セットアップウィザードから再利用する想定）。
///
/// 注: モンスター画像（<c>images/monsters/*.png</c>）は Spine アニメーションからのレンダリング成果物で
/// <c>.pck</c> から単純抽出できないため本セットには含めない（StS2Toys 側は画像なしでも動作する）。
/// これらは後続フェーズで Spine パイプラインを取り込む際に対応する。
/// </summary>
public sealed class AssetExtractor
{
    readonly PckReader _pck;
    readonly string _outRoot;

    public AssetExtractor(PckReader pck, string outputRoot)
    {
        _pck = pck;
        _outRoot = outputRoot;
    }

    /// <summary>StS2Toys ビューア用のアセット一式を抽出する。</summary>
    public void ExtractViewerAssets(IProgress<ExtractProgress>? progress = null, CancellationToken ct = default)
    {
        // カードポートレート: images/packed/card_portraits/{char}/{id}.png.import → card_portraits_png/{char}/{id}.png
        // （ctex-to-png に倣い beta サブフォルダは除外）
        ConvertPortraits("images/packed/card_portraits/", "images/card_portraits_png/",
            skipBetaSubdir: true, group: "card_portraits", progress, ct);

        // 個別レリック画像: images/relics/{rel}.png.import → relics_png/{rel}.png
        ConvertPortraits("images/relics/", "images/relics_png/",
            skipBetaSubdir: false, group: "relics", progress, ct);

        // カードアトラスのスプライト定義（.tres）をそのままコピー（CardAtlasService がリージョンを読む）
        CopyGlob("images/atlases/card_atlas.sprites/", suffix: null, group: "card_atlas_sprites", progress, ct);

        // カードアトラス本体 PNG: .godot/imported/card_atlas_N.png-*.ctex → images/atlases/card_atlas_N.png
        ConvertImportedCtex("card_atlas_", "images/atlases", group: "card_atlas_png", progress, ct);

        // レリックアトラス: ctex と tpsheet を生のままコピー（RelicImageService が実行時 BC7 デコード）
        CopyGlob(".godot/imported/relic_atlas.png-", suffix: ".ctex", group: "relic_atlas", progress, ct);
        CopyExact("images/atlases/relic_atlas.tpsheet", group: "relic_atlas", progress, ct);

        // エンチャントアイコン: .png.import と参照先 ctex を生のままコピー（EnchantmentIconService が実行時デコード）
        CopyImportsWithCtex("images/enchantments/", group: "enchantments", progress, ct);

        // ローカライズ JSON をそのままコピー（アプリが読むのは eng / jpn のみ）
        CopyGlob("localization/eng/", suffix: ".json", group: "localization", progress, ct);
        CopyGlob("localization/jpn/", suffix: ".json", group: "localization", progress, ct);
    }

    // ── 抽出ストラテジ ────────────────────────────────────────────────────────

    /// <summary>接頭辞配下の <c>.png.import</c> ごとに ctex を解決・デコードして PNG を書き出す。</summary>
    void ConvertPortraits(string pckPrefix, string outPrefix, bool skipBetaSubdir, string group,
        IProgress<ExtractProgress>? progress, CancellationToken ct)
    {
        var imports = _pck.EnumerateUnder(pckPrefix)
            .Where(e => e.ResPath.EndsWith(".png.import", StringComparison.OrdinalIgnoreCase))
            .Where(e => !skipBetaSubdir || !e.ResPath.Contains("/beta/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.ResPath, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < imports.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var e = imports[i];

            var importText = System.Text.Encoding.UTF8.GetString(_pck.Read(e.ResPath));
            var ctexRes = CtexDecoder.ParseImportCtexPathFromText(importText);
            if (ctexRes is not null && _pck.TryRead(ctexRes, out var ctexBytes))
            {
                // pckPrefix 以降の相対パスの ".png.import" を ".png" に置換して出力名にする。
                var rel = e.ResPath[pckPrefix.Length..];
                rel = rel[..^".import".Length]; // → "{...}.png"
                var outPath = ResolveOut(outPrefix + rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                CtexDecoder.ConvertToPng(ctexBytes, outPath);
            }

            progress?.Report(new ExtractProgress(group, i + 1, imports.Count));
        }
    }

    /// <summary>インポート済み ctex（<c>.godot/imported/{token}...-hash.ctex</c>）を PNG に変換して出力ディレクトリへ。</summary>
    void ConvertImportedCtex(string basenameToken, string outDir, string group,
        IProgress<ExtractProgress>? progress, CancellationToken ct)
    {
        var entries = _pck.EnumerateUnder(".godot/imported/")
            .Where(e => e.ResPath.EndsWith(".ctex", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileName(e.ResPath).StartsWith(basenameToken, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.ResPath, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var e = entries[i];
            // "card_atlas_0.png-<hash>.bptc.ctex" → "card_atlas_0.png"
            var outName = Path.GetFileName(e.ResPath).Split('-')[0];
            var outPath = ResolveOut(Path.Combine(outDir, outName));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            CtexDecoder.ConvertToPng(_pck.Read(e.ResPath), outPath);
            progress?.Report(new ExtractProgress(group, i + 1, entries.Count));
        }
    }

    /// <summary>接頭辞（任意で末尾一致）に合致する全エントリを、論理パスそのままの相対位置へコピーする。</summary>
    void CopyGlob(string prefix, string? suffix, string group,
        IProgress<ExtractProgress>? progress, CancellationToken ct)
    {
        var entries = _pck.EnumerateUnder(prefix)
            .Where(e => suffix is null || e.ResPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.ResPath, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            CopyVerbatim(entries[i].ResPath);
            progress?.Report(new ExtractProgress(group, i + 1, entries.Count));
        }
    }

    /// <summary>単一エントリを論理パスそのままの相対位置へコピーする。</summary>
    void CopyExact(string resPath, string group, IProgress<ExtractProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_pck.Find(resPath) is not null)
            CopyVerbatim(resPath);
        progress?.Report(new ExtractProgress(group, 1, 1));
    }

    /// <summary>接頭辞配下の <c>.png.import</c> と、その参照先 ctex を両方とも生のままコピーする。</summary>
    void CopyImportsWithCtex(string prefix, string group,
        IProgress<ExtractProgress>? progress, CancellationToken ct)
    {
        var imports = _pck.EnumerateUnder(prefix)
            .Where(e => e.ResPath.EndsWith(".png.import", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.ResPath, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < imports.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var e = imports[i];
            CopyVerbatim(e.ResPath);

            var importText = System.Text.Encoding.UTF8.GetString(_pck.Read(e.ResPath));
            var ctexRes = CtexDecoder.ParseImportCtexPathFromText(importText);
            if (ctexRes is not null && _pck.Find(ctexRes) is not null)
                CopyVerbatim(ctexRes);

            progress?.Report(new ExtractProgress(group, i + 1, imports.Count));
        }
    }

    // ── 出力ヘルパ ────────────────────────────────────────────────────────────

    void CopyVerbatim(string resPath)
    {
        var outPath = ResolveOut(resPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllBytes(outPath, _pck.Read(resPath));
    }

    string ResolveOut(string relPath) =>
        Path.Combine(_outRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
}
