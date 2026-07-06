using StS2Shared.Spine;

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
/// モンスター画像（<c>images/monsters/*.png</c>）は Spine スケルトンのレンダリング成果物のため単純抽出できない。
/// <see cref="ExtractMonsters"/> が <c>.pck</c> を直読みして先頭フレームを描画し、SiteBuilder と同じ
/// 生成パイプライン（StS2Shared.Spine）で <c>images/monsters/{id}.png</c> を生成する（GIF は非生成）。
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

        // 派生ゲームテキスト（card_database/card_descriptions/potion_database/monster_names）を
        // 抽出ルート直下に生成。配布ビルドで埋め込みを除外した際の外部解決先になる。
        LocTextDeriver.Derive(_pck, _outRoot, progress, ct);

        // モンスター画像を pck 直読みで Spine レンダリングして images/monsters/{id}.png へ生成。
        ExtractMonsters(progress, ct);
    }

    /// <summary>
    /// 各モンスターの先頭フレームを <c>.pck</c> 直読みで描画し <c>images/monsters/{id}.png</c> に書き出す。
    /// 個別モンスターの失敗はスキップして全体を止めない（キャンセルのみ伝播）。画像なし（Invisible）は書かない。
    /// </summary>
    void ExtractMonsters(IProgress<ExtractProgress>? progress, CancellationToken ct)
    {
        var src = new PckAssetSource(_pck);
        var ids = EnumerateMonsterIds();

        for (int i = 0; i < ids.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var png = MonsterPngRenderer.TryRenderFirstFramePng(src, ids[i]);
                if (png is not null)
                {
                    var outPath = ResolveOut("images/monsters/" + ids[i] + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    File.WriteAllBytes(outPath, png);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* 個別モンスターの解決/描画失敗はスキップ（? 表示に degrade。全体は継続） */ }

            progress?.Report(new ExtractProgress("monsters", i + 1, ids.Count));
        }
    }

    /// <summary>
    /// レンダリング対象モンスターの ID 一覧を <c>.pck</c> から直接列挙する
    /// （<c>scenes/creature_visuals/{id}.tscn</c> ∪ <c>animations/monsters/{id}/…</c>）。
    /// 外部解決される <see cref="MonsterDatabaseService"/> に依存しないため、抽出中（最終ディレクトリ未作成）でも
    /// 正しい一覧が得られる。<see cref="Spine.MonsterResolver"/> が実際に読む入力そのものと一致する。
    /// </summary>
    List<string> EnumerateMonsterIds()
    {
        var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        const string tscnPrefix = "scenes/creature_visuals/";
        foreach (var e in _pck.EnumerateUnder(tscnPrefix))
            if (e.ResPath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                ids.Add(e.ResPath[tscnPrefix.Length..^".tscn".Length]);

        const string animPrefix = "animations/monsters/";
        foreach (var e in _pck.EnumerateUnder(animPrefix))
        {
            var rest = e.ResPath[animPrefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash > 0) ids.Add(rest[..slash]);
        }

        return ids.ToList();
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
