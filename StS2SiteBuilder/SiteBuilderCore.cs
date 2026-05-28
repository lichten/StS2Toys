using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp.Formats.Png;
using StS2Shared.Services;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ISImage  = SixLabors.ImageSharp.Image;
using ISRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

public static class SiteBuilderCore
{
    // DescriptionFormatter が解決できなかった変数 [VarName] をスタイル付きスパンに変換するための正規表現
    static readonly Regex UnresolvedVarRegex = new(@"\[([A-Za-z][A-Za-z0-9]*)\]", RegexOptions.Compiled);

    public static string GetDistDir()
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        return Path.Combine(projectDir, "dist");
    }

    public static void Build(string distDir, Action<string> log)
    {
        Directory.CreateDirectory(distDir);

CharData[] chars =
[
    new("ironclad",    "Ironclad",    "アイアンクラッド", "#c0392b", "#fde8e8",
        "力と忍耐のキャラクター。StrengthとExhaustを活かした圧倒的な攻撃力が持ち味。"),
    new("silent",      "Silent",      "サイレント",       "#1a7a4a", "#e8f8f0",
        "素早さと策略のキャラクター。Poisonで敵を蝕みながら、Shivの連撃で圧倒する。"),
    new("defect",      "Defect",      "ディフェクト",     "#1a5799", "#e8f0fc",
        "論理と精密さのキャラクター。属性オーブのChannel・Evokeを駆使して戦う。"),
    new("necrobinder", "Necrobinder", "ネクロバインダー", "#6c3483", "#f4ecf7",
        "暗黒魔術と召喚のキャラクター。OstyとSoulを操り、Doomで敵を追い詰める。"),
    new("regent",      "Regent",      "リージェント",     "#7d6608", "#fdf8e8",
        "創造と王権のキャラクター。武器を鍛え、カードを生み出し、Starを消費して強力な効果を発動する。"),
];

var commonMecs = CharacterMechanics.All
    .FirstOrDefault(g => g.EnLabel == "Common")?.Mechanics
    .Select(m => (m.EnLabel, m.JaLabel, "common"))
    .ToArray() ?? [];
var mechanicsMap = CharacterMechanics.All
    .Where(g => g.Mechanics.Length > 0 && g.EnLabel != "Common")
    .ToDictionary(
        g => g.EnLabel,
        g => g.Mechanics.Select(m => (m.EnLabel, m.JaLabel, g.EnLabel.ToLowerInvariant()))
             .Concat(commonMecs).ToArray(),
        StringComparer.OrdinalIgnoreCase);

var allCardIds   = CardDatabaseService.GetAllCardIds().ToArray();
var allRelicIds  = CardDatabaseService.GetAllRelicIds().ToArray();
var allEventIds     = CardDatabaseService.GetAllEventIds().ToArray();
var allEncounterIds = EncounterDatabaseService.GetAllEncounterIds().ToArray();

// レリック画像を dist/images/relics/ に変換・コピー
var toolsRoot      = FindToolsRoot(Path.GetDirectoryName(distDir)!);
var relicImgDstDir = Path.Combine(distDir, "images", "relics");
Directory.CreateDirectory(relicImgDstDir);
var relicsWithImg  = toolsRoot is not null
    ? ConvertImages(toolsRoot, "relics", relicImgDstDir, allRelicIds, "レリック", log)
    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// イベント画像を dist/images/events/ に変換・コピー
var eventImgDstDir = Path.Combine(distDir, "images", "events");
Directory.CreateDirectory(eventImgDstDir);
var eventsWithImg  = toolsRoot is not null
    ? ConvertImages(toolsRoot, "events", eventImgDstDir, allEventIds, "イベント", log)
    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// モンスタースナップショットを dist/images/monsters/ にコピー
var monsterImgDstDir = Path.Combine(distDir, "images", "monsters");
Directory.CreateDirectory(monsterImgDstDir);
var monstersWithImg = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
if (toolsRoot is not null)
{
    var monsterSrcDir = Path.Combine(toolsRoot, "images", "monsters");
    if (Directory.Exists(monsterSrcDir))
    {
        foreach (var src in Directory.GetFiles(monsterSrcDir, "*.png"))
        {
            var fname = Path.GetFileName(src);
            var dst = Path.Combine(monsterImgDstDir, fname);
            if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst))
                File.Copy(src, dst, overwrite: true);
            monstersWithImg.Add(Path.GetFileNameWithoutExtension(fname));
        }
        log($"モンスター画像: {monstersWithImg.Count} 件コピー");
    }
    else
    {
        log("モンスター画像: tools/extracted/images/monsters/ が見つかりません（MonsterBrowserで生成してください）");
    }
}

// カードポートレートを dist/images/cards/ にコピー
var cardImgDstDir = Path.Combine(distDir, "images", "cards");
Directory.CreateDirectory(cardImgDstDir);
var cardsWithImg = toolsRoot is not null
    ? CopyCardImages(toolsRoot, cardImgDstDir, allCardIds, chars, log)
    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

PageEntry[] pages =
[
    new PageEntry("キャラクター", "characters.html", "Character List", "キャラクター一覧",
        "5人のキャラクターのカード・メカニクスを確認できます。", "#4a90d9"),
    new PageEntry("カード", "cards.html", "Card List", "カード一覧",
        "全カードをタイプ・レアリティ・フラグ付きで一覧表示。", "#2c3e50"),
    new PageEntry("レリック", "relics.html", "Relic List", "レリック一覧",
        $"全{allRelicIds.Length}件のレリックを一覧表示。", "#a0600c"),
    new PageEntry("イベント", "events.html", "Event List", "イベント一覧",
        $"全{allEventIds.Length}件のイベントをテキスト・選択肢付きで一覧表示。", "#1a6678"),
    new PageEntry("エンカウンター", "encounters.html", "Encounter List", "エンカウンター一覧",
        $"全{allEncounterIds.Length}件のエンカウンターをタイプ別に一覧表示。", "#8b2222"),
    new PageEntry("メカニクス", "mechanics.html", "Mechanic List", "メカニクス一覧",
        $"全{CharacterMechanics.All.Sum(g => g.Mechanics.Length)}件のメカニクスをキャラクター別に一覧表示。", "#4a5568"),
];

// favicon を assets/ から dist/ にコピー
var faviconSrc = Path.Combine(Path.GetDirectoryName(distDir)!, "assets", "favicon.png");
var faviconDst = Path.Combine(distDir, "favicon.png");
if (File.Exists(faviconSrc)) File.Copy(faviconSrc, faviconDst, overwrite: true);

chars = chars.Select(ch =>
{
    var saved = ExtractReview(Path.Combine(distDir, $"{ch.Id}.html"));
    return string.IsNullOrEmpty(saved) ? ch : ch with { Desc = saved };
}).ToArray();

File.WriteAllText(Path.Combine(distDir, "index.html"),      BuildIndex(chars),         System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "characters.html"), BuildCharListPage(chars),  System.Text.Encoding.UTF8);
var aboutPath     = Path.Combine(distDir, "about.html");
File.WriteAllText(aboutPath,
    BuildAboutPage(chars,
        ExtractMarker(aboutPath, "REVIEW"),
        ExtractMarker(aboutPath, "BIO")),
    System.Text.Encoding.UTF8);
var changelogPath = Path.Combine(distDir, "changelog.html");
File.WriteAllText(changelogPath, BuildChangelogPage(chars, ExtractReview(changelogPath)), System.Text.Encoding.UTF8);
var changelogDates = ParseChangelogDates(changelogPath);
File.WriteAllText(Path.Combine(distDir, "pages.html"),
    BuildPageList(pages, chars, allCardIds, allRelicIds, allEventIds, allEncounterIds),
    System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "cards.html"),  BuildCardListPage(allCardIds, chars),   System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "relics.html"), BuildRelicListPage(allRelicIds, chars), System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "events.html"),     BuildEventListPage(allEventIds, chars, eventsWithImg), System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "encounters.html"), BuildEncounterListPage(allEncounterIds, chars),        System.Text.Encoding.UTF8);
foreach (var ch in chars)
{
    mechanicsMap.TryGetValue(ch.EnName, out var mecs);
    File.WriteAllText(Path.Combine(distDir, $"{ch.Id}.html"),
        BuildCharPage(ch, chars, mecs ?? []), System.Text.Encoding.UTF8);
}

// 個別カードページ（dist/cards/{charDir}/{CARD_ID}.html）
foreach (var cardId in allCardIds)
{
    var dir     = GetCardDir(cardId, chars);
    var cardDir = Path.Combine(distDir, "cards", dir);
    Directory.CreateDirectory(cardDir);
    var outPath     = Path.Combine(cardDir, $"{RawId(cardId)}.html");
    var review      = ExtractReview(outPath);
    var relPath     = Path.GetRelativePath(distDir, outPath).Replace('\\', '/');
    var lastUpdated = changelogDates.GetValueOrDefault(relPath, "");
    File.WriteAllText(outPath,
        BuildCardPage(cardId, chars, basePath: "../../",
            hasImage: cardsWithImg.Contains(cardId), review: review, lastUpdated: lastUpdated),
        System.Text.Encoding.UTF8);
}

// 個別レリックページ（dist/relics/{RELIC_ID}.html）
var relicOutDir = Path.Combine(distDir, "relics");
Directory.CreateDirectory(relicOutDir);
foreach (var relicId in allRelicIds)
{
    var outPath     = Path.Combine(relicOutDir, $"{relicId}.html");
    var review      = ExtractReview(outPath);
    var relPath     = Path.GetRelativePath(distDir, outPath).Replace('\\', '/');
    var lastUpdated = changelogDates.GetValueOrDefault(relPath, "");
    File.WriteAllText(outPath,
        BuildRelicPage(relicId, chars, relicsWithImg.Contains(relicId), review: review, lastUpdated: lastUpdated),
        System.Text.Encoding.UTF8);
}

// 個別イベントページ（dist/events/{EVENT_ID}.html）
var eventOutDir = Path.Combine(distDir, "events");
Directory.CreateDirectory(eventOutDir);
foreach (var eventId in allEventIds)
{
    var outPath     = Path.Combine(eventOutDir, $"{eventId}.html");
    var review      = ExtractReview(outPath);
    var relPath     = Path.GetRelativePath(distDir, outPath).Replace('\\', '/');
    var lastUpdated = changelogDates.GetValueOrDefault(relPath, "");
    File.WriteAllText(outPath,
        BuildEventPage(eventId, chars, eventsWithImg.Contains(eventId), review: review, lastUpdated: lastUpdated),
        System.Text.Encoding.UTF8);
}

// 個別エンカウンターページ（dist/encounters/{ENCOUNTER_ID}.html）
var encounterOutDir = Path.Combine(distDir, "encounters");
Directory.CreateDirectory(encounterOutDir);
foreach (var encId in allEncounterIds)
{
    var outPath     = Path.Combine(encounterOutDir, $"{encId}.html");
    var review      = ExtractReview(outPath);
    var relPath     = Path.GetRelativePath(distDir, outPath).Replace('\\', '/');
    var lastUpdated = changelogDates.GetValueOrDefault(relPath, "");
    File.WriteAllText(outPath,
        BuildEncounterPage(encId, chars, monstersWithImg, review: review, lastUpdated: lastUpdated),
        System.Text.Encoding.UTF8);
}

// 個別メカニクスページ（dist/mechanics/{groupDir}/{MecFileName}.html）
var mecOutDir = Path.Combine(distDir, "mechanics");
Directory.CreateDirectory(mecOutDir);
File.WriteAllText(Path.Combine(distDir, "mechanics.html"),
    BuildMechanicListPage(chars), System.Text.Encoding.UTF8);
foreach (var group in CharacterMechanics.All.Where(g => g.Mechanics.Length > 0))
{
    var groupDir = Path.Combine(mecOutDir, group.EnLabel.ToLowerInvariant());
    Directory.CreateDirectory(groupDir);
    foreach (var mec in group.Mechanics)
    {
        var outPath     = Path.Combine(groupDir, MecFileName(mec.EnLabel));
        var review      = ExtractReview(outPath);
        var relPath     = Path.GetRelativePath(distDir, outPath).Replace('\\', '/');
        var lastUpdated = changelogDates.GetValueOrDefault(relPath, "");
        File.WriteAllText(outPath,
            BuildMechanicPage(group, mec, allCardIds, chars, review: review, lastUpdated: lastUpdated),
            System.Text.Encoding.UTF8);
    }
}

var totalMecs = CharacterMechanics.All.Sum(g => g.Mechanics.Length);

// 既存のランページを最新コードで再生成
var runOutDir    = Path.Combine(distDir, "run");
var historyDir   = RunHistoryService.GetHistoryDir();
var runCount     = 0;
if (!string.IsNullOrEmpty(historyDir) && Directory.Exists(historyDir) && Directory.Exists(runOutDir))
{
    var (runLinkMap, runLinkRegex, runEventIds) = BuildAutoLinkMap(chars);
    foreach (var summary in RunHistoryService.LoadSummaries(historyDir))
    {
        var htmlPath = Path.Combine(runOutDir, $"{summary.StartTime}.html");
        if (!File.Exists(htmlPath)) continue;
        var run = RunHistoryService.Load(summary.FilePath);
        WriteRunPageCore(run, chars, distDir, runLinkMap, runLinkRegex, runEventIds);
        runCount++;
    }
    if (runCount > 0)
    {
        var listHtml = BuildRunListPage(chars, runOutDir);
        File.WriteAllText(Path.Combine(distDir, "runs.html"), listHtml, System.Text.Encoding.UTF8);
        log($"ラン再生成: {runCount} 件");
    }
}

// 記事ページ (articles/)
var articlesSourceDir = FindArticlesDir(distDir);
var articleMetas = articlesSourceDir is not null
    ? Directory.GetFiles(articlesSourceDir, "*.html")
               .Select(ParseArticle)
               .OrderByDescending(a => a.Date)
               .ToArray()
    : [];
Directory.CreateDirectory(Path.Combine(distDir, "articles"));
foreach (var article in articleMetas)
    File.WriteAllText(Path.Combine(distDir, "articles", $"{article.Slug}.html"),
        BuildArticlePage(article, chars), System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "articles.html"),
    BuildArticleListPage(articleMetas, chars), System.Text.Encoding.UTF8);

        log($"Generated {9 + chars.Length + allCardIds.Length + allRelicIds.Length + allEventIds.Length + allEncounterIds.Length + 1 + totalMecs + runCount + 1 + articleMetas.Length} files -> {distDir}");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    static string RawId(string id) => id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;

static string GetCardDir(string cardId, CharData[] chars)
{
    var charName = CardDatabaseService.GetCardCharacter(cardId);
    var ch = chars.FirstOrDefault(c => c.EnName.Equals(charName, StringComparison.OrdinalIgnoreCase));
    return ch?.Id ?? "shared";
}

// ── page builders ─────────────────────────────────────────────────────────────

static string BuildAboutPage(CharData[] chars, string review = "", string bioReview = "")
{
    const string OVERVIEW_DEFAULT = """

        <script type="text/markdown">
        このサイトは **Slay the Spire 2** のカード・レリック・イベント・エンカウンター・メカニクスをまとめた個人用リファレンスです。

        ゲームデータをもとに静的 HTML として生成されており、英語・日本語の両名称および効果テキストを掲載しています。
        </script>

        """;

    const string BIO_DEFAULT = """

        <!-- プロフィールテキストをここに入力してください -->
        <!-- このコメントを削除して任意のHTMLを記述できます  -->

        """;

    const string AUTHOR_NAME = "（表示名）";

    var overviewContent = review    == "" ? OVERVIEW_DEFAULT : review;
    var bioContent      = bioReview == "" ? BIO_DEFAULT      : bioReview;

    const string AUTHOR_CSS = """
        <style>
        .author-card { display: flex; gap: 16px; align-items: flex-start; }
        .author-avatar { width: 72px; height: 72px; border-radius: 50%; flex-shrink: 0;
                         object-fit: cover; border: 2px solid #e0e0e0; }
        .author-body { display: flex; flex-direction: column; gap: 4px; }
        .author-name { font-size: 16px; font-weight: 700; }
        .author-handle { font-size: 13px; color: #1d9bf0; text-decoration: none; }
        .author-handle:hover { text-decoration: underline; }
        .author-bio { font-size: 13px; color: #555; margin-top: 6px; }
        </style>
        """;

    return Layout("このサイトについて", "about", "#4a90d9", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">このサイトについて</h1>
          <p class="hero-sub">Slay the Spire 2 攻略メモメモ</p>
        </div>
        <section class="section">
          <h2 class="section-title">概要</h2>
          <!-- REVIEW_START -->{overviewContent}<!-- REVIEW_END -->
        </section>
        <section class="section">
          <h2 class="section-title">収録コンテンツ</h2>
          <table class="card-table" style="max-width:480px">
            <tbody>
              <tr><td class="col-name">カード一覧</td><td>全キャラクターのカードをタイプ・レアリティ別に掲載</td></tr>
              <tr><td class="col-name">レリック一覧</td><td>全レリックの効果テキストを掲載</td></tr>
              <tr><td class="col-name">イベント一覧</td><td>全イベントの本文・選択肢を掲載</td></tr>
              <tr><td class="col-name">エンカウンター一覧</td><td>全エンカウンターの登場モンスターを掲載</td></tr>
              <tr><td class="col-name">メカニクス一覧</td><td>キャラクター固有および共通メカニクスを掲載</td></tr>
            </tbody>
          </table>
        </section>
        <section class="section">
          <h2 class="section-title">注意事項</h2>
          <p class="desc-main">
            本サイトはゲームのアーリーアクセス版データをもとに生成されており、内容はゲームのアップデートにより変更される場合があります。
            公式情報については <a href="https://store.steampowered.com/app/646570/Slay_the_Spire_2/" class="wiki-link" target="_blank" rel="noopener">Steam ストアページ</a> をご確認ください。
          </p>
        </section>
        <section class="section">
          <h2 class="section-title">作者</h2>
          <div class="author-card">
            <img src="images/author/avatar.jpg" class="author-avatar" alt="@Plover2064"
                 onerror="this.style.display='none'">
            <div class="author-body">
              <div class="author-name">{AUTHOR_NAME}</div>
              <a href="https://x.com/Plover2064" target="_blank" rel="noopener noreferrer"
                 class="author-handle">@Plover2064</a>
              <div class="author-bio"><!-- BIO_START -->{bioContent}<!-- BIO_END --></div>
            </div>
          </div>
        </section>
        """, extraHead: AUTHOR_CSS);
}

// ── ラン履歴 ─────────────────────────────────────────────────────────────────

static string NodeBadge(string type)
{
    var (label, cls) = type switch
    {
        "monster"   => ("通常", "node-monster"),
        "elite"     => ("エリート", "node-elite"),
        "boss"      => ("ボス", "node-boss"),
        "shop"      => ("ショップ", "node-shop"),
        "treasure"  => ("宝箱", "node-treasure"),
        "rest_site" => ("焚き火", "node-rest_site"),
        "ancient"   => ("Ancient", "node-ancient"),
        _           => ("？", "node-unknown"),
    };
    return $"""<span class="node-badge {cls}">{label}</span>""";
}

static string BuildRunPage(RunHistoryData run, CharData[] chars,
    IReadOnlyDictionary<string, string> autoLinkMap, Regex autoLinkRegex,
    HashSet<string> eventIds, string review = "")
{
    var player  = run.Players.Count > 0 ? run.Players[0] : new RunPlayerData();
    var charId  = RawId(player.Character).ToLowerInvariant();
    var ch      = chars.FirstOrDefault(c => c.Id == charId);
    var accent  = ch?.Accent  ?? "#4a5568";
    var lightBg = ch?.LightBg ?? "#f5f6f8";
    var charJa  = ch?.JaName  ?? charId;
    var charEn  = ch?.EnName  ?? charId;

    var resultBadge = run.Win
        ? """<span class="run-win">勝利</span>"""
        : run.WasAbandoned
          ? """<span class="run-abandon">離脱</span>"""
          : """<span class="run-lose">敗北</span>""";

    var startDt   = DateTimeOffset.FromUnixTimeSeconds(run.StartTime).LocalDateTime;
    var dateStr   = startDt.ToString("yyyy-MM-dd HH:mm");
    var durMin    = run.RunTime / 60;
    var durSec    = run.RunTime % 60;
    var durStr    = $"{durMin}:{durSec:D2}";
    var totalFloors = run.MapPointHistory.Sum(a => a.Count);

    // death info
    var killedBy = run.KilledByEncounter is { Length: > 0 } kb && kb != "NONE.NONE"
        ? EncounterDatabaseService.GetEncounterName(kb, japanese: true)
        : "";

    // ── サマリー ──
    var lastPs = run.MapPointHistory.SelectMany(a => a)
        .LastOrDefault()?.PlayerStats?.FirstOrDefault();
    var finalHp   = lastPs is not null ? $"{lastPs.CurrentHp}/{lastPs.MaxHp}" : "—";
    var finalGold = lastPs is not null ? lastPs.CurrentGold.ToString() : "—";

    var summaryHtml = $"""
        <div class="run-summary-grid">
          <div class="run-stat"><div class="run-stat-val">{finalHp}</div><div class="run-stat-label">最終HP</div></div>
          <div class="run-stat"><div class="run-stat-val">{finalGold}</div><div class="run-stat-label">最終Gold</div></div>
          <div class="run-stat"><div class="run-stat-val">{totalFloors}</div><div class="run-stat-label">総フロア</div></div>
          <div class="run-stat"><div class="run-stat-val">{player.Deck.Count}</div><div class="run-stat-label">デッキ枚数</div></div>
          <div class="run-stat"><div class="run-stat-val">{player.Relics.Count}</div><div class="run-stat-label">レリック数</div></div>
        </div>
        """;

    // ── フロアタイムライン ──
    var timelineHtml = new System.Text.StringBuilder();
    for (int ai = 0; ai < run.MapPointHistory.Count; ai++)
    {
        var actId = ai < run.Acts.Count ? run.Acts[ai] : $"ACT {ai + 1}";
        var actName = EncounterDatabaseService.GetActName(actId, japanese: true);
        timelineHtml.AppendLine($"""<section class="section"><h2 class="section-title">{actName}</h2>""");

        var act = run.MapPointHistory[ai];
        for (int fi = 0; fi < act.Count; fi++)
        {
            var mp = act[fi];
            var ps = mp.PlayerStats?.FirstOrDefault();

            var badge = NodeBadge(mp.MapPointType);
            var room = mp.Rooms?.FirstOrDefault();
            string encLink = "";
            if (room?.ModelId is { Length: > 0 } mid)
            {
                var rawId = RawId(mid);
                if (eventIds.Contains(rawId))
                {
                    var name = CardDatabaseService.GetEventTitle(rawId, japanese: true);
                    encLink = $"""<a class="auto-link" href="../events/{rawId}.html">{name}</a>""";
                }
                else
                {
                    var name = EncounterDatabaseService.GetEncounterName(mid, japanese: true);
                    encLink = $"""<a class="auto-link" href="../encounters/{rawId}.html">{name}</a>""";
                }
            }
            var turns = room?.TurnsTaken > 0
                ? $"""<span style="color:#aaa;font-size:11px">{room.TurnsTaken}T</span>"""
                : "";

            var hpStr = ps is not null
                ? $"""<span class="floor-hp">HP {ps.CurrentHp}/{ps.MaxHp}"""
                  + (ps.DamageTaken > 0 ? $""" <span style="color:#c00">-{ps.DamageTaken}</span>""" : "")
                  + (ps.HpHealed > 0    ? $""" <span style="color:#2a6">+{ps.HpHealed}</span>""" : "")
                  + "</span>"
                : "";

            // カード選択
            var cardPickHtml = "";
            if (ps?.CardChoices is { Count: > 0 } choices)
            {
                var parts = choices.Select(c =>
                {
                    var cid = c.Card?.Id ?? "";
                    var cname = cid.Length > 0 ? CardDatabaseService.GetName(cid, japanese: true) : "?";
                    return c.WasPicked
                        ? $"""<span class="floor-picked">▶{cname}</span>"""
                        : $"""<span class="floor-skipped">{cname}</span>""";
                });
                cardPickHtml = string.Join(" ", parts);
            }
            else if (ps?.CardsGained is { Count: > 0 } gained)
            {
                var parts = gained.Select(c =>
                    $"""<span class="floor-picked">▶{CardDatabaseService.GetName(c.Id, japanese: true)}</span>""");
                cardPickHtml = string.Join(" ", parts);
            }

            // カード除外
            var cardRemoveHtml = "";
            if (ps?.CardsRemoved is { Count: > 0 } removed)
            {
                var parts = removed.Select(c =>
                    $"""<span style="color:#888;font-size:12px">✕{CardDatabaseService.GetName(c.Id, japanese: true)}</span>""");
                cardRemoveHtml = " " + string.Join(" ", parts);
            }

            // レリック取得
            var relicHtml = "";
            if (ps?.RelicChoices is { Count: > 0 } relics)
            {
                var picked = relics.Where(r => r.WasPicked)
                    .Select(r => CardDatabaseService.GetRelicTitle(RawId(r.Choice), japanese: true));
                if (picked.Any())
                    relicHtml = $""" <span style="color:#a0600c">🔮{string.Join(", ", picked)}</span>""";
            }
            else if (ps?.BoughtRelics is { Count: > 0 } bought)
            {
                var names = bought.Select(r => CardDatabaseService.GetRelicTitle(RawId(r), japanese: true));
                relicHtml = $""" <span style="color:#a0600c">🛒{string.Join(", ", names)}</span>""";
            }
            else if (ps?.AncientChoice is { Count: > 0 } ancient)
            {
                var picked2 = ancient.Where(a => a.WasChosen)
                    .Select(a => CardDatabaseService.GetRelicTitle(a.TextKey, japanese: true));
                if (picked2.Any())
                    relicHtml = $""" <span style="color:#a0600c">🔮{string.Join(", ", picked2)}</span>""";
            }

            // 焚き火
            var restHtml = "";
            if (ps?.RestSiteChoices is { Count: > 0 } rest)
                restHtml = $""" <span style="color:#166;font-size:12px">[{string.Join("/", rest)}]</span>""";

            timelineHtml.AppendLine($"""
              <div class="floor-row">{badge}{encLink}{turns} {cardPickHtml}{cardRemoveHtml}{relicHtml}{restHtml} {hpStr}</div>
              """);
        }
        timelineHtml.AppendLine("</section>");
    }

    // ── 最終デッキ ──
    var deckTags = player.Deck
        .GroupBy(c => (c.Id, c.CurrentUpgradeLevel, c.FloorAddedToDeck))
        .OrderBy(g => g.Key.FloorAddedToDeck)
        .ThenBy(g => g.Key.Id)
        .Select(g =>
        {
            var c          = g.First();
            var count      = g.Count();
            var name       = CardDatabaseService.GetName(c.Id, japanese: true);
            var upg        = c.CurrentUpgradeLevel > 0
                ? $"""<span style="color:#1a5799;font-size:11px"> +{c.CurrentUpgradeLevel}</span>"""
                : "";
            var floor      = c.FloorAddedToDeck > 0
                ? $"""<span style="color:#aaa;font-size:11px"> F{c.FloorAddedToDeck}</span>"""
                : "";
            var countBadge = count > 1
                ? $"""<span style="color:#888;font-size:11px"> ×{count}</span>"""
                : "";
            var cardDir    = GetCardDir(c.Id, chars);
            var href       = $"../cards/{cardDir}/{RawId(c.Id)}.html";
            return $"""<a class="mec-tag" href="{href}">{name}{upg}{floor}{countBadge}</a>""";
        });
    var deckHtml = $"""
        <section class="section">
          <h2 class="section-title">最終デッキ <span class="section-meta">{player.Deck.Count}枚</span></h2>
          <div class="mec-tags">{string.Join(" ", deckTags)}</div>
        </section>
        """;

    // ── 最終レリック ──
    var relicItems = player.Relics.Select(r =>
    {
        var bareId = RawId(r.Id);
        var name   = CardDatabaseService.GetRelicTitle(bareId, japanese: true);
        var floor  = r.FloorAddedToDeck > 0 ? $"""<span style="color:#aaa;font-size:11px"> F{r.FloorAddedToDeck}</span>""" : "";
        var href   = $"../relics/{bareId}.html";
        return $"""<a class="mec-tag" href="{href}">{name}{floor}</a>""";
    });
    var relicsHtml = $"""
        <section class="section">
          <h2 class="section-title">最終レリック <span class="section-meta">{player.Relics.Count}個</span></h2>
          <div class="mec-tags">{string.Join(" ", relicItems)}</div>
        </section>
        """;

    // ── 死因 ──
    var killedSection = killedBy != ""
        ? $"""<section class="section"><h2 class="section-title">最後の戦闘</h2><p class="desc-main">{killedBy}</p></section>"""
        : "";

    // ── 感想・メモ（REVIEW） ──
    const string REVIEW_GUIDE = """
        <!-- REVIEW_START -->
        <!--
          【感想・メモ】
          このコメントブロック全体を削除し、かわりにHTMLを書いてください。
        -->
        <!-- REVIEW_END -->
        """;
    var strippedReview = review == "" ? "" : StripAutoLinks(review, "../");
    var linkedReview = strippedReview == "" ? "" : AutoLinkHtml(strippedReview, autoLinkRegex, autoLinkMap, "../");
    var reviewZone = linkedReview == ""
        ? REVIEW_GUIDE
        : $"<!-- REVIEW_START -->{linkedReview}<!-- REVIEW_END -->";

    var content = $"""
        <div class="card-detail-header" style="border-left:5px solid {accent};background:{lightBg}">
          <div class="card-breadcrumb">
            <a href="../runs.html" class="char-back-link" style="color:{accent}">ラン一覧</a>
          </div>
          <h1 class="card-title-en" style="color:{accent}">{charJa} {resultBadge}</h1>
          <div class="card-title-ja">{charEn}</div>
          <div class="card-badges">
            <span class="badge" style="background:#eee;color:#555">A{run.Ascension}</span>
            <span class="badge" style="background:#eee;color:#555">{dateStr}</span>
            <span class="badge" style="background:#eee;color:#555">⏱ {durStr}</span>
            <span class="badge" style="background:#eee;color:#555">Seed: {run.Seed}</span>
            {(run.BuildId is { Length: > 0 } bid ? $"""<span class="badge" style="background:#eee;color:#555">v{bid}</span>""" : "")}
          </div>
          <!-- LAST_UPDATED_START --><!-- LAST_UPDATED_END -->
        </div>
        {reviewZone}
        <section class="section">
          <h2 class="section-title">サマリー</h2>
          {summaryHtml}
        </section>
        {deckHtml}
        {relicsHtml}
        {killedSection}
        {timelineHtml}
        """;

    var title = $"{charJa} A{run.Ascension} {(run.Win ? "勝利" : run.WasAbandoned ? "離脱" : "敗北")} {startDt:yyyy-MM-dd}";
    return Layout(title, "runs", accent, chars, content, basePath: "../", wikiLink: false);
}

// GUI からの呼び出し用（map を内部で構築して runs.html も更新）
internal static string WriteRunPage(RunHistoryData run, CharData[] chars, string distDir)
{
    var (linkMap, linkRegex, eventIds) = BuildAutoLinkMap(chars);
    var outPath = WriteRunPageCore(run, chars, distDir, linkMap, linkRegex, eventIds);

    var runDir   = Path.Combine(distDir, "run");
    var listHtml = BuildRunListPage(chars, runDir);
    File.WriteAllText(Path.Combine(distDir, "runs.html"), listHtml, System.Text.Encoding.UTF8);

    return outPath;
}

// Build() からの呼び出し用（map を外部から受け取る）
static string WriteRunPageCore(RunHistoryData run, CharData[] chars, string distDir,
    IReadOnlyDictionary<string, string> linkMap, Regex linkRegex, HashSet<string> eventIds)
{
    var runDir = Path.Combine(distDir, "run");
    Directory.CreateDirectory(runDir);

    var outPath        = Path.Combine(runDir, $"{run.StartTime}.html");
    var existingReview = ExtractReview(outPath);
    var html           = BuildRunPage(run, chars, linkMap, linkRegex, eventIds, existingReview);
    var metaJson = System.Text.Json.JsonSerializer.Serialize(new {
        win       = run.Win,
        abandoned = run.WasAbandoned,
        @char     = run.Players.Count > 0 ? RunHistoryService.StripPrefix(run.Players[0].Character).ToLowerInvariant() : "",
        ascension = run.Ascension,
        startTime = run.StartTime,
        runTime   = run.RunTime,
    });
    File.WriteAllText(outPath, $"<!-- RUN_META: {metaJson} -->\n{html}", System.Text.Encoding.UTF8);
    return outPath;
}

static string BuildRunListPage(CharData[] chars, string runDir)
{
    if (!Directory.Exists(runDir))
        return Layout("ラン一覧", "runs", "#4a90d9", chars, """
            <div class="page-hero"><h1 class="hero-title">ラン一覧</h1></div>
            <section class="section"><p class="placeholder">ランがまだ生成されていません。</p></section>
            """);

    // RUN_META コメントから軽量に情報を読む
    var metaPattern = new System.Text.RegularExpressions.Regex(
        @"<!-- RUN_META: (\{.+?\}) -->");

    var runs = new List<(long StartTime, string CharId, bool Win, bool Abandoned, int Ascension, int RunTime, string FileName)>();
    foreach (var file in Directory.EnumerateFiles(runDir, "*.html"))
    {
        try
        {
            var first = File.ReadLines(file).Take(3).FirstOrDefault() ?? "";
            var m = metaPattern.Match(first);
            if (!m.Success) continue;
            var meta = System.Text.Json.JsonSerializer.Deserialize<RunMeta>(
                m.Groups[1].Value, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (meta is null) continue;
            runs.Add((meta.StartTime, meta.Char, meta.Win, meta.Abandoned,
                      meta.Ascension, meta.RunTime, Path.GetFileName(file)));
        }
        catch { }
    }
    runs.Sort((a, b) => b.StartTime.CompareTo(a.StartTime));

    string Rows()
    {
        if (runs.Count == 0)
            return """<tr><td colspan="6" style="text-align:center;color:#aaa">まだラン詳細が生成されていません</td></tr>""";
        return string.Concat(runs.Select(r =>
        {
            var ch     = chars.FirstOrDefault(c => c.Id == r.CharId);
            var charJa = ch?.JaName ?? r.CharId;
            var dt     = DateTimeOffset.FromUnixTimeSeconds(r.StartTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
            var result = r.Win ? """<span class="run-win">勝利</span>"""
                       : r.Abandoned ? """<span class="run-abandon">離脱</span>"""
                       : """<span class="run-lose">敗北</span>""";
            var dur    = $"{r.RunTime / 60}:{r.RunTime % 60:D2}";
            return $"""
                <tr>
                  <td>{dt}</td>
                  <td>{charJa}</td>
                  <td>{result}</td>
                  <td>A{r.Ascension}</td>
                  <td>{dur}</td>
                  <td><a href="run/{r.FileName}">詳細</a></td>
                </tr>
                """;
        }));
    }

    var content = $"""
        <div class="page-hero">
          <h1 class="hero-title">ラン一覧</h1>
          <p class="hero-sub">Run History</p>
        </div>
        <section class="section">
          <table class="card-table">
            <thead><tr>
              <th>日付</th><th>キャラ</th><th>結果</th><th>A#</th><th>時間</th><th></th>
            </tr></thead>
            <tbody>{Rows()}</tbody>
          </table>
        </section>
        """;

    return Layout("ラン一覧", "runs", "#4a90d9", chars, content, basePath: "", wikiLink: false);
}

// RUN_META コメントのデシリアライズ用
record RunMeta(
    [property: System.Text.Json.Serialization.JsonPropertyName("win")]       bool Win,
    [property: System.Text.Json.Serialization.JsonPropertyName("abandoned")] bool Abandoned,
    [property: System.Text.Json.Serialization.JsonPropertyName("char")]      string Char,
    [property: System.Text.Json.Serialization.JsonPropertyName("ascension")] int Ascension,
    [property: System.Text.Json.Serialization.JsonPropertyName("startTime")] long StartTime,
    [property: System.Text.Json.Serialization.JsonPropertyName("runTime")]   int RunTime
);

static string BuildChangelogPage(CharData[] chars, string review = "") =>
    Layout("更新履歴", "changelog", "#4a90d9", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">更新履歴</h1>
        </div>
        <section class="section">
          <!-- REVIEW_START -->{review}<!-- REVIEW_END -->
        </section>
        """);

static string BuildIndex(CharData[] chars)
{
    var cards = string.Concat(chars.Select(ch => $"""
              <a href="{ch.Id}.html" class="char-card">
                <div class="char-card-header" style="background:{ch.Accent}">
                  <div class="char-name-en">{ch.EnName}</div>
                  <div class="char-name-ja">{ch.JaName}</div>
                </div>
                <div class="char-card-body">
                  <p class="char-desc">{ch.Desc}</p>
                </div>
                <div class="char-card-footer">カードを見る &rarr;</div>
              </a>
        """));

    return Layout("トップ", "index", "#4a90d9", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">Slay the Spire 2</h1>
          <p class="hero-sub">攻略メモメモ</p>
          <p class="hero-desc">5人のキャラクターのカード・メカニクスを確認できます。</p>
        </div>
        <div class="char-grid">
          {cards}
        </div>
        """);
}

static string BuildCharListPage(CharData[] chars)
{
    var cards = string.Concat(chars.Select(ch => $"""
              <a href="{ch.Id}.html" class="char-card">
                <div class="char-card-header" style="background:{ch.Accent}">
                  <div class="char-name-en">{ch.EnName}</div>
                  <div class="char-name-ja">{ch.JaName}</div>
                </div>
                <div class="char-card-body">
                  <p class="char-desc">{ch.Desc}</p>
                </div>
                <div class="char-card-footer">詳細を見る &rarr;</div>
              </a>
        """));

    return Layout("キャラクター一覧", "characters", "#4a90d9", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">キャラクター一覧</h1>
          <p class="hero-sub">Character List</p>
          <p class="hero-desc">5人のキャラクターのカード・メカニクスを確認できます。</p>
        </div>
        <div class="char-grid">
          {cards}
        </div>
        """);
}

static string BuildPageList(PageEntry[] pages, CharData[] chars,
    string[] allCardIds, string[] allRelicIds, string[] allEventIds, string[] allEncounterIds)
{
    static string Pills(IEnumerable<(string href, string label, string? labelSub)> items) =>
        string.Concat(items.Select(x =>
            x.labelSub is not null
                ? $"""<a href="{x.href}" class="mec-tag">{x.label}<span class="mec-sub">{x.labelSub}</span></a>"""
                : $"""<a href="{x.href}" class="mec-tag">{x.label}</a>"""));

    static string SubSection(string title, string pillsHtml) => $"""
        <div class="pagelist-sub">
          <div class="pagelist-sub-title">{title}</div>
          <div class="mec-tags">{pillsHtml}</div>
        </div>
        """;

    // ── サイト情報 ──────────────────────────────────────────────────────────────
    var siteInfoLinks = new (string href, string label)[]
    {
        ("index.html",      "トップ"),
        ("about.html",      "このサイトについて"),
        ("changelog.html",  "更新履歴"),
        ("pages.html",      "全ページ一覧"),
    };
    var siteInfoHtml = string.Concat(siteInfoLinks.Select(x =>
        $"""<a href="{x.href}" class="pagelist-list-link">{x.label}</a>"""));

    // ── キャラクター ────────────────────────────────────────────────────────────
    var charListLink = $"""<a href="characters.html" class="pagelist-list-link">キャラクター一覧</a>""";
    var charIndivLinks = Pills(chars.Select(ch =>
        (href: $"{ch.Id}.html", label: ch.JaName, labelSub: ch.EnName != ch.JaName ? (string?)ch.EnName : null)));

    // ── カード ──────────────────────────────────────────────────────────────────
    var charCardSubSecs = string.Concat(chars.Select(ch =>
    {
        var ids = allCardIds
            .Where(id => CardDatabaseService.GetCardCharacter(id)
                .Equals(ch.EnName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => TypeOrder(CardDatabaseService.GetCardType(id)))
            .ThenBy(id => RarityOrder(CardDatabaseService.GetCardRarity(id)))
            .ThenBy(id => CardDatabaseService.GetName(id))
            .ToArray();
        if (ids.Length == 0) return "";
        var pills = Pills(ids.Select(id =>
        {
            var en = CardDatabaseService.GetName(id);
            var ja = CardDatabaseService.GetName(id, japanese: true);
            return ($"cards/{ch.Id}/{RawId(id)}.html", en, ja != en ? (string?)ja : null);
        }));
        return SubSection($"{ch.EnName} ({ids.Length})", pills);
    }));
    var sharedIds = allCardIds
        .Where(id => !chars.Any(ch => CardDatabaseService.GetCardCharacter(id)
            .Equals(ch.EnName, StringComparison.OrdinalIgnoreCase)))
        .ToArray();
    if (sharedIds.Length > 0)
    {
        var pills = Pills(sharedIds.Select(id =>
        {
            var en = CardDatabaseService.GetName(id);
            var ja = CardDatabaseService.GetName(id, japanese: true);
            return ($"cards/shared/{RawId(id)}.html", en, ja != en ? (string?)ja : null);
        }));
        charCardSubSecs += SubSection($"共有 ({sharedIds.Length})", pills);
    }

    // ── レリック ────────────────────────────────────────────────────────────────
    var relicPills = Pills(allRelicIds.Select(id =>
    {
        var en = CardDatabaseService.GetRelicTitle(id);
        var ja = CardDatabaseService.GetRelicTitle(id, japanese: true);
        return ($"relics/{id}.html", en, ja != en ? (string?)ja : null);
    }));

    // ── イベント ────────────────────────────────────────────────────────────────
    var eventPills = Pills(allEventIds.Select(id =>
    {
        var en = CardDatabaseService.GetEventTitle(id);
        var ja = CardDatabaseService.GetEventTitle(id, japanese: true);
        return ($"events/{id}.html", en, ja != en ? (string?)ja : null);
    }));

    // ── エンカウンター ──────────────────────────────────────────────────────────
    var encounterPills = Pills(allEncounterIds.Select(id =>
    {
        var en = EncounterDatabaseService.GetEncounterName(id);
        var ja = EncounterDatabaseService.GetEncounterName(id, japanese: true);
        return ($"encounters/{id}.html", en, ja != en ? (string?)ja : null);
    }));

    // ── メカニクス ──────────────────────────────────────────────────────────────
    var mecSubSecs = string.Concat(
        CharacterMechanics.All
            .Where(g => g.Mechanics.Length > 0)
            .Select(g =>
            {
                var groupDir = g.EnLabel.ToLowerInvariant();
                var pills = Pills(g.Mechanics.Select(mec =>
                    ($"mechanics/{groupDir}/{MecFileName(mec.EnLabel)}",
                     mec.JaLabel,
                     mec.JaLabel != mec.EnLabel ? (string?)mec.EnLabel : null)));
                return SubSection($"{g.JaLabel} ({g.Mechanics.Length})", pills);
            }));

    var totalPages = 4 + 1 + chars.Length + allCardIds.Length + allRelicIds.Length
                   + allEventIds.Length + allEncounterIds.Length + 1
                   + CharacterMechanics.All.Sum(g => g.Mechanics.Length);

    var content = $"""
        <div class="page-hero">
          <h1 class="hero-title">全ページ一覧</h1>
          <p class="hero-sub">Full Page Index</p>
          <p class="hero-desc">全{totalPages}ページのリンクを列挙しています。</p>
        </div>

        <section class="section">
          <h2 class="section-title">サイト情報</h2>
          <div class="pagelist-top-links">{siteInfoHtml}</div>
        </section>

        <section class="section">
          <h2 class="section-title">キャラクター — {chars.Length + 1}件</h2>
          <div class="pagelist-top-links">{charListLink}</div>
          <div class="pagelist-sub">
            <div class="pagelist-sub-title">個別ページ ({chars.Length})</div>
            <div class="mec-tags">{charIndivLinks}</div>
          </div>
        </section>

        <section class="section">
          <h2 class="section-title">カード — {allCardIds.Length + 1}件</h2>
          <div class="pagelist-top-links"><a href="cards.html" class="pagelist-list-link">カード一覧</a></div>
          {charCardSubSecs}
        </section>

        <section class="section">
          <h2 class="section-title">レリック — {allRelicIds.Length + 1}件</h2>
          <div class="pagelist-top-links"><a href="relics.html" class="pagelist-list-link">レリック一覧</a></div>
          <div class="pagelist-sub">
            <div class="mec-tags">{relicPills}</div>
          </div>
        </section>

        <section class="section">
          <h2 class="section-title">イベント — {allEventIds.Length + 1}件</h2>
          <div class="pagelist-top-links"><a href="events.html" class="pagelist-list-link">イベント一覧</a></div>
          <div class="pagelist-sub">
            <div class="mec-tags">{eventPills}</div>
          </div>
        </section>

        <section class="section">
          <h2 class="section-title">エンカウンター — {allEncounterIds.Length + 1}件</h2>
          <div class="pagelist-top-links"><a href="encounters.html" class="pagelist-list-link">エンカウンター一覧</a></div>
          <div class="pagelist-sub">
            <div class="mec-tags">{encounterPills}</div>
          </div>
        </section>

        <section class="section">
          <h2 class="section-title">メカニクス — {CharacterMechanics.All.Sum(g => g.Mechanics.Length) + 1}件</h2>
          <div class="pagelist-top-links"><a href="mechanics.html" class="pagelist-list-link">メカニクス一覧</a></div>
          {mecSubSecs}
        </section>
        """;

    return Layout("全ページ一覧", "pages", "#4a90d9", chars, content);
}

static string BuildCardListPage(string[] allCardIds, CharData[] chars)
{
    var charNames = new HashSet<string>(chars.Select(c => c.EnName), StringComparer.OrdinalIgnoreCase);

    var groups = chars
        .Select(ch => (
            Label:   ch.EnName,
            LabelJa: ch.JaName,
            Accent:  ch.Accent,
            CharId:  ch.Id,
            Ids: allCardIds
                .Where(id => CardDatabaseService.GetCardCharacter(id)
                    .Equals(ch.EnName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(id => TypeOrder(CardDatabaseService.GetCardType(id)))
                .ThenBy(id => RarityOrder(CardDatabaseService.GetCardRarity(id)))
                .ThenBy(id => CardDatabaseService.GetName(id))
                .ToArray()
        ))
        .Append((
            Label:   "共有・特殊",
            LabelJa: "",
            Accent:  "#888",
            CharId:  "shared",
            Ids: allCardIds
                .Where(id => !charNames.Contains(CardDatabaseService.GetCardCharacter(id)))
                .OrderBy(id => TypeOrder(CardDatabaseService.GetCardType(id)))
                .ThenBy(id => RarityOrder(CardDatabaseService.GetCardRarity(id)))
                .ThenBy(id => CardDatabaseService.GetName(id))
                .ToArray()
        ))
        .Where(g => g.Ids.Length > 0)
        .ToArray();

    var sections = string.Concat(groups.Select(g =>
    {
        var metaText = g.LabelJa != "" ? $"{g.LabelJa} · {g.Ids.Length}件" : $"{g.Ids.Length}件";

        var rows = string.Concat(g.Ids.Select(id =>
        {
            var rawId   = RawId(id);
            var nameEn  = CardDatabaseService.GetName(id);
            var nameJa  = CardDatabaseService.GetName(id, japanese: true);
            var type    = CardDatabaseService.GetCardType(id);
            var rarity  = CardDatabaseService.GetCardRarity(id);
            var flags   = ComputeFlags(id);
            var dir     = GetCardDir(id, chars);
            var href    = $"cards/{dir}/{rawId}.html";
            var (rawDescEn, rawDescJa) = CardDatabaseService.GetDescription(id);
            var descAttr = System.Net.WebUtility.HtmlEncode(
                DescriptionFormatter.Clean(rawDescEn) + " " +
                DescriptionFormatter.Clean(rawDescJa, japanese: true));

            var typeBadge   = type   != "" ? $"""<span class="badge type-{type.ToLower()}">{type}</span>""" : "";
            var rarityBadge = rarity != "" ? $"""<span class="badge rarity-{rarity.ToLower()}">{rarity}</span>""" : "";
            var flagBadges  = string.Concat(
                CardFlags.AllDefs
                    .Where(f => flags.Contains(f.Key))
                    .Select(f => $"""<span class="badge flag-badge">{f.Label}</span>"""));
            var jaSpan    = nameJa != nameEn ? $"""<span class="card-name-ja">{nameJa}</span>""" : "";
            var charLabel = g.LabelJa != "" ? g.LabelJa : g.Label;
            var charBadge = $"""<span class="badge char-{g.CharId}">{charLabel}</span>""";

            return $"""
                      <tr data-desc="{descAttr}">
                        <td class="col-name">
                          <a href="{href}" class="card-name-link">{nameEn}</a>{jaSpan}
                        </td>
                        <td class="col-type">{typeBadge}</td>
                        <td class="col-rarity">{rarityBadge}</td>
                        <td class="col-char">{charBadge}</td>
                        <td class="col-flags"><div class="flag-cell">{flagBadges}</div></td>
                      </tr>
                """;
        }));

        return $"""
            <section class="section" data-char="{g.CharId}">
              <h2 class="section-title">
                <span class="section-dot" style="background:{g.Accent}"></span>
                {g.Label}
                <span class="section-meta">{metaText}</span>
              </h2>
              <table class="card-table">
                <thead>
                  <tr>
                    <th>カード名</th><th>タイプ</th><th>レアリティ</th><th>キャラ</th><th>特性</th>
                  </tr>
                </thead>
                <tbody>{rows}</tbody>
              </table>
            </section>
            """;
    }));

    var charBtns = string.Concat(chars.Select(ch =>
        $"""<button class="filter-btn" data-filter="{ch.Id}">{ch.EnName}</button>"""));

    var filterPanel = $"""
        <div class="filter-panel">
          <div class="filter-section">
            <span class="filter-label">検索</span>
            <input type="text" id="card-search" class="search-input" placeholder="名前・説明文で検索…" autocomplete="off">
            <button class="filter-btn" id="thumb-toggle" style="margin-left:4px">サムネイル表示</button>
          </div>
          <div class="filter-section">
            <span class="filter-label">キャラ</span>
            <div class="filter-bar">
              <button class="filter-btn active" data-filter="all">すべて</button>
              {charBtns}
              <button class="filter-btn" data-filter="shared">共有・特殊</button>
            </div>
          </div>
          <div class="filter-section">
            <span class="filter-label">タイプ</span>
            <div class="filter-bar">
              <button class="filter-btn active" data-type="all">すべて</button>
              <button class="filter-btn" data-type="attack">Attack</button>
              <button class="filter-btn" data-type="skill">Skill</button>
              <button class="filter-btn" data-type="power">Power</button>
              <button class="filter-btn" data-type="status">Status</button>
              <button class="filter-btn" data-type="curse">Curse</button>
              <button class="filter-btn" data-type="quest">Quest</button>
            </div>
          </div>
          <div class="filter-section">
            <span class="filter-label">レアリティ</span>
            <div class="filter-bar">
              <button class="filter-btn active" data-rarity="all">すべて</button>
              <button class="filter-btn" data-rarity="starter">Starter</button>
              <button class="filter-btn" data-rarity="common">Common</button>
              <button class="filter-btn" data-rarity="uncommon">Uncommon</button>
              <button class="filter-btn" data-rarity="rare">Rare</button>
              <button class="filter-btn" data-rarity="ancient">Ancient</button>
              <button class="filter-btn" data-rarity="event">Event</button>
              <button class="filter-btn" data-rarity="shop">Shop</button>
            </div>
          </div>
        </div>
        """;

    var charCss = BuildCharCss(chars);

    const string FILTER_CSS = """
        <style>
        .filter-panel {
          background: #fff; border-radius: 10px; padding: 16px 20px;
          margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .filter-section { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
        .filter-section + .filter-section { margin-top: 10px; padding-top: 10px; border-top: 1px solid #f2f2f2; }
        .filter-label {
          font-size: 10.5px; font-weight: 700; text-transform: uppercase;
          letter-spacing: 0.7px; color: #bbb; min-width: 66px; flex-shrink: 0;
        }
        .filter-bar { display: flex; gap: 7px; flex-wrap: wrap; }
        .filter-btn {
          padding: 5px 13px; border: 1.5px solid transparent; border-radius: 20px;
          font-size: 12.5px; font-weight: 600; cursor: pointer; transition: all 0.15s;
          background: #fff; box-shadow: 0 1px 2px rgba(0,0,0,0.07); font-family: inherit;
        }
        .filter-btn:hover { transform: translateY(-1px); box-shadow: 0 3px 8px rgba(0,0,0,0.13); }
        .filter-btn[data-filter="all"]              { color: #555; border-color: #ccc; }
        .filter-btn[data-filter="all"].active       { background: #555; color: #fff; border-color: #555; }
        .filter-btn[data-filter="ironclad"]         { color: #c0392b; border-color: #c0392b; }
        .filter-btn[data-filter="ironclad"].active  { background: #c0392b; color: #fff; }
        .filter-btn[data-filter="silent"]           { color: #1a7a4a; border-color: #1a7a4a; }
        .filter-btn[data-filter="silent"].active    { background: #1a7a4a; color: #fff; }
        .filter-btn[data-filter="defect"]           { color: #1a5799; border-color: #1a5799; }
        .filter-btn[data-filter="defect"].active    { background: #1a5799; color: #fff; }
        .filter-btn[data-filter="necrobinder"]      { color: #6c3483; border-color: #6c3483; }
        .filter-btn[data-filter="necrobinder"].active { background: #6c3483; color: #fff; }
        .filter-btn[data-filter="regent"]           { color: #7d6608; border-color: #7d6608; }
        .filter-btn[data-filter="regent"].active    { background: #7d6608; color: #fff; }
        .filter-btn[data-filter="shared"]           { color: #555; border-color: #999; }
        .filter-btn[data-filter="shared"].active    { background: #666; color: #fff; border-color: #666; }
        .filter-btn[data-type="all"]             { color: #555; border-color: #ccc; }
        .filter-btn[data-type="all"].active      { background: #555; color: #fff; border-color: #555; }
        .filter-btn[data-type="attack"]          { color: #c0392b; border-color: #c0392b; }
        .filter-btn[data-type="attack"].active   { background: #c0392b; color: #fff; }
        .filter-btn[data-type="skill"]           { color: #1a5799; border-color: #1a5799; }
        .filter-btn[data-type="skill"].active    { background: #1a5799; color: #fff; }
        .filter-btn[data-type="power"]           { color: #7d6608; border-color: #7d6608; }
        .filter-btn[data-type="power"].active    { background: #7d6608; color: #fff; }
        .filter-btn[data-type="status"]          { color: #666; border-color: #aaa; }
        .filter-btn[data-type="status"].active   { background: #666; color: #fff; border-color: #666; }
        .filter-btn[data-type="curse"]           { color: #6c3483; border-color: #6c3483; }
        .filter-btn[data-type="curse"].active    { background: #6c3483; color: #fff; }
        .filter-btn[data-type="quest"]           { color: #1a7a4a; border-color: #1a7a4a; }
        .filter-btn[data-type="quest"].active    { background: #1a7a4a; color: #fff; }
        .filter-btn[data-rarity="all"]              { color: #555; border-color: #ccc; }
        .filter-btn[data-rarity="all"].active       { background: #555; color: #fff; border-color: #555; }
        .filter-btn[data-rarity="starter"]          { color: #888; border-color: #bbb; }
        .filter-btn[data-rarity="starter"].active   { background: #888; color: #fff; border-color: #888; }
        .filter-btn[data-rarity="common"]           { color: #555; border-color: #999; }
        .filter-btn[data-rarity="common"].active    { background: #666; color: #fff; border-color: #666; }
        .filter-btn[data-rarity="uncommon"]         { color: #1a5799; border-color: #1a5799; }
        .filter-btn[data-rarity="uncommon"].active  { background: #1a5799; color: #fff; }
        .filter-btn[data-rarity="rare"]             { color: #c0392b; border-color: #c0392b; }
        .filter-btn[data-rarity="rare"].active      { background: #c0392b; color: #fff; }
        .filter-btn[data-rarity="ancient"]          { color: #a0600c; border-color: #a0600c; }
        .filter-btn[data-rarity="ancient"].active   { background: #a0600c; color: #fff; }
        .filter-btn[data-rarity="event"]            { color: #1a7a4a; border-color: #1a7a4a; }
        .filter-btn[data-rarity="event"].active     { background: #1a7a4a; color: #fff; }
        .filter-btn[data-rarity="shop"]             { color: #7d1a7d; border-color: #7d1a7d; }
        .filter-btn[data-rarity="shop"].active      { background: #7d1a7d; color: #fff; }
        .search-input {
          flex: 1; min-width: 180px; max-width: 320px; padding: 5px 13px;
          border: 1.5px solid #ddd; border-radius: 20px; font-size: 13px;
          color: #333; background: #fff; font-family: inherit;
          outline: none; transition: border-color 0.15s, box-shadow 0.15s;
        }
        .search-input:focus { border-color: #4a90d9; box-shadow: 0 0 0 3px rgba(74,144,217,0.12); }
        .search-input::placeholder { color: #bbb; }
        .card-thumb {
          width: 40px; height: 40px; object-fit: cover; border-radius: 3px;
          vertical-align: middle; margin-right: 8px; display: inline-block; background: #f0f0f0;
        }
        </style>
        """;

    const string FILTER_JS = """
        <script>
        (function () {
          var sections    = document.querySelectorAll('.section[data-char]');
          var searchInput = document.getElementById('card-search');
          function getActive(a) {
            var b = document.querySelector('.filter-btn[data-' + a + '].active');
            return b ? b.getAttribute('data-' + a) : 'all';
          }
          function applyFilters() {
            var cf = getActive('filter'), tf = getActive('type'), rf = getActive('rarity');
            var q  = searchInput.value.trim().toLowerCase();
            sections.forEach(function (sec) {
              if (cf !== 'all' && sec.dataset.char !== cf) { sec.style.display = 'none'; return; }
              if (tf === 'all' && rf === 'all' && q === '') {
                sec.style.display = '';
                sec.querySelectorAll('tbody tr').forEach(function (r) { r.style.display = ''; });
                return;
              }
              var n = 0;
              sec.querySelectorAll('tbody tr').forEach(function (row) {
                var tb = row.querySelector('.col-type .badge');
                var rb = row.querySelector('.col-rarity .badge');
                var nl = row.querySelector('.card-name-link');
                var nj = row.querySelector('.card-name-ja');
                var tc = tb ? Array.from(tb.classList).find(function(c){return c.startsWith('type-');})   : '';
                var rc = rb ? Array.from(rb.classList).find(function(c){return c.startsWith('rarity-');}) : '';
                var ok = (tf === 'all' || tc === 'type-' + tf)
                      && (rf === 'all' || rc === 'rarity-' + rf)
                      && (q  === ''   || (nl && nl.textContent.toLowerCase().includes(q))
                                      || (nj && nj.textContent.toLowerCase().includes(q))
                                      || row.dataset.desc.toLowerCase().includes(q));
                row.style.display = ok ? '' : 'none';
                if (ok) n++;
              });
              sec.style.display = n > 0 ? '' : 'none';
            });
          }
          function bindGroup(sel) {
            document.querySelectorAll(sel).forEach(function (btn) {
              btn.addEventListener('click', function () {
                document.querySelectorAll(sel).forEach(function (b) { b.classList.remove('active'); });
                this.classList.add('active');
                applyFilters();
              });
            });
          }
          searchInput.addEventListener('input', applyFilters);
          bindGroup('.filter-btn[data-filter]');
          bindGroup('.filter-btn[data-type]');
          bindGroup('.filter-btn[data-rarity]');
          var BASE = '../../tools/extracted/images/card_portraits_png/';
          var on = false;
          function portraitSrc(row) {
            var lnk = row.querySelector('.card-name-link');
            if (!lnk) return null;
            var p = lnk.getAttribute('href').split('/');
            if (p.length < 3) return null;
            var dir = p[1], file = p[2].replace('.html','').toLowerCase(), folder;
            if (dir !== 'shared') { folder = dir; } else {
              var tb = row.querySelector('.col-type .badge');
              var tc = tb ? Array.from(tb.classList).find(function(c){return c.startsWith('type-');}) : '';
              folder = tc === 'type-curse' ? 'curse' : tc === 'type-quest' ? 'quest'
                     : tc === 'type-status' ? 'status' : 'colorless';
            }
            return BASE + folder + '/' + file + '.png';
          }
          document.getElementById('thumb-toggle').addEventListener('click', function () {
            on = !on;
            this.classList.toggle('active', on);
            document.querySelectorAll('.section[data-char] tbody tr').forEach(function (row) {
              var cell = row.querySelector('.col-name');
              if (!cell) return;
              if (on) {
                var src = portraitSrc(row);
                if (src && !cell.querySelector('.card-thumb')) {
                  var img = document.createElement('img');
                  img.src = src; img.className = 'card-thumb'; img.loading = 'lazy'; img.alt = '';
                  cell.insertBefore(img, cell.firstChild);
                }
              } else {
                var e = cell.querySelector('.card-thumb');
                if (e) e.remove();
              }
            });
          });
          // URL パラメーターによるフィルタ初期化
          (function() {
            var p = new URLSearchParams(window.location.search);
            function setActive(attr, val) {
              if (!val) return;
              document.querySelectorAll('.filter-btn[data-' + attr + ']')
                .forEach(function(b) { b.classList.remove('active'); });
              var t = document.querySelector('.filter-btn[data-' + attr + '="' + val + '"]');
              if (t) t.classList.add('active');
            }
            var cf = p.get('char'), tf = p.get('type'), rf = p.get('rarity'), sq = p.get('search');
            if (cf || tf || rf || sq) {
              setActive('filter', cf);
              setActive('type', tf);
              setActive('rarity', rf);
              if (sq) searchInput.value = sq;
              applyFilters();
            }
          })();
        })();
        </script>
        """;

    return Layout("カード一覧", "cards", "#2c3e50", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">カード一覧</h1>
          <p class="hero-sub">全{allCardIds.Length}件</p>
        </div>
        {filterPanel}
        {sections}
        """, extraHead: charCss + FILTER_CSS, extraFoot: FILTER_JS);
}

static string BuildRelicListPage(string[] allRelicIds, CharData[] chars)
{
    var rows = string.Concat(allRelicIds.Select(id =>
    {
        var nameEn = CardDatabaseService.GetRelicTitle(id);
        var nameJa = CardDatabaseService.GetRelicTitle(id, japanese: true);
        var rarity = CardDatabaseService.GetRelicRarity(id);
        var href   = $"relics/{id}.html";
        var jaSpan      = nameJa != nameEn ? $"""<span class="card-name-ja">{nameJa}</span>""" : "";
        var rarityBadge = rarity != "" ? $"""<span class="badge rarity-{rarity.ToLower()}">{rarity}</span>""" : "";
        var (rawDescEn, rawDescJa) = CardDatabaseService.GetDescription("RELIC." + id);
        var descAttr = System.Net.WebUtility.HtmlEncode(
            DescriptionFormatter.Clean(rawDescEn) + " " +
            DescriptionFormatter.Clean(rawDescJa, japanese: true));
        return $"""
                  <tr data-rarity="{rarity.ToLower()}" data-desc="{descAttr}">
                    <td class="col-name">
                      <a href="{href}" class="card-name-link">{nameEn}</a>{jaSpan}
                    </td>
                    <td class="col-rarity">{rarityBadge}</td>
                  </tr>
            """;
    }));

    const string RELIC_FILTER_CSS = """
        <style>
        .relic-filter-panel {
          background: #fff; border-radius: 10px; padding: 16px 20px;
          margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .relic-filter-section { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
        .relic-filter-section + .relic-filter-section { margin-top: 10px; padding-top: 10px; border-top: 1px solid #f2f2f2; }
        .relic-filter-label {
          font-size: 10.5px; font-weight: 700; text-transform: uppercase;
          letter-spacing: 0.7px; color: #bbb; min-width: 66px; flex-shrink: 0;
        }
        .relic-search-input {
          flex: 1; min-width: 200px; max-width: 380px; padding: 5px 13px;
          border: 1.5px solid #ddd; border-radius: 20px; font-size: 13px;
          color: #333; background: #fff; font-family: inherit;
          outline: none; transition: border-color 0.15s, box-shadow 0.15s;
        }
        .relic-search-input:focus { border-color: #a0600c; box-shadow: 0 0 0 3px rgba(160,96,12,0.12); }
        .relic-search-input::placeholder { color: #bbb; }
        .relic-count { font-size: 12px; color: #bbb; margin-left: 8px; }
        .relic-filter-bar { display: flex; gap: 7px; flex-wrap: wrap; }
        .rfbtn {
          padding: 5px 13px; border: 1.5px solid transparent; border-radius: 20px;
          font-size: 12.5px; font-weight: 600; cursor: pointer; transition: all 0.15s;
          background: #fff; box-shadow: 0 1px 2px rgba(0,0,0,0.07); font-family: inherit;
        }
        .rfbtn:hover { transform: translateY(-1px); box-shadow: 0 3px 8px rgba(0,0,0,0.13); }
        .rfbtn[data-r="all"]      { color: #555; border-color: #ccc; }
        .rfbtn[data-r="all"].active   { background: #555; color: #fff; border-color: #555; }
        .rfbtn[data-r="common"]   { color: #555; border-color: #999; }
        .rfbtn[data-r="common"].active   { background: #666; color: #fff; border-color: #666; }
        .rfbtn[data-r="uncommon"] { color: #1a5799; border-color: #1a5799; }
        .rfbtn[data-r="uncommon"].active { background: #1a5799; color: #fff; }
        .rfbtn[data-r="rare"]     { color: #c0392b; border-color: #c0392b; }
        .rfbtn[data-r="rare"].active     { background: #c0392b; color: #fff; }
        .rfbtn[data-r="shop"]     { color: #7d1a7d; border-color: #7d1a7d; }
        .rfbtn[data-r="shop"].active     { background: #7d1a7d; color: #fff; }
        .rfbtn[data-r="event"]    { color: #1a7a4a; border-color: #1a7a4a; }
        .rfbtn[data-r="event"].active    { background: #1a7a4a; color: #fff; }
        .rfbtn[data-r="starter"]  { color: #888; border-color: #bbb; }
        .rfbtn[data-r="starter"].active  { background: #888; color: #fff; border-color: #888; }
        .rfbtn[data-r="ancient"]  { color: #a0600c; border-color: #a0600c; }
        .rfbtn[data-r="ancient"].active  { background: #a0600c; color: #fff; }
        </style>
        """;

    const string RELIC_FILTER_JS = """
        <script>
        (function () {
          var input   = document.getElementById('relic-search');
          var countEl = document.getElementById('relic-count');
          var allRows = document.querySelectorAll('#relic-table tbody tr');
          function getActiveRarity() {
            var b = document.querySelector('.rfbtn.active');
            return b ? b.getAttribute('data-r') : 'all';
          }
          function update() {
            var q = input.value.trim().toLowerCase();
            var r = getActiveRarity();
            var n = 0;
            allRows.forEach(function (row) {
              var lnk = row.querySelector('.card-name-link');
              var ja  = row.querySelector('.card-name-ja');
              var rowRarity = row.getAttribute('data-rarity') || '';
              var nameOk = q === '' || (lnk && lnk.textContent.toLowerCase().includes(q))
                                    || (ja  && ja.textContent.toLowerCase().includes(q))
                                    || row.dataset.desc.toLowerCase().includes(q);
              var rarityOk = r === 'all' || rowRarity === r;
              var ok = nameOk && rarityOk;
              row.style.display = ok ? '' : 'none';
              if (ok) n++;
            });
            countEl.textContent = n + '件';
          }
          input.addEventListener('input', update);
          document.querySelectorAll('.rfbtn').forEach(function (btn) {
            btn.addEventListener('click', function () {
              document.querySelectorAll('.rfbtn').forEach(function (b) { b.classList.remove('active'); });
              this.classList.add('active');
              update();
            });
          });
        })();
        </script>
        """;

    var filterPanel = $"""
        <div class="relic-filter-panel">
          <div class="relic-filter-section">
            <span class="relic-filter-label">検索</span>
            <input type="text" id="relic-search" class="relic-search-input" placeholder="名前・説明文で検索…" autocomplete="off">
            <span class="relic-count" id="relic-count">{allRelicIds.Length}件</span>
          </div>
          <div class="relic-filter-section">
            <span class="relic-filter-label">レアリティ</span>
            <div class="relic-filter-bar">
              <button class="rfbtn active" data-r="all">すべて</button>
              <button class="rfbtn" data-r="starter">Starter</button>
              <button class="rfbtn" data-r="common">Common</button>
              <button class="rfbtn" data-r="uncommon">Uncommon</button>
              <button class="rfbtn" data-r="rare">Rare</button>
              <button class="rfbtn" data-r="shop">Shop</button>
              <button class="rfbtn" data-r="event">Event</button>
              <button class="rfbtn" data-r="ancient">Ancient</button>
            </div>
          </div>
        </div>
        """;

    return Layout("レリック一覧", "relics", "#a0600c", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">レリック一覧</h1>
          <p class="hero-sub">全{allRelicIds.Length}件</p>
        </div>
        {filterPanel}
        <section class="section">
          <table class="card-table" id="relic-table">
            <thead>
              <tr><th>レリック名</th><th>レアリティ</th></tr>
            </thead>
            <tbody>{rows}</tbody>
          </table>
        </section>
        """, extraHead: RELIC_FILTER_CSS, extraFoot: RELIC_FILTER_JS);
}

static string BuildEventListPage(string[] allEventIds, CharData[] chars, HashSet<string> eventsWithImg)
{
    var rows = string.Concat(allEventIds.Select(id =>
    {
        var nameEn = CardDatabaseService.GetEventTitle(id);
        var nameJa = CardDatabaseService.GetEventTitle(id, japanese: true);
        var href   = $"events/{id}.html";
        var jaSpan = nameJa != nameEn ? $"""<span class="card-name-ja">{nameJa}</span>""" : "";
        var hasImg = eventsWithImg.Contains(id) ? "true" : "false";
        return $"""
                  <tr data-has-img="{hasImg}">
                    <td class="col-name">
                      <a href="{href}" class="card-name-link">{nameEn}</a>{jaSpan}
                    </td>
                  </tr>
            """;
    }));

    const string EVENT_FILTER_CSS = """
        <style>
        .event-filter-panel {
          background: #fff; border-radius: 10px; padding: 16px 20px;
          margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .event-filter-section { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
        .event-search-input {
          flex: 1; min-width: 200px; max-width: 400px; padding: 5px 13px;
          border: 1.5px solid #ddd; border-radius: 20px; font-size: 13px;
          color: #333; background: #fff; font-family: inherit;
          outline: none; transition: border-color 0.15s, box-shadow 0.15s;
        }
        .event-search-input:focus { border-color: #1a6678; box-shadow: 0 0 0 3px rgba(26,102,120,0.12); }
        .event-search-input::placeholder { color: #bbb; }
        .event-count { font-size: 12px; color: #bbb; margin-left: 8px; }
        .event-thumb {
          width: 56px; height: 36px; object-fit: cover; border-radius: 3px;
          vertical-align: middle; margin-right: 8px; display: inline-block;
          background: #f0f0f0;
        }
        </style>
        """;

    const string EVENT_FILTER_JS = """
        <script>
        (function () {
          var input   = document.getElementById('event-search');
          var countEl = document.getElementById('event-count');
          var allRows = document.querySelectorAll('#event-table tbody tr');
          function update() {
            var q = input.value.trim().toLowerCase();
            var n = 0;
            allRows.forEach(function (row) {
              var lnk = row.querySelector('.card-name-link');
              var ja  = row.querySelector('.card-name-ja');
              var ok = q === '' || (lnk && lnk.textContent.toLowerCase().includes(q))
                                || (ja  && ja.textContent.toLowerCase().includes(q));
              row.style.display = ok ? '' : 'none';
              if (ok) n++;
            });
            countEl.textContent = n + '件';
          }
          input.addEventListener('input', update);

          var BASE = 'images/events/';
          var on = false;
          document.getElementById('event-thumb-toggle').addEventListener('click', function () {
            on = !on;
            this.classList.toggle('active', on);
            allRows.forEach(function (row) {
              if (row.getAttribute('data-has-img') !== 'true') return;
              var cell = row.querySelector('.col-name');
              if (!cell) return;
              if (on) {
                if (!cell.querySelector('.event-thumb')) {
                  var lnk = cell.querySelector('.card-name-link');
                  if (!lnk) return;
                  var id = lnk.getAttribute('href').replace('events/', '').replace('.html', '').toLowerCase();
                  var img = document.createElement('img');
                  img.src = BASE + id + '.png'; img.className = 'event-thumb';
                  img.loading = 'lazy'; img.alt = '';
                  cell.insertBefore(img, cell.firstChild);
                }
              } else {
                var e = cell.querySelector('.event-thumb');
                if (e) e.remove();
              }
            });
          });
        })();
        </script>
        """;

    var filterPanel = $"""
        <div class="event-filter-panel">
          <div class="event-filter-section">
            <span class="relic-filter-label">イベント名</span>
            <input type="text" id="event-search" class="event-search-input" placeholder="名前で検索…" autocomplete="off">
            <span class="event-count" id="event-count">{allEventIds.Length}件</span>
            <button class="filter-btn" id="event-thumb-toggle" style="margin-left:4px">サムネイル表示</button>
          </div>
        </div>
        """;

    return Layout("イベント一覧", "events", "#1a6678", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">イベント一覧</h1>
          <p class="hero-sub">全{allEventIds.Length}件</p>
        </div>
        {filterPanel}
        <section class="section">
          <table class="card-table" id="event-table">
            <thead>
              <tr><th>イベント名</th></tr>
            </thead>
            <tbody>{rows}</tbody>
          </table>
        </section>
        """, extraHead: EVENT_FILTER_CSS, extraFoot: EVENT_FILTER_JS);
}

static string BuildEventPage(string eventId, CharData[] chars, bool hasImage = false, string review = "", string lastUpdated = "")
{
    const string basePath = "../";
    const string accent   = "#1a6678";
    const string lightBg  = "#eef6f8";

    var nameEn = CardDatabaseService.GetEventTitle(eventId);
    var nameJa = CardDatabaseService.GetEventTitle(eventId, japanese: true);
    var (rawDescEn, rawDescJa) = CardDatabaseService.GetEventDescription(eventId);
    var descEn = DescriptionFormatter.Clean(rawDescEn).Replace("\n", "<br>");
    var descJa = DescriptionFormatter.Clean(rawDescJa, japanese: true).Replace("\n", "<br>");
    var options = CardDatabaseService.GetEventOptions(eventId);

    var imgSection = hasImage ? $"""
        <div class="event-image-wrap">
          <img src="../images/events/{eventId.ToLowerInvariant()}.png" class="event-image" alt="{nameEn}">
        </div>
        """ : "";

    var descSection = descEn != "" ? $"""
        <section class="section">
          <h2 class="section-title">イベントテキスト</h2>
          <p class="desc-main">{descEn}</p>
          {(descJa != "" && descJa != descEn ? $"""<p class="desc-sub">{descJa}</p>""" : "")}
        </section>
        """ : "";

    var optionCards = string.Concat(options
        .Where(o => o.TitleEn != "")
        .Select(o =>
        {
            var titleJaPart = o.TitleJa != "" && o.TitleJa != o.TitleEn
                ? $"""<span class="event-option-title-ja">{o.TitleJa}</span>"""
                : "";
            var descEnClean = DescriptionFormatter.Clean(o.DescEn).Replace("\n", "<br>");
            var descJaClean = DescriptionFormatter.Clean(o.DescJa, japanese: true).Replace("\n", "<br>");
            var descEnPart  = descEnClean != "" ? $"""<div class="event-option-desc">{descEnClean}</div>""" : "";
            var descJaPart  = descJaClean != "" && descJaClean != descEnClean
                ? $"""<div class="event-option-desc-ja">{descJaClean}</div>"""
                : "";
            return $"""
                      <div class="event-option">
                        <div class="event-option-title">{o.TitleEn}{titleJaPart}</div>
                        {descEnPart}
                        {descJaPart}
                      </div>
                """;
        }));

    var optionsSection = optionCards != "" ? $"""
        <section class="section">
          <h2 class="section-title">選択肢</h2>
          <div class="event-options">{optionCards}</div>
        </section>
        """ : "";

    const string REVIEW_GUIDE = """

        <!-- REVIEW_START -->
        <!--
          【評価・メモ】
          このコメントブロック全体を削除し、かわりにHTMLを書いてください。
          ビルド（dotnet run --project StS2SiteBuilder）後も上書きされません。

          ▼ テンプレート（コピーして使ってください） ▼

          <section class="section">
            <h2 class="section-title">評価・メモ</h2>
            <p>ここに感想や評価を書く。</p>
          </section>
        -->
        <!-- REVIEW_END -->
        """;

    var reviewZone = review == ""
        ? REVIEW_GUIDE
        : $"""

        <!-- REVIEW_START -->{review}<!-- REVIEW_END -->
        """;

    var content = $"""
        <div class="card-detail-header" style="border-left:5px solid {accent};background:{lightBg}">
          <div class="card-breadcrumb">
            <a href="{basePath}events.html" class="char-back-link" style="color:{accent}">イベント一覧</a>
          </div>
          <h1 class="card-title-en" style="color:{accent}">{nameEn}</h1>
          {(nameJa != nameEn ? $"""<div class="card-title-ja">{nameJa}</div>""" : "")}
          <!-- LAST_UPDATED_START -->{(lastUpdated != "" ? $"""<div class="page-updated">最終更新: {lastUpdated}</div>""" : "")}<!-- LAST_UPDATED_END -->
        </div>
        {imgSection}
        {descSection}
        {reviewZone}
        {optionsSection}
        """;

    return Layout(nameEn, "events", accent, chars, content, basePath);
}

static string BuildEncounterListPage(string[] allEncounterIds, CharData[] chars)
{
    var rows = string.Concat(allEncounterIds.Select(id =>
    {
        var nameEn    = EncounterDatabaseService.GetEncounterName(id);
        var nameJa    = EncounterDatabaseService.GetEncounterName(id, japanese: true);
        var type      = EncounterDatabaseService.GetEncounterType(id);
        var href      = $"encounters/{id}.html";
        var jaSpan    = nameJa != nameEn ? $"""<span class="card-name-ja">{nameJa}</span>""" : "";
        var typeCls   = type != "" ? $"enc-{type.ToLower()}" : "enc-other";
        var typeLabel = type != "" ? type : "Other";
        var typeBadge = $"""<span class="badge {typeCls}">{typeLabel}</span>""";
        var dataType  = type != "" ? type.ToLower() : "other";
        return $"""
                  <tr data-etype="{dataType}">
                    <td class="col-name">
                      <a href="{href}" class="card-name-link">{nameEn}</a>{jaSpan}
                    </td>
                    <td class="col-type">{typeBadge}</td>
                  </tr>
            """;
    }));

    const string ENC_FILTER_CSS = """
        <style>
        .enc-filter-panel {
          background: #fff; border-radius: 10px; padding: 16px 20px;
          margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .enc-filter-section { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
        .enc-filter-section + .enc-filter-section { margin-top: 10px; padding-top: 10px; border-top: 1px solid #f2f2f2; }
        .enc-filter-label {
          font-size: 10.5px; font-weight: 700; text-transform: uppercase;
          letter-spacing: 0.7px; color: #bbb; min-width: 66px; flex-shrink: 0;
        }
        .enc-search-input {
          flex: 1; min-width: 200px; max-width: 380px; padding: 5px 13px;
          border: 1.5px solid #ddd; border-radius: 20px; font-size: 13px;
          color: #333; background: #fff; font-family: inherit;
          outline: none; transition: border-color 0.15s, box-shadow 0.15s;
        }
        .enc-search-input:focus { border-color: #8b2222; box-shadow: 0 0 0 3px rgba(139,34,34,0.12); }
        .enc-search-input::placeholder { color: #bbb; }
        .enc-count { font-size: 12px; color: #bbb; margin-left: 8px; }
        .enc-filter-bar { display: flex; gap: 7px; flex-wrap: wrap; }
        .efbtn {
          padding: 5px 13px; border: 1.5px solid transparent; border-radius: 20px;
          font-size: 12.5px; font-weight: 600; cursor: pointer; transition: all 0.15s;
          background: #fff; box-shadow: 0 1px 2px rgba(0,0,0,0.07); font-family: inherit;
        }
        .efbtn:hover { transform: translateY(-1px); box-shadow: 0 3px 8px rgba(0,0,0,0.13); }
        .efbtn[data-t="all"]    { color: #555;    border-color: #ccc; }
        .efbtn[data-t="all"].active    { background: #555;    color: #fff; border-color: #555; }
        .efbtn[data-t="boss"]   { color: #b03030; border-color: #b03030; }
        .efbtn[data-t="boss"].active   { background: #b03030; color: #fff; }
        .efbtn[data-t="elite"]  { color: #6c3483; border-color: #6c3483; }
        .efbtn[data-t="elite"].active  { background: #6c3483; color: #fff; }
        .efbtn[data-t="normal"] { color: #1a5799; border-color: #1a5799; }
        .efbtn[data-t="normal"].active { background: #1a5799; color: #fff; }
        .efbtn[data-t="weak"]   { color: #555;    border-color: #999; }
        .efbtn[data-t="weak"].active   { background: #666;    color: #fff; border-color: #666; }
        .efbtn[data-t="event"]  { color: #1a6678; border-color: #1a6678; }
        .efbtn[data-t="event"].active  { background: #1a6678; color: #fff; }
        .efbtn[data-t="other"]  { color: #888;    border-color: #bbb; }
        .efbtn[data-t="other"].active  { background: #888;    color: #fff; border-color: #888; }
        </style>
        """;

    const string ENC_FILTER_JS = """
        <script>
        (function () {
          var input   = document.getElementById('enc-search');
          var countEl = document.getElementById('enc-count');
          var allRows = document.querySelectorAll('#enc-table tbody tr');
          function getActiveType() {
            var b = document.querySelector('.efbtn.active');
            return b ? b.getAttribute('data-t') : 'all';
          }
          function update() {
            var q = input.value.trim().toLowerCase();
            var t = getActiveType();
            var n = 0;
            allRows.forEach(function (row) {
              var lnk     = row.querySelector('.card-name-link');
              var ja      = row.querySelector('.card-name-ja');
              var rowType = row.getAttribute('data-etype') || '';
              var nameOk  = q === '' || (lnk && lnk.textContent.toLowerCase().includes(q))
                                     || (ja  && ja.textContent.toLowerCase().includes(q));
              var typeOk  = t === 'all' || rowType === t;
              var ok = nameOk && typeOk;
              row.style.display = ok ? '' : 'none';
              if (ok) n++;
            });
            countEl.textContent = n + '件';
          }
          input.addEventListener('input', update);
          document.querySelectorAll('.efbtn').forEach(function (btn) {
            btn.addEventListener('click', function () {
              document.querySelectorAll('.efbtn').forEach(function (b) { b.classList.remove('active'); });
              this.classList.add('active');
              update();
            });
          });
        })();
        </script>
        """;

    var filterPanel = $"""
        <div class="enc-filter-panel">
          <div class="enc-filter-section">
            <span class="enc-filter-label">エンカウンター名</span>
            <input type="text" id="enc-search" class="enc-search-input" placeholder="名前で検索…" autocomplete="off">
            <span class="enc-count" id="enc-count">{allEncounterIds.Length}件</span>
          </div>
          <div class="enc-filter-section">
            <span class="enc-filter-label">タイプ</span>
            <div class="enc-filter-bar">
              <button class="efbtn active" data-t="all">すべて</button>
              <button class="efbtn" data-t="boss">Boss</button>
              <button class="efbtn" data-t="elite">Elite</button>
              <button class="efbtn" data-t="normal">Normal</button>
              <button class="efbtn" data-t="weak">Weak</button>
              <button class="efbtn" data-t="event">Event</button>
              <button class="efbtn" data-t="other">その他</button>
            </div>
          </div>
        </div>
        """;

    return Layout("エンカウンター一覧", "encounters", "#8b2222", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">エンカウンター一覧</h1>
          <p class="hero-sub">全{allEncounterIds.Length}件</p>
        </div>
        {filterPanel}
        <section class="section">
          <table class="card-table" id="enc-table">
            <thead>
              <tr><th>エンカウンター名</th><th>タイプ</th></tr>
            </thead>
            <tbody>{rows}</tbody>
          </table>
        </section>
        """, extraHead: ENC_FILTER_CSS, extraFoot: ENC_FILTER_JS);
}

static string BuildEncounterPage(string encId, CharData[] chars, HashSet<string> monstersWithImg, string review = "", string lastUpdated = "")
{
    const string basePath = "../";

    var nameEn = EncounterDatabaseService.GetEncounterName(encId);
    var nameJa = EncounterDatabaseService.GetEncounterName(encId, japanese: true);
    var type   = EncounterDatabaseService.GetEncounterType(encId);

    var (accent, lightBg) = type switch
    {
        "Boss"  => ("#b03030", "#fef8f8"),
        "Elite" => ("#6c3483", "#f9f4fc"),
        "Event" => ("#1a6678", "#eef6f8"),
        "Normal"=> ("#1a5799", "#f0f4fc"),
        "Weak"  => ("#555555", "#f5f5f5"),
        _       => ("#888888", "#f8f8f8"),
    };

    var typeCls   = type != "" ? $"enc-{type.ToLower()}" : "enc-other";
    var typeLabel = type != "" ? type : "Other";
    var typeBadge = $"""<span class="badge {typeCls}">{typeLabel}</span>""";

    var lossEn = FormatLoss(EncounterDatabaseService.GetLossText(encId),              nameEn);
    var lossJa = FormatLoss(EncounterDatabaseService.GetLossText(encId, japanese: true), nameJa, japanese: true);

    var lossSection = lossEn != "" ? $"""
        <section class="section">
          <h2 class="section-title">敗北テキスト</h2>
          <p class="desc-main" style="font-style:italic;color:#666">{lossEn}</p>
          {(lossJa != "" && lossJa != lossEn ? $"""<p class="desc-sub" style="font-style:italic">{lossJa}</p>""" : "")}
        </section>
        """ : "";

    var rewardEn = DescriptionFormatter.Clean(EncounterDatabaseService.GetCustomRewardDescription(encId));
    var rewardJa = DescriptionFormatter.Clean(EncounterDatabaseService.GetCustomRewardDescription(encId, japanese: true), japanese: true);

    var rewardSection = rewardEn != "" ? $"""
        <section class="section">
          <h2 class="section-title">特別報酬</h2>
          <p class="desc-main">{rewardEn}</p>
          {(rewardJa != "" && rewardJa != rewardEn ? $"""<p class="desc-sub">{rewardJa}</p>""" : "")}
        </section>
        """ : "";

    var monsterDirs = MonsterDatabaseService.GetEncounterMonsterDirs(encId);
    string monsterSection;
    if (monsterDirs == null || monsterDirs.Length == 0)
    {
        monsterSection = $"""
        <section class="section">
          <h2 class="section-title">登場モンスター</h2>
          <p style="color:#999;font-style:italic">未設定</p>
        </section>
        """;
    }
    else
    {
        var cards = string.Concat(monsterDirs.Select(dir =>
        {
            var m   = MonsterDatabaseService.GetOrCreate(dir);
            var img = monstersWithImg.Contains(dir)
                ? $"""<img src="{basePath}images/monsters/{dir}.png" alt="{m.EnLabel}" class="monster-thumb">"""
                : $"""<div class="monster-thumb monster-thumb-missing">?</div>""";
            return $"""
              <div class="monster-card">
                {img}
                <div class="monster-name-ja">{m.JaLabel}</div>
                <div class="monster-name-en">{m.EnLabel}</div>
              </div>
            """;
        }));
        monsterSection = $"""
        <section class="section">
          <h2 class="section-title">登場モンスター</h2>
          <div class="monster-grid">{cards}</div>
        </section>
        """;
    }

    const string REVIEW_GUIDE = """

        <!-- REVIEW_START -->
        <!--
          【評価・メモ】
          このコメントブロック全体を削除し、かわりにHTMLを書いてください。
          ビルド（dotnet run --project StS2SiteBuilder）後も上書きされません。

          ▼ テンプレート（コピーして使ってください） ▼

          <section class="section">
            <h2 class="section-title">評価・メモ</h2>
            <p>ここに感想や評価を書く。</p>
          </section>
        -->
        <!-- REVIEW_END -->
        """;

    var reviewZone = review == ""
        ? REVIEW_GUIDE
        : $"""

        <!-- REVIEW_START -->{review}<!-- REVIEW_END -->
        """;

    var content = $"""
        <div class="card-detail-header" style="border-left:5px solid {accent};background:{lightBg}">
          <div class="card-breadcrumb">
            <a href="{basePath}encounters.html" class="char-back-link" style="color:{accent}">エンカウンター一覧</a>
          </div>
          <h1 class="card-title-en" style="color:{accent}">{nameEn}</h1>
          {(nameJa != nameEn ? $"""<div class="card-title-ja">{nameJa}</div>""" : "")}
          <div class="card-badges">{typeBadge}</div>
          <!-- LAST_UPDATED_START -->{(lastUpdated != "" ? $"""<div class="page-updated">最終更新: {lastUpdated}</div>""" : "")}<!-- LAST_UPDATED_END -->
        </div>
        {monsterSection}
        {lossSection}
        {reviewZone}
        {rewardSection}
        """;

    return Layout(nameEn, "encounters", accent, chars, content, basePath);
}

static string FormatLoss(string raw, string encName, bool japanese = false)
{
    if (string.IsNullOrEmpty(raw)) return "";
    var hero = japanese ? "主人公" : "the hero";
    var s = raw.Replace("{character}", hero).Replace("{encounter}", encName);
    return DescriptionFormatter.Clean(s, japanese);
}

// DescriptionFormatter が解決できなかった [VarName] をホバーで変数名が分かるスパンに変換する
static string WrapUnresolvedVars(string html) =>
    UnresolvedVarRegex.Replace(html,
        m => $"<span class=\"game-var\" title=\"ゲームデータ変数: {m.Groups[1].Value}\">?</span>");

static string BuildRelicPage(string relicId, CharData[] chars, bool hasImage = false, string review = "", string lastUpdated = "")
{
    const string basePath = "../";
    var nameEn  = CardDatabaseService.GetRelicTitle(relicId);
    var nameJa  = CardDatabaseService.GetRelicTitle(relicId, japanese: true);
    var rarity  = CardDatabaseService.GetRelicRarity(relicId);
    var stats   = CardDatabaseService.GetRelicStats(relicId);
    var (rawDescEn, rawDescJa) = CardDatabaseService.GetDescription("RELIC." + relicId);
    var descEn = WrapUnresolvedVars(DescriptionFormatter.Resolve(rawDescEn, stats).Replace("\n", "<br>"));
    var descJa = WrapUnresolvedVars(DescriptionFormatter.Resolve(rawDescJa, stats, japanese: true).Replace("\n", "<br>"));
    var flavor = CardDatabaseService.GetFlavor("RELIC." + relicId);

    const string accent  = "#a0600c";
    const string lightBg = "#fff8f0";

    var descSection = descEn != "" ? $"""
        <section class="section">
          <h2 class="section-title">効果テキスト</h2>
          <p class="desc-main">{descEn}</p>
          {(descJa != "" && descJa != descEn ? $"""<p class="desc-sub">{descJa}</p>""" : "")}
        </section>
        """ : "";

    var flavorSection = flavor is { En: var flEn, Ja: var flJa } ? $"""
        <section class="section">
          <h2 class="section-title">フレーバーテキスト</h2>
          <p class="desc-main" style="font-style:italic;color:#888">{flEn}</p>
          {(flJa != "" && flJa != flEn ? $"""<p class="desc-sub" style="font-style:italic">{flJa}</p>""" : "")}
        </section>
        """ : "";

    const string REVIEW_GUIDE = """

        <!-- REVIEW_START -->
        <!--
          【評価・メモ】
          このコメントブロック全体を削除し、かわりにHTMLを書いてください。
          ビルド（dotnet run --project StS2SiteBuilder）後も上書きされません。

          ▼ テンプレート（コピーして使ってください） ▼

          <section class="section">
            <h2 class="section-title">評価・メモ</h2>
            <p>ここに感想や評価を書く。</p>
          </section>
        -->
        <!-- REVIEW_END -->
        """;

    var reviewZone = review == ""
        ? REVIEW_GUIDE
        : $"""

        <!-- REVIEW_START -->{review}<!-- REVIEW_END -->
        """;

    var rarityBadge = rarity != "" ? $"""<span class="badge rarity-{rarity.ToLower()}">{rarity}</span>""" : "";

    var imgHtml = hasImage
        ? $"""<img src="../images/relics/{relicId.ToLowerInvariant()}.png" class="relic-icon" alt="{nameEn}">"""
        : "";

    var content = $"""
        <div class="card-detail-header" style="border-left:5px solid {accent};background:{lightBg}">
          <div class="relic-header-inner">
            {imgHtml}
            <div>
              <div class="card-breadcrumb">
                <a href="{basePath}relics.html" class="char-back-link" style="color:{accent}">レリック一覧</a>
              </div>
              <h1 class="card-title-en" style="color:{accent}">{nameEn}</h1>
              {(nameJa != nameEn ? $"""<div class="card-title-ja">{nameJa}</div>""" : "")}
              {(rarityBadge != "" ? $"""<div class="card-badges">{rarityBadge}</div>""" : "")}
              <!-- LAST_UPDATED_START -->{(lastUpdated != "" ? $"""<div class="page-updated">最終更新: {lastUpdated}</div>""" : "")}<!-- LAST_UPDATED_END -->
            </div>
          </div>
        </div>
        {descSection}
        {reviewZone}
        {flavorSection}
        """;

    return Layout(nameEn, "relics", accent, chars, content, basePath);
}

static string BuildCardPage(string cardId, CharData[] chars, string basePath, bool hasImage = false, string review = "", string lastUpdated = "")
{
    var rawId    = RawId(cardId);
    var nameEn   = CardDatabaseService.GetName(cardId);
    var nameJa   = CardDatabaseService.GetName(cardId, japanese: true);
    var type     = CardDatabaseService.GetCardType(cardId);
    var rarity   = CardDatabaseService.GetCardRarity(cardId);
    var cost     = CardDatabaseService.GetCardCost(cardId);
    var flags    = ComputeFlags(cardId);
    var stats    = CardDatabaseService.GetCardStats(cardId);
    var (rawDescEn, rawDescJa) = CardDatabaseService.GetDescription(cardId);
    var charName = CardDatabaseService.GetCardCharacter(cardId);
    var ch       = chars.FirstOrDefault(c => c.EnName.Equals(charName, StringComparison.OrdinalIgnoreCase));

    var accent  = ch?.Accent  ?? "#888";
    var lightBg = ch?.LightBg ?? "#f8f8f8";

    // テンプレート変数を stats で解決。未定義変数は [VarName] 表示
    var descEn    = DescriptionFormatter.Resolve(rawDescEn, stats).Replace("\n", "<br>");
    var descJa    = DescriptionFormatter.Resolve(rawDescJa, stats, japanese: true).Replace("\n", "<br>");
    var descEnUpg = DescriptionFormatter.Resolve(rawDescEn, stats, upgraded: true).Replace("\n", "<br>");
    var descJaUpg = DescriptionFormatter.Resolve(rawDescJa, stats, japanese: true, upgraded: true).Replace("\n", "<br>");
    var hasUpgrade = descEn != "" && (descEnUpg != descEn || descJaUpg != descJa);

    var typeBadge   = type   != "" ? $"""<span class="badge type-{type.ToLower()}">{type}</span>""" : "";
    var rarityBadge = rarity != "" ? $"""<span class="badge rarity-{rarity.ToLower()}">{rarity}</span>""" : "";
    var costBadge   = cost   != "" ? $"""<span class="badge cost-badge">{cost}</span>""" : "";
    var flagBadges  = string.Concat(
        CardFlags.AllDefs
            .Where(f => flags.Contains(f.Key))
            .Select(f => $"""<span class="badge flag-badge">{f.Label}</span>"""));

    var charLink = ch is not null
        ? $"""<a href="{basePath}{ch.Id}.html" class="char-back-link" style="color:{accent}">{ch.EnName} <span class="char-back-ja">({ch.JaName})</span></a>"""
        : """<span class="char-back-link char-back-neutral">共有・特殊</span>""";

    var charDir  = GetCardDir(cardId, chars);
    var imgHtml  = hasImage
        ? $"""<img src="{basePath}images/cards/{charDir}/{rawId.ToLowerInvariant()}.png" class="card-portrait" alt="{nameEn}">"""
        : "";

    string descSection;
    if (descEn == "")
    {
        descSection = "";
    }
    else if (hasUpgrade)
    {
        var jaBase = descJa != "" && descJa != descEn    ? $"""<p class="desc-sub">{descJa}</p>"""    : "";
        var jaUpg  = descJaUpg != "" && descJaUpg != descEnUpg ? $"""<p class="desc-sub">{descJaUpg}</p>""" : "";
        descSection = $"""
            <section class="section">
              <h2 class="section-title">効果テキスト</h2>
              <div class="tab-bar">
                <button class="tab-btn active" data-tab="base">通常</button>
                <button class="tab-btn" data-tab="upgraded">アップグレード後</button>
              </div>
              <div class="tab-panel" data-panel="base">
                <p class="desc-main">{descEn}</p>
                {jaBase}
              </div>
              <div class="tab-panel hidden" data-panel="upgraded">
                <p class="desc-main">{descEnUpg}</p>
                {jaUpg}
              </div>
            </section>
            """;
    }
    else
    {
        descSection = $"""
            <section class="section">
              <h2 class="section-title">効果テキスト</h2>
              <p class="desc-main">{descEn}</p>
              {(descJa != "" && descJa != descEn ? $"""<p class="desc-sub">{descJa}</p>""" : "")}
            </section>
            """;
    }

    var statsSection = "";
    if (stats is { Count: > 0 })
    {
        var rows = string.Concat(stats.Select(kv =>
            $"""<tr><td class="stat-key">{kv.Key}</td><td class="stat-val">{kv.Value}</td></tr>"""));
        statsSection = $"""
            <section class="section">
              <h2 class="section-title">基本値</h2>
              <table class="stat-table"><tbody>{rows}</tbody></table>
            </section>
            """;
    }

    var flagsSection = flags.Count > 0 ? $"""
        <section class="section">
          <h2 class="section-title">特性</h2>
          <div class="flag-cell">{flagBadges}</div>
        </section>
        """ : "";

    // マーカー区間：ビルドで上書きされない手書きセクション
    const string REVIEW_GUIDE = """

        <!-- REVIEW_START -->
        <!--
          【評価・メモ】
          このコメントブロック全体を削除し、かわりにHTMLを書いてください。
          ビルド（dotnet run --project StS2SiteBuilder）後も上書きされません。

          ▼ テンプレート（コピーして使ってください） ▼

          <section class="section">
            <h2 class="section-title">評価・メモ</h2>
            <p>ここに感想や評価を書く。</p>
          </section>

          ▼ 使えるCSSクラス ▼

          テキスト段落：
            <p>テキスト</p>

          キー/値テーブル：
            <table class="stat-table">
              <tr><td class="stat-key">評価</td><td class="stat-val">A</td></tr>
              <tr><td class="stat-key">習得時期</td><td class="stat-val">序盤</td></tr>
            </table>

          タグ・バッジ：
            <span class="mec-tag">Strength</span>
            <span class="badge rarity-rare">Rare</span>
        -->
        <!-- REVIEW_END -->
        """;

    var reviewZone = review == ""
        ? REVIEW_GUIDE
        : $"""

        <!-- REVIEW_START -->{review}<!-- REVIEW_END -->
        """;

    var content = $"""
        <div class="card-detail-header" style="border-left:5px solid {accent};background:{lightBg}">
          <div class="card-header-inner">
            {imgHtml}
            <div>
              <div class="card-breadcrumb">{charLink}</div>
              <h1 class="card-title-en" style="color:{accent}">{nameEn}</h1>
              {(nameJa != nameEn ? $"""<div class="card-title-ja">{nameJa}</div>""" : "")}
              <div class="card-badges">{typeBadge}{rarityBadge}{costBadge}</div>
              <!-- LAST_UPDATED_START -->{(lastUpdated != "" ? $"""<div class="page-updated">最終更新: {lastUpdated}</div>""" : "")}<!-- LAST_UPDATED_END -->
            </div>
          </div>
        </div>
        {descSection}
        {reviewZone}
        {statsSection}
        {flagsSection}
        """;

    const string UPGRADE_TAB_JS = """
        <script>
        (function() {
          document.querySelectorAll('.tab-bar').forEach(function(bar) {
            bar.querySelectorAll('.tab-btn').forEach(function(btn) {
              btn.addEventListener('click', function() {
                var section = bar.closest('.section');
                var tab = btn.dataset.tab;
                bar.querySelectorAll('.tab-btn').forEach(function(b) {
                  b.classList.toggle('active', b === btn);
                });
                section.querySelectorAll('.tab-panel').forEach(function(p) {
                  p.classList.toggle('hidden', p.dataset.panel !== tab);
                });
              });
            });
          });
        })();
        </script>
        """;

    var extraFoot = hasUpgrade ? UPGRADE_TAB_JS : "";
    return Layout(nameEn, "cards", accent, chars, content, basePath, extraFoot: extraFoot);
}

static string BuildCharPage(CharData ch, CharData[] chars, (string En, string Ja, string MecDir)[] mecs)
{
    var mecHtml = mecs.Length > 0
        ? $"""<div class="mec-tags">{string.Concat(mecs.Select(m =>
            m.En == m.Ja
                ? $"""<a href="mechanics/{m.MecDir}/{MecFileName(m.En)}" class="mec-tag">{m.En}</a>"""
                : $"""<a href="mechanics/{m.MecDir}/{MecFileName(m.En)}" class="mec-tag">{m.Ja}<span class="mec-sub">{m.En}</span></a>"""))}</div>"""
        : """<p class="placeholder">メカニクス情報なし</p>""";

    return Layout(ch.EnName, ch.Id, ch.Accent, chars, $"""
        <div class="char-header" style="border-left:5px solid {ch.Accent};background:{ch.LightBg}">
          <div class="char-header-body">
            <h1 class="char-title-en" style="color:{ch.Accent}">{ch.EnName}</h1>
            <div class="char-title-ja">{ch.JaName}</div>
            <p class="char-desc-full"><!-- REVIEW_START -->{ch.Desc}<!-- REVIEW_END --></p>
          </div>
          <img src="images/characters/{ch.Id}.jpg" class="char-hero-img" alt="{ch.EnName}">
        </div>
        <section class="section">
          <h2 class="section-title">メカニクス / シナジー</h2>
          {mecHtml}
        </section>
        <section class="section">
          <h2 class="section-title">カード一覧</h2>
          <a href="cards.html?char={ch.Id}" class="pagelist-list-link">{ch.JaName}のカード一覧を見る →</a>
        </section>
        """);
}

static string MecFileName(string enLabel) =>
    Regex.Replace(enLabel, @"[^A-Za-z0-9]+", "_").Trim('_') + ".html";

static string BuildMechanicListPage(CharData[] chars)
{
    const string basePath = "./";
    const string accent   = "#4a5568";
    var totalCount = CharacterMechanics.All.Sum(g => g.Mechanics.Length);

    var sections = string.Concat(
        CharacterMechanics.All
            .Where(g => g.Mechanics.Length > 0)
            .Select(group =>
            {
                var ch       = chars.FirstOrDefault(c => c.EnName == group.EnLabel);
                var grpAccent = ch?.Accent ?? "#4a5568";
                var grpBg    = ch?.LightBg ?? "#f5f6f8";
                var titleLink = ch != null
                    ? $"""<a href="{basePath}{ch.Id}.html" class="char-back-link" style="color:{grpAccent}">{group.JaLabel}</a>"""
                    : $"""<span style="color:{grpAccent}">{group.JaLabel}</span>""";
                var tags = string.Concat(group.Mechanics.Select(mec =>
                {
                    var href = $"mechanics/{group.EnLabel.ToLowerInvariant()}/{MecFileName(mec.EnLabel)}";
                    return mec.EnLabel == mec.JaLabel
                        ? $"""<a href="{href}" class="mec-tag">{mec.EnLabel}</a>"""
                        : $"""<a href="{href}" class="mec-tag">{mec.JaLabel}<span class="mec-sub">{mec.EnLabel}</span></a>""";
                }));
                return $"""
                    <section class="section" style="border-left:3px solid {grpAccent}">
                      <h2 class="section-title">{titleLink}</h2>
                      <div class="mec-tags">{tags}</div>
                    </section>
                    """;
            }));

    return Layout("メカニクス一覧", "mechanics", accent, chars, $"""
        <div class="page-hero">
          <div class="hero-title">メカニクス一覧</div>
          <div class="hero-sub">Mechanic List</div>
          <div class="hero-desc">全{totalCount}件のメカニクスをキャラクター別に表示。</div>
        </div>
        {sections}
        """, basePath);
}

static string BuildMechanicPage(CharGroup group, MechanicDef mec, string[] allCardIds, CharData[] chars, string review = "", string lastUpdated = "")
{
    const string basePath = "../../";
    var ch       = chars.FirstOrDefault(c => c.EnName == group.EnLabel);
    var accent   = ch?.Accent ?? "#4a5568";
    var lightBg  = ch?.LightBg ?? "#f5f6f8";
    var charDir  = group.EnLabel.ToLowerInvariant();

    var crumbChar = ch != null
        ? $"""<a href="{basePath}{ch.Id}.html" class="char-back-link" style="color:{accent}">{group.JaLabel}</a>"""
        : $"""<span class="char-back-neutral">{group.JaLabel}</span>""";

    static int RarityOrd(string r) => r.ToLower() switch
    {
        "starter"  => 0, "common"   => 1, "uncommon" => 2, "rare"  => 3,
        "ancient"  => 4, "event"    => 5, "shop"     => 6, _       => 99,
    };

    var matchingCards = allCardIds
        .Where(id => mec.Filter(id))
        .Select(id =>
        {
            var nameEn   = CardDatabaseService.GetName(id);
            var nameJa   = CardDatabaseService.GetName(id, japanese: true);
            var type     = CardDatabaseService.GetCardType(id);
            var rarity   = CardDatabaseService.GetCardRarity(id);
            var cost     = CardDatabaseService.GetCardCost(id);
            var cDir     = GetCardDir(id, chars);
            var chData   = chars.FirstOrDefault(c => c.Id == cDir);
            var charLabel = chData?.JaName ?? (cDir == "shared" ? "共有・特殊" : cDir);
            return (Id: id, NameEn: nameEn, NameJa: nameJa, Type: type, Rarity: rarity, Cost: cost, CDir: cDir, CharId: cDir, CharLabel: charLabel);
        })
        .OrderBy(c => RarityOrd(c.Rarity))
        .ThenBy(c => c.NameEn)
        .ToList();

    var cardSections = string.Concat(
        matchingCards
            .GroupBy(c => c.Rarity)
            .OrderBy(g => RarityOrd(g.Key))
            .Select(g =>
            {
                var rarity = g.Key;
                var rows   = string.Concat(g.Select(c =>
                {
                    var jaSpan    = c.NameJa != c.NameEn ? $"""<span class="card-name-ja">{c.NameJa}</span>""" : "";
                    var typeBadge = c.Type   != ""       ? $"""<span class="badge type-{c.Type.ToLower()}">{c.Type}</span>""" : "";
                    var costBadge = c.Cost   != ""       ? $"""<span class="badge cost-badge">{c.Cost}</span>""" : "";
                    var charBadge = $"""<span class="badge char-{c.CharId}">{c.CharLabel}</span>""";
                    var href      = $"{basePath}cards/{c.CDir}/{RawId(c.Id)}.html";
                    return $"""
                              <tr>
                                <td class="col-name"><a href="{href}" class="card-name-link">{c.NameEn}</a>{jaSpan}</td>
                                <td class="col-type">{typeBadge}</td>
                                <td class="col-cost">{costBadge}</td>
                                <td class="col-char">{charBadge}</td>
                              </tr>
                        """;
                }));
                var rarityBadge = $"""<span class="badge rarity-{rarity.ToLower()}">{rarity}</span>""";
                return $"""
                    <section class="section">
                      <h2 class="section-title">
                        {rarityBadge}
                        <span class="section-meta">{g.Count()}枚</span>
                      </h2>
                      <table class="card-table">
                        <thead><tr>
                          <th>カード名</th><th>タイプ</th><th>コスト</th><th>キャラ</th>
                        </tr></thead>
                        <tbody>{rows}</tbody>
                      </table>
                    </section>
                    """;
            }));

    var noCards = matchingCards.Count == 0
        ? """<section class="section"><p class="placeholder">該当カードなし</p></section>"""
        : "";

    const string REVIEW_GUIDE = """

        <!-- REVIEW_START -->
        <!--
          【評価・メモ】
          このコメントブロック全体を削除し、かわりにHTMLを書いてください。

          ▼ テンプレート ▼
          <section class="section">
            <h2 class="section-title">評価・メモ</h2>
            <p>ここに感想や評価を書く。</p>
          </section>
        -->
        <!-- REVIEW_END -->
        """;

    var reviewZone = review == ""
        ? REVIEW_GUIDE
        : $"""

        <!-- REVIEW_START -->{review}<!-- REVIEW_END -->
        """;

    var content = $"""
        <div class="card-detail-header" style="border-left:5px solid {accent};background:{lightBg}">
          <div class="card-breadcrumb">
            {crumbChar}
            <span style="color:#bbb"> / メカニクス</span>
          </div>
          <h1 class="card-title-en" style="color:{accent}">{(mec.EnLabel == mec.JaLabel ? mec.EnLabel : mec.JaLabel)}</h1>
          {(mec.EnLabel != mec.JaLabel ? $"""<div class="mec-title-en">{mec.EnLabel}</div>""" : "")}
          <!-- LAST_UPDATED_START -->{(lastUpdated != "" ? $"""<div class="page-updated">最終更新: {lastUpdated}</div>""" : "")}<!-- LAST_UPDATED_END -->
        </div>
        {reviewZone}
        {cardSections}
        {noCards}
        """;

    return Layout(mec.JaLabel, "mechanics", accent, chars, content, basePath,
                  extraHead: BuildCharCss(chars));
}

static HashSet<string> ComputeFlags(string id)
{
    var type   = CardDatabaseService.GetCardType(id);
    var rarity = CardDatabaseService.GetCardRarity(id);
    var flags  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (rarity == "Ancient")         flags.Add(CardFlags.IsAncient);
    if (type is "Status" or "Curse") flags.Add(CardFlags.IsGeneratedInCombat);
    return flags;
}

static int TypeOrder(string type) => type switch
{
    "Attack" => 0, "Skill" => 1, "Power" => 2,
    "Status" => 3, "Curse" => 4, "Quest" => 5,
    _ => 99,
};

internal static CharData[] GetBaseChars() =>
[
    new("ironclad",    "Ironclad",    "アイアンクラッド", "#c0392b", "#fde8e8", ""),
    new("silent",      "Silent",      "サイレント",       "#1a7a4a", "#e8f8f0", ""),
    new("defect",      "Defect",      "ディフェクト",     "#1a5799", "#e8f0fc", ""),
    new("necrobinder", "Necrobinder", "ネクロバインダー", "#6c3483", "#f4ecf7", ""),
    new("regent",      "Regent",      "リージェント",     "#7d6608", "#fdf8e8", ""),
];

static int RarityOrder(string rarity) => rarity switch
{
    "Starter" => 0, "Common" => 1, "Uncommon" => 2, "Rare" => 3,
    "Ancient" => 4, "Event"  => 5, "Shop"     => 6,
    _ => 99,
};

static string BuildCharCss(CharData[] chars) => $$"""
    <style>
    .col-char { white-space: nowrap; }
    {{string.Concat(chars.Select(ch => $".char-{ch.Id}{{background:{ch.LightBg};color:{ch.Accent}}}\n"))}}
    .char-shared { background: #f0f0f0; color: #888; }
    </style>
    """;

static string Layout(string title, string activeId, string accent, CharData[] chars, string content,
                     string basePath = "", string extraHead = "", string extraFoot = "", bool wikiLink = true)
{
    const string CSS = """
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
        body {
          font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Helvetica Neue', Arial,
                       'Hiragino Sans', 'Noto Sans JP', sans-serif;
          background: #f5f7fa; color: #2c2c2c; line-height: 1.6;
        }
        a { color: inherit; text-decoration: none; }
        .layout { display: flex; min-height: 100vh; }

        /* ── Sidebar ── */
        .sidebar {
          width: 240px; min-width: 240px; background: #fff;
          border-right: 1px solid #e8e8e8; display: flex; flex-direction: column;
          position: sticky; top: 0; height: 100vh; overflow-y: auto;
        }
        .sidebar-brand { padding: 18px 20px 16px; background: #1e2128; border-bottom: 1px solid #2c3040; }
        .sidebar-buildtime { padding: 10px 20px 14px; font-size: 10.5px; color: #b0b0b0; margin-top: auto; border-top: 1px solid #f0f0f0; }
        .brand-game  { font-size: 13px; font-weight: 700; color: #f0f0f0; letter-spacing: 0.3px; }
        .brand-label { font-size: 11px; color: #8899aa; margin-top: 3px; }
        .nav-section { padding: 14px 0 6px; }
        .nav-group-label {
          font-size: 10px; font-weight: 600; text-transform: uppercase;
          letter-spacing: 1px; color: #c0c0c0; padding: 0 20px 8px;
        }
        .nav-link {
          display: flex; align-items: center; gap: 9px;
          padding: 8px 20px; font-size: 13.5px; color: #555;
          border-left: 3px solid transparent; transition: background 0.1s;
        }
        .nav-link:hover  { background: #f7f8fa; color: #222; }
        .nav-link.active { background: #f3f4f6; color: #111; font-weight: 600; }
        .nav-dot  { width: 9px; height: 9px; border-radius: 50%; flex-shrink: 0; }
        .nav-icon { font-size: 15px; line-height: 1; }
        .nav-name-ja { font-size: 11px; color: #aaa; margin-left: auto; }
        .nav-link.active .nav-name-ja { color: #888; }

        /* ── Main ── */
        .main { flex: 1; padding: 40px 48px; min-width: 0; }

        /* ── Hero ── */
        .page-hero { margin-bottom: 32px; padding-bottom: 24px; border-bottom: 1px solid #e8e8e8; }
        .hero-title { font-size: 28px; font-weight: 800; color: #1a1a2e; letter-spacing: -0.5px; }
        .hero-sub   { font-size: 15px; color: #666; margin-top: 4px; }
        .hero-desc  { font-size: 13.5px; color: #999; margin-top: 10px; }

        /* ── Character grid ── */
        .char-grid {
          display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 16px;
        }
        .char-card {
          display: flex; flex-direction: column; background: #fff;
          border-radius: 10px; overflow: hidden;
          box-shadow: 0 1px 4px rgba(0,0,0,0.08); transition: box-shadow 0.15s, transform 0.15s;
        }
        .char-card:hover { box-shadow: 0 6px 20px rgba(0,0,0,0.13); transform: translateY(-3px); }
        .char-card-header { padding: 20px 18px 14px; color: #fff; }
        .char-name-en     { font-size: 19px; font-weight: 800; letter-spacing: -0.3px; }
        .char-name-ja     { font-size: 11px; opacity: 0.82; margin-top: 3px; }
        .char-card-body   { padding: 14px 18px; flex: 1; }
        .char-desc        { font-size: 12.5px; color: #666; line-height: 1.65; }
        .char-card-footer {
          padding: 9px 18px 11px; font-size: 12px; color: #aaa;
          border-top: 1px solid #f0f0f0; font-weight: 500;
        }
        .char-card:hover .char-card-footer { color: #666; }

        /* ── Character page header ── */
        .char-header {
          border-radius: 10px; padding: 28px 32px; margin-bottom: 24px;
          display: flex; align-items: center; gap: 28px; overflow: hidden;
        }
        .char-header-body { flex: 1; min-width: 0; }
        .char-title-en  { font-size: 30px; font-weight: 800; letter-spacing: -0.5px; }
        .char-title-ja  { font-size: 13px; color: #777; margin-top: 5px; }
        .char-desc-full { font-size: 14px; color: #555; margin-top: 14px; max-width: 560px; line-height: 1.75; }
        .char-hero-img  {
          width: 132px; height: 195px; object-fit: cover; border-radius: 8px;
          flex-shrink: 0; box-shadow: 0 2px 8px rgba(0,0,0,0.15); image-rendering: auto;
        }

        /* ── Relic detail header ── */
        .relic-header-inner { display: flex; align-items: center; gap: 20px; }
        .relic-icon {
          width: 80px; height: 80px; object-fit: contain;
          border-radius: 6px; flex-shrink: 0; box-shadow: 0 2px 8px rgba(0,0,0,0.12);
        }

        /* ── Card detail header ── */
        .card-header-inner { display: flex; align-items: flex-start; gap: 20px; }
        .card-portrait {
          width: 200px; height: auto; object-fit: contain;
          border-radius: 6px; flex-shrink: 0; box-shadow: 0 2px 8px rgba(0,0,0,0.12);
        }

        /* ── 全ページ一覧 ── */
        .pagelist-sub { margin-top: 14px; }
        .pagelist-sub-title {
          font-size: 11.5px; font-weight: 700; color: #999; letter-spacing: 0.05em;
          text-transform: uppercase; margin-bottom: 6px;
        }
        .pagelist-top-links { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 4px; }
        .pagelist-list-link {
          display: inline-block; padding: 5px 14px;
          background: #e8f0fe; border-radius: 6px; font-size: 13px;
          color: #1a5799; font-weight: 600;
        }
        .pagelist-list-link:hover { background: #d0e3ff; }

        /* ── Event detail ── */
        .event-image-wrap {
          margin-bottom: 20px; border-radius: 10px; overflow: hidden;
          box-shadow: 0 1px 3px rgba(0,0,0,0.08);
        }
        .event-image { width: 100%; max-height: 360px; object-fit: cover; display: block; }
        .event-options { display: flex; flex-direction: column; gap: 12px; }
        .event-option {
          border: 1.5px solid #e8e8e8; border-radius: 8px; padding: 14px 18px;
          background: #fafafa;
        }
        .event-option-title { font-weight: 700; font-size: 14px; color: #222; }
        .event-option-title-ja { font-size: 11px; color: #999; margin-left: 6px; font-weight: 400; }
        .event-option-desc { font-size: 13px; color: #444; line-height: 1.75; margin-top: 6px; }
        .event-option-desc-ja { font-size: 12px; color: #888; line-height: 1.75; margin-top: 4px; }

        /* ── Card detail header ── */
        .card-detail-header {
          border-radius: 10px; padding: 28px 32px; margin-bottom: 24px; overflow: hidden;
        }
        .card-breadcrumb  { font-size: 12px; margin-bottom: 10px; }
        .char-back-link   { font-weight: 600; }
        .char-back-link:hover { text-decoration: underline; }
        .char-back-ja     { font-weight: 400; opacity: 0.8; }
        .char-back-neutral { color: #888; }
        .card-title-en  { font-size: 30px; font-weight: 800; letter-spacing: -0.5px; }
        .card-title-ja  { font-size: 13px; color: #777; margin-top: 5px; }
        .mec-title-en   { font-size: 13px; color: #777; margin-top: 5px; }
        .card-badges    { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 14px; }
        .page-updated   { font-size: 11px; color: #b0b8c0; margin-top: 8px; font-variant-numeric: tabular-nums; }
        .run-win    { background:#27ae60; color:#fff; border-radius:4px; padding:2px 8px; font-size:12px; }
        .run-lose   { background:#c0392b; color:#fff; border-radius:4px; padding:2px 8px; font-size:12px; }
        .run-abandon{ background:#7f8c8d; color:#fff; border-radius:4px; padding:2px 8px; font-size:12px; }
        .node-badge   { border-radius:4px; padding:1px 6px; font-size:11px; font-weight:600; margin-right:6px; }
        .node-monster { background:#fee8e8; color:#a00; }
        .node-elite   { background:#f0e6ff; color:#600; }
        .node-boss    { background:#ffecd0; color:#a30; }
        .node-shop    { background:#e8f5e9; color:#2a6; }
        .node-treasure{ background:#fff8e1; color:#996; }
        .node-rest_site{background:#e3f2fd; color:#166; }
        .node-ancient { background:#ede7f6; color:#512da8; }
        .node-unknown { background:#f5f5f5; color:#888; }
        .floor-row  { display:flex; align-items:baseline; gap:10px; padding:6px 0; border-bottom:1px solid #f0f0f0; font-size:13px; }
        .auto-link { color:#1a5799; }
        .auto-link:hover { text-decoration:underline; }
        .floor-hp   { font-size:12px; color:#888; white-space:nowrap; }
        .floor-picked { color:#1a5799; font-weight:600; }
        .floor-skipped{ color:#bbb; text-decoration:line-through; }
        .run-summary-grid { display:flex; gap:24px; flex-wrap:wrap; margin:16px 0; }
        .run-stat   { display:flex; flex-direction:column; align-items:center; }
        .run-stat-val { font-size:24px; font-weight:700; }
        .run-stat-label { font-size:11px; color:#888; }
        .desc-main   { font-size: 14px; color: #333; line-height: 1.75; }
        .desc-sub   { font-size: 13px; color: #777; line-height: 1.75; margin-top: 12px; }
        .game-var   { font-style: italic; color: #aaa; background: rgba(0,0,0,0.05); padding: 0 3px; border-radius: 3px; cursor: help; font-size: 0.9em; }
        .stat-table     { border-collapse: collapse; }
        .stat-key { font-size: 13px; color: #666; padding: 3px 20px 3px 0; }
        .stat-val { font-size: 13px; font-weight: 600; color: #222; }

        /* ── Sections ── */
        .section {
          background: #fff; border-radius: 10px; padding: 24px 28px;
          margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .section-title {
          display: flex; align-items: center;
          font-size: 14px; font-weight: 700; color: #333;
          margin-bottom: 16px; padding-bottom: 10px;
          border-bottom: 1px solid #f0f0f0; letter-spacing: 0.2px;
        }
        .section-dot  { width: 10px; height: 10px; border-radius: 50%; margin-right: 8px; flex-shrink: 0; }
        .section-meta { font-size: 12px; font-weight: 400; color: #bbb; margin-left: 8px; }
        .mec-tags { display: flex; flex-wrap: wrap; gap: 8px; }
        .mec-tag  {
          display: inline-block; padding: 5px 13px; background: #f5f6f8; border: 1px solid #e4e6ea;
          border-radius: 20px; font-size: 13px; color: #444; text-decoration: none;
        }
        a.mec-tag:hover { background: #eaecf0; border-color: #c8ccd2; }
        .mec-sub  { display: block; font-size: 10.5px; color: #999; margin-top: 1px; }
        .placeholder { font-size: 13.5px; color: #bbb; font-style: italic; }

        /* ── Page list ── */
        .section-pending { opacity: 0.55; }
        .pending-badge {
          display: inline-block; font-size: 10px; font-weight: 600;
          background: #f0f0f0; color: #aaa; border-radius: 10px;
          padding: 2px 8px; margin-left: 8px; vertical-align: middle;
          letter-spacing: 0.5px; font-style: normal;
        }

        /* ── Monster grid ── */
        .monster-grid { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 8px; }
        .monster-card { display: flex; flex-direction: column; align-items: center; width: 120px; }
        .monster-thumb { width: 100px; height: 100px; object-fit: contain;
          background: #1e1e23; border-radius: 6px; border: 1px solid #ddd; }
        .monster-thumb-missing { width: 100px; height: 100px; display: flex; align-items: center;
          justify-content: center; background: #f0f0f0; border-radius: 6px;
          border: 1px dashed #ccc; color: #aaa; font-size: 28px; }
        .monster-name-ja { font-size: 12px; font-weight: 600; text-align: center; margin-top: 4px; }
        .monster-name-en { font-size: 10px; color: #888; text-align: center; }

        /* ── Card table ── */
        .card-table { width: 100%; border-collapse: collapse; font-size: 13px; }
        .card-table th {
          text-align: left; font-size: 10.5px; font-weight: 600;
          text-transform: uppercase; letter-spacing: 0.5px; color: #bbb;
          padding: 0 16px 8px 0; border-bottom: 1px solid #f0f0f0;
        }
        .card-table td { padding: 5px 16px 5px 0; border-bottom: 1px solid #f8f8f8; vertical-align: middle; }
        .card-table tr:last-child td { border-bottom: none; }
        .col-name   { min-width: 180px; }
        .col-type   { white-space: nowrap; }
        .col-rarity { white-space: nowrap; }
        .card-name-link { color: #1a5799; }
        .card-name-link:hover { text-decoration: underline; }
        .card-name-ja   { font-size: 11px; color: #bbb; margin-left: 6px; }
        .flag-cell  { display: flex; gap: 4px; flex-wrap: wrap; }

        /* ── Badges ── */
        .badge {
          display: inline-block; padding: 2px 7px;
          border-radius: 4px; font-size: 11px; font-weight: 600; white-space: nowrap;
        }
        .type-attack    { background: #fde8e8; color: #c0392b; }
        .type-skill     { background: #e8f0fc; color: #1a5799; }
        .type-power     { background: #fdf8e8; color: #7d6608; }
        .type-status    { background: #f0f0f0; color: #777; }
        .type-curse     { background: #f4ecf7; color: #6c3483; }
        .type-quest     { background: #e8f8f0; color: #1a7a4a; }
        .rarity-starter  { background: #efefef;  color: #999; }
        .rarity-common   { background: #e8eaed;  color: #555; }
        .rarity-uncommon { background: #dde8f5;  color: #1a5799; }
        .rarity-rare     { background: #fde8e8;  color: #c0392b; }
        .rarity-ancient  { background: #fff0d8;  color: #a0600c; }
        .rarity-event    { background: #e8f8f0;  color: #1a7a4a; }
        .rarity-shop     { background: #fdf0fc;  color: #7d1a7d; }
        .cost-badge      { background: #e8eaed;  color: #333; }
        .flag-badge      { background: #e8eaf0;  color: #445566; }
        .enc-boss   { background: #fde8e8; color: #b03030; }
        .enc-elite  { background: #f4ecf7; color: #6c3483; }
        .enc-normal { background: #dde8f5; color: #1a5799; }
        .enc-weak   { background: #e8eaed; color: #555; }
        .enc-event  { background: #eef6f8; color: #1a6678; }
        .enc-other  { background: #f0f0f0; color: #777; }
        .wiki-link { display: inline-block; margin-top: 12px; font-size: 12.5px; color: #1a5799; }
        .wiki-link:hover { text-decoration: underline; }

        /* ── Changelog list ── */
        .changelog-list { list-style: none; margin: 0; padding: 0; }
        .cl-entry { display: flex; align-items: baseline; gap: 12px; padding: 10px 0; border-bottom: 1px solid #f0f0f0; font-size: 14px; line-height: 1.6; }
        .cl-entry:last-child { border-bottom: none; }
        .cl-date { font-size: 12px; color: #999; white-space: nowrap; flex-shrink: 0; font-variant-numeric: tabular-nums; }
        .cl-link { color: #1a5799; }
        .cl-link:hover { text-decoration: underline; }

        /* ── Markdown body ── */
        .md-body p { font-size: 14px; color: #333; line-height: 1.75; margin-top: 1em; }
        .md-body p:first-child { margin-top: 0; }
        .md-body ul { margin: 1em 0 0 1.5em; font-size: 14px; color: #333; line-height: 1.75; }
        .md-body ul:first-child { margin-top: 0; }
        .md-body li + li { margin-top: 0.3em; }
        .md-body a { color: #1a5799; }
        .md-body a:hover { text-decoration: underline; }

        /* ── Upgrade tabs ── */
        .tab-bar { display: flex; gap: 6px; margin-bottom: 14px; }
        .tab-btn {
          padding: 4px 14px; border: 1.5px solid #ddd; border-radius: 16px;
          font-size: 12px; font-weight: 600; cursor: pointer; background: #fff;
          color: #999; font-family: inherit; transition: all 0.15s;
        }
        .tab-btn.active { border-color: #4a90d9; color: #1a5799; background: #eef3fc; }
        .tab-panel.hidden { display: none; }
        """;

    const string MD_JS = """
        <script>
        (function () {
          function esc(s) {
            return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
          }
          function inline(s) {
            return esc(s)
              .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
              .replace(/\*(.+?)\*/g, '<em>$1</em>')
              .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
          }
          function renderMd(src) {
            return src.trim().split(/\n[ \t]*\n/).map(function (block) {
              block = block.trim();
              if (!block) return '';
              var lines = block.split('\n');
              if (/^[-*]\s/.test(lines[0])) {
                return '<ul>' + lines
                  .filter(function (l) { return /^[-*]\s/.test(l); })
                  .map(function (l) { return '<li>' + inline(l.replace(/^[-*]\s+/, '')) + '</li>'; })
                  .join('') + '</ul>';
              }
              return '<p>' + inline(block.replace(/\n/g, ' ')) + '</p>';
            }).join('');
          }
          document.querySelectorAll('script[type="text/markdown"]').forEach(function (el) {
            var d = document.createElement('div');
            d.className = 'md-body';
            d.innerHTML = renderMd(el.textContent);
            el.parentNode.replaceChild(d, el);
          });
        })();
        </script>
        """;

    var homeActive   = activeId == "index";
    var homeStyle    = homeActive   ? " style=\"border-left-color:#4a90d9\"" : "";
    var homeClass    = homeActive   ? " active" : "";
    var aboutActive     = activeId == "about";
    var aboutStyle      = aboutActive     ? " style=\"border-left-color:#4a90d9\"" : "";
    var aboutClass      = aboutActive     ? " active" : "";
    var changelogActive = activeId == "changelog";
    var changelogStyle  = changelogActive ? " style=\"border-left-color:#4a90d9\"" : "";
    var changelogClass  = changelogActive ? " active" : "";
    var pagesActive  = activeId == "pages";
    var pagesStyle   = pagesActive  ? " style=\"border-left-color:#4a90d9\"" : "";
    var pagesClass   = pagesActive  ? " active" : "";
    var cardsActive  = activeId == "cards";
    var cardsStyle   = cardsActive  ? " style=\"border-left-color:#4a90d9\"" : "";
    var cardsClass   = cardsActive  ? " active" : "";
    var relicsActive = activeId == "relics";
    var relicsStyle  = relicsActive ? " style=\"border-left-color:#a0600c\"" : "";
    var relicsClass  = relicsActive ? " active" : "";
    var eventsActive     = activeId == "events";
    var eventsStyle      = eventsActive     ? " style=\"border-left-color:#1a6678\"" : "";
    var eventsClass      = eventsActive     ? " active" : "";
    var encountersActive = activeId == "encounters";
    var encountersStyle  = encountersActive ? " style=\"border-left-color:#8b2222\"" : "";
    var encountersClass  = encountersActive ? " active" : "";
    var mecsActive  = activeId == "mechanics";
    var mecsStyle   = mecsActive  ? " style=\"border-left-color:#4a5568\"" : "";
    var mecsClass   = mecsActive  ? " active" : "";
    var runsActive  = activeId == "runs";
    var runsStyle   = runsActive  ? " style=\"border-left-color:#4a90d9\"" : "";
    var runsClass   = runsActive  ? " active" : "";
    var articlesActive = activeId == "articles";
    var articlesStyle  = articlesActive ? " style=\"border-left-color:#4a90d9\"" : "";
    var articlesClass  = articlesActive ? " active" : "";
    var charsActive = activeId == "characters" || chars.Any(c => c.Id == activeId);
    var charsAccent = chars.FirstOrDefault(c => c.Id == activeId)?.Accent ?? "#4a90d9";
    var charsStyle  = charsActive ? $" style=\"border-left-color:{charsAccent}\"" : "";
    var charsClass  = charsActive ? " active" : "";

    return $"""
        <!DOCTYPE html>
        <html lang="ja">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>{title} | Slay the Spire 2 攻略メモメモ</title>
          <link rel="icon" type="image/png" href="{basePath}favicon.png">
          <style>
        {CSS}
          </style>
          {extraHead}
        </head>
        <body>
          <div class="layout">
            <nav class="sidebar">
              <div class="sidebar-brand">
                <div class="brand-game">Slay the Spire 2</div>
                <div class="brand-label">攻略メモメモ</div>
              </div>
              <div class="nav-section">
                <div class="nav-group-label">ページ</div>
                <a href="{basePath}index.html" class="nav-link{homeClass}"{homeStyle}>
                  <span class="nav-icon">&#8962;</span>トップ
                </a>
                <a href="{basePath}about.html" class="nav-link{aboutClass}"{aboutStyle}>
                  <span class="nav-icon">&#9432;</span>このサイトについて
                </a>
                <a href="{basePath}changelog.html" class="nav-link{changelogClass}"{changelogStyle}>
                  <span class="nav-icon">&#9711;</span>更新履歴
                </a>
                <a href="{basePath}articles.html" class="nav-link{articlesClass}"{articlesStyle}>
                  <span class="nav-icon">&#9997;</span>記事
                </a>
                <a href="{basePath}pages.html" class="nav-link{pagesClass}"{pagesStyle}>
                  <span class="nav-icon">&#9776;</span>全ページ一覧
                </a>
                <a href="{basePath}characters.html" class="nav-link{charsClass}"{charsStyle}>
                  <span class="nav-icon">&#9786;</span>キャラクター一覧
                </a>
                <a href="{basePath}cards.html" class="nav-link{cardsClass}"{cardsStyle}>
                  <span class="nav-icon">&#9670;</span>カード一覧
                </a>
                <a href="{basePath}relics.html" class="nav-link{relicsClass}"{relicsStyle}>
                  <span class="nav-icon">&#9671;</span>レリック一覧
                </a>
                <a href="{basePath}events.html" class="nav-link{eventsClass}"{eventsStyle}>
                  <span class="nav-icon">&#9830;</span>イベント一覧
                </a>
                <a href="{basePath}encounters.html" class="nav-link{encountersClass}"{encountersStyle}>
                  <span class="nav-icon">&#9876;</span>エンカウンター一覧
                </a>
                <a href="{basePath}mechanics.html" class="nav-link{mecsClass}"{mecsStyle}>
                  <span class="nav-icon">&#9881;</span>メカニクス一覧
                </a>
                <a href="{basePath}runs.html" class="nav-link{runsClass}"{runsStyle}>
                  <span class="nav-icon">&#9654;</span>ラン一覧
                </a>
              </div>
              <div class="sidebar-buildtime">ビルド日: {DateTime.Now:yyyy-MM-dd}</div>
            </nav>
            <main class="main">
              {content}
            </main>
          </div>
          {MD_JS}
          {(wikiLink ? $"""<script src="{basePath}wiki-link.js"></script>""" : "")}
          {extraFoot}
        </body>
        </html>
        """;
}

static string? FindToolsRoot(string startDir)
{
    var dir = startDir;
    while (dir != null)
    {
        var candidate = Path.Combine(dir, "tools", "extracted");
        if (Directory.Exists(candidate)) return candidate;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static string? FindArticlesDir(string distDir)
{
    // Try repo root (grandparent of dist) first, then sibling of dist as fallback
    var distParent = Path.GetDirectoryName(distDir)!;
    string[] candidates =
    [
        Path.Combine(Path.GetDirectoryName(distParent)!, "articles"),
        Path.Combine(distParent, "articles"),
    ];
    return candidates.FirstOrDefault(Directory.Exists);
}

static string ExtractMetaField(string line, string key)
{
    var search = key + ":";
    var idx = line.IndexOf(search, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return "";
    var start = idx + search.Length;
    while (start < line.Length && line[start] == ' ') start++;
    var end = line.IndexOf(';', start);
    if (end < 0) end = line.IndexOf("-->", start, StringComparison.Ordinal);
    if (end < 0) end = line.Length;
    return line[start..end].Trim();
}

static ArticleMeta ParseArticle(string filePath)
{
    var text  = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    var nl    = text.IndexOf('\n');
    var first = nl >= 0 ? text[..nl] : text;
    var rest  = nl >= 0 ? text[(nl + 1)..] : "";

    string title, date, desc, body;
    if (first.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
    {
        title = ExtractMetaField(first, "title");
        date  = ExtractMetaField(first, "date");
        desc  = ExtractMetaField(first, "desc");
        body  = rest;
    }
    else
    {
        title = Path.GetFileNameWithoutExtension(filePath);
        date  = "";
        desc  = "";
        body  = text;
    }
    if (title == "") title = Path.GetFileNameWithoutExtension(filePath);
    return new ArticleMeta(Path.GetFileNameWithoutExtension(filePath), title, date, desc, body);
}

static string BuildArticleListPage(ArticleMeta[] articles, CharData[] chars)
{
    const string ARTICLE_LIST_CSS = """
        <style>
        .article-list { display: flex; flex-direction: column; gap: 14px; }
        .article-card {
          background: #fff; border-radius: 10px; padding: 18px 22px;
          box-shadow: 0 1px 3px rgba(0,0,0,0.06); border: 1px solid #f0f0f0;
          display: flex; justify-content: space-between; align-items: flex-start; gap: 16px;
        }
        .article-card-main { flex: 1; min-width: 0; }
        .article-title { font-size: 15.5px; font-weight: 600; color: #1a1a2e; }
        .article-title:hover { color: #4a90d9; }
        .article-desc { font-size: 13px; color: #888; margin-top: 6px; }
        .article-card-meta { display: flex; flex-direction: column; align-items: flex-end; gap: 8px; flex-shrink: 0; }
        .article-date { font-size: 12px; color: #bbb; }
        .article-read { font-size: 13px; color: #4a90d9; font-weight: 500; white-space: nowrap; }
        .article-read:hover { text-decoration: underline; }
        .article-empty { color: #bbb; font-size: 14px; padding: 20px 0; }
        </style>
        """;

    var cards = articles.Length == 0
        ? """<div class="article-empty">まだ記事がありません。</div>"""
        : string.Concat(articles.Select(a =>
        {
            var titleEnc = System.Net.WebUtility.HtmlEncode(a.Title);
            var descHtml = a.Desc != "" ? $"""<div class="article-desc">{System.Net.WebUtility.HtmlEncode(a.Desc)}</div>""" : "";
            var dateHtml = a.Date != "" ? $"""<span class="article-date">{a.Date}</span>""" : "";
            return $"""
                <div class="article-card">
                  <div class="article-card-main">
                    <a href="articles/{a.Slug}.html" class="article-title">{titleEnc}</a>
                    {descHtml}
                  </div>
                  <div class="article-card-meta">
                    {dateHtml}
                    <a href="articles/{a.Slug}.html" class="article-read">読む →</a>
                  </div>
                </div>
                """;
        }));

    return Layout("記事一覧", "articles", "#4a90d9", chars, $"""
        <div class="page-hero">
          <div class="hero-title">記事一覧</div>
          <div class="hero-sub">{articles.Length} 件の記事</div>
        </div>
        <div class="article-list">
        {cards}
        </div>
        """, extraHead: ARTICLE_LIST_CSS);
}

static string BuildArticlePage(ArticleMeta article, CharData[] chars)
{
    const string ARTICLE_PAGE_CSS = """
        <style>
        .article-header { margin-bottom: 28px; padding-bottom: 20px; border-bottom: 1px solid #e8e8e8; }
        .article-header-title { font-size: 26px; font-weight: 800; color: #1a1a2e; letter-spacing: -0.3px; }
        .article-header-date  { font-size: 13px; color: #aaa; margin-top: 8px; }
        .article-body { font-size: 15px; line-height: 1.8; }
        .article-body h2 { font-size: 20px; font-weight: 700; margin: 28px 0 12px; padding-bottom: 6px; border-bottom: 1px solid #f0f0f0; }
        .article-body h3 { font-size: 16px; font-weight: 600; margin: 20px 0 8px; }
        .article-body p  { margin-bottom: 14px; }
        .article-body ul, .article-body ol { margin: 0 0 14px 24px; }
        .article-body li { margin-bottom: 4px; }
        .article-body a  { color: #4a90d9; text-decoration: underline; }
        .article-body code { font-family: monospace; background: #f5f5f5; padding: 1px 5px; border-radius: 3px; font-size: 0.9em; }
        .article-back { margin-top: 40px; padding-top: 20px; border-top: 1px solid #f0f0f0; font-size: 13px; }
        .article-back a { color: #4a90d9; }
        .article-back a:hover { text-decoration: underline; }
        </style>
        """;

    var dateHtml = article.Date != "" ? $"""<div class="article-header-date">{article.Date}</div>""" : "";
    return Layout(article.Title, "articles", "#4a90d9", chars, $"""
        <div class="article-header">
          <div class="article-header-title">{System.Net.WebUtility.HtmlEncode(article.Title)}</div>
          {dateHtml}
        </div>
        <div class="article-body">{article.BodyHtml}</div>
        <div class="article-back"><a href="../articles.html">← 記事一覧に戻る</a></div>
        """, basePath: "../", extraHead: ARTICLE_PAGE_CSS);
}

static HashSet<string> CopyCardImages(string toolsRoot, string dstDir, string[] ids, CharData[] chars, Action<string> log)
{
    var copied      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var portBaseDir = Path.Combine(toolsRoot, "images", "card_portraits_png");
    if (!Directory.Exists(portBaseDir))
    {
        log("カード画像: card_portraits_png/ が見つかりません");
        return copied;
    }

    string[] fallbackDirs = ["colorless", "curse", "status", "token", "event", "quest"];

    foreach (var cardId in ids)
    {
        var rawId   = RawId(cardId).ToLowerInvariant();
        var charDir = GetCardDir(cardId, chars);

        string? srcPng = null;
        if (charDir != "shared")
        {
            var p = Path.Combine(portBaseDir, charDir, rawId + ".png");
            if (File.Exists(p)) srcPng = p;
        }
        if (srcPng is null)
        {
            foreach (var sub in fallbackDirs)
            {
                var p = Path.Combine(portBaseDir, sub, rawId + ".png");
                if (File.Exists(p)) { srcPng = p; break; }
            }
        }
        // fallback: packed/card_portraits/ から ctex 変換
        if (srcPng is null)
        {
            var packedBase = Path.Combine(toolsRoot, "images", "packed", "card_portraits");
            string? importPath = null;
            foreach (var sub in (charDir != "shared" ? new[] { charDir } : Array.Empty<string>()).Concat(fallbackDirs))
            {
                var p = Path.Combine(packedBase, sub, rawId + ".png.import");
                if (File.Exists(p)) { importPath = p; break; }
            }
            if (importPath is not null)
            {
                var ctexRel = ParseCtexPath(importPath);
                if (ctexRel is not null)
                {
                    var ctexFull = Path.Combine(toolsRoot, ctexRel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(ctexFull))
                    {
                        var cacheSubDir = charDir != "shared" ? charDir : fallbackDirs[0];
                        var cacheDir = Path.Combine(portBaseDir, cacheSubDir);
                        Directory.CreateDirectory(cacheDir);
                        var cachePng = Path.Combine(cacheDir, rawId + ".png");
                        try { ConvertCtex(ctexFull, cachePng); srcPng = cachePng; }
                        catch { /* 変換失敗は無視 */ }
                    }
                }
            }
        }
        if (srcPng is null) continue;

        var outDir = Path.Combine(dstDir, charDir);
        Directory.CreateDirectory(outDir);
        var dstPng = Path.Combine(outDir, rawId + ".png");
        if (!File.Exists(dstPng) || File.GetLastWriteTimeUtc(srcPng) > File.GetLastWriteTimeUtc(dstPng))
            File.Copy(srcPng, dstPng, overwrite: true);
        copied.Add(cardId);
    }
    log($"カード画像: {copied.Count} 件コピー");
    return copied;
}

static HashSet<string> ConvertImages(string toolsRoot, string imgSubdir, string dstDir, string[] ids, string label, Action<string> log)
{
    var converted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var srcDir    = Path.Combine(toolsRoot, "images", imgSubdir);
    if (!Directory.Exists(srcDir)) return converted;

    var total = 0;
    foreach (var id in ids)
    {
        var importPath = Path.Combine(srcDir, id.ToLowerInvariant() + ".png.import");
        if (!File.Exists(importPath)) continue;

        var ctexRel = ParseCtexPath(importPath);
        if (ctexRel is null) continue;

        var ctexFull = Path.Combine(toolsRoot, ctexRel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(ctexFull)) continue;

        var dstPng = Path.Combine(dstDir, id.ToLowerInvariant() + ".png");
        if (!File.Exists(dstPng))
        {
            try { ConvertCtex(ctexFull, dstPng); total++; }
            catch { continue; }
        }
        else
        {
            total++;
        }
        converted.Add(id);
    }
    log($"{label}画像を変換中... {converted.Count}件 ({total}件変換) -> dist/images/{imgSubdir}/");
    return converted;
}

static string? ParseCtexPath(string importPath)
{
    var content = File.ReadAllText(importPath);
    var m = Regex.Match(content, @"^path(?:\.\w+)?=""res://(.+?\.ctex)""",
        RegexOptions.Multiline);
    return m.Success ? m.Groups[1].Value : null;
}

static void ConvertCtex(string srcPath, string outPath)
{
    var data = File.ReadAllBytes(srcPath);
    if (System.Text.Encoding.ASCII.GetString(data, 0, 4) != "GST2")
        throw new InvalidDataException("Not a GST2 ctex file");

    var width      = (int)BitConverter.ToUInt32(data, 8);
    var height     = (int)BitConverter.ToUInt32(data, 12);
    var dataFormat = BitConverter.ToUInt32(data, 36);

    const int Hdr = 52;
    using var img = dataFormat == 2
        ? LoadWebP(data, Hdr)
        : DecodeBc7(data, Hdr, width, height);
    using var outStream = File.OpenWrite(outPath);
    img.Save(outStream, new PngEncoder());
}

static ISRgba32 LoadWebP(byte[] data, int hdr)
{
    var size = (int)BitConverter.ToUInt32(data, hdr);
    using var ms = new MemoryStream(data, hdr + 4, size);
    return ISImage.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(ms);
}

static ISRgba32 DecodeBc7(byte[] data, int hdr, int w, int h)
{
    var bc7Data = new ReadOnlyMemory<byte>(data, hdr, data.Length - hdr);
    var decoder = new BcDecoder();
    var pixels  = decoder.DecodeRaw(bc7Data.ToArray(), w, h, CompressionFormat.Bc7);
    var bytes   = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
    return ISImage.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes, w, h);
}

static string ExtractMarker(string filePath, string marker)
{
    var start = $"<!-- {marker}_START -->";
    var end   = $"<!-- {marker}_END -->";
    if (!File.Exists(filePath)) return "";
    var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    var s = content.IndexOf(start, StringComparison.Ordinal);
    var e = content.IndexOf(end,   StringComparison.Ordinal);
    if (s < 0 || e <= s) return "";
    return content[(s + start.Length)..e];
}

static string ExtractReview(string filePath) => ExtractMarker(filePath, "REVIEW");

static Dictionary<string, string> ParseChangelogDates(string changelogPath)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(changelogPath)) return result;
    var review = ExtractMarker(changelogPath, "REVIEW");
    foreach (Match m in Regex.Matches(review, @"<!--\s*CL:(\d{4}-\d{2}-\d{2}):([^\s>]+)\s*-->"))
    {
        var date = m.Groups[1].Value;
        var rel  = m.Groups[2].Value;
        if (!result.TryGetValue(rel, out var existing) ||
            string.Compare(date, existing, StringComparison.Ordinal) > 0)
            result[rel] = date;
    }
    return result;
}

public static string? ExtractReviewPublic(string filePath)
{
    const string START = "<!-- REVIEW_START -->";
    if (!File.Exists(filePath)) return null;
    var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    if (!content.Contains(START, StringComparison.Ordinal)) return null;
    return ExtractReview(filePath);
}

public static void SaveReview(string filePath, string reviewHtml)
{
    const string START = "<!-- REVIEW_START -->";
    const string END   = "<!-- REVIEW_END -->";
    if (!File.Exists(filePath)) return;
    var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    var s = content.IndexOf(START, StringComparison.Ordinal);
    var e = content.IndexOf(END,   StringComparison.Ordinal);
    if (s < 0 || e <= s) return;
    var newContent = content[..(s + START.Length)] + reviewHtml + content[e..];
    File.WriteAllText(filePath, newContent, System.Text.Encoding.UTF8);
}

static void UpdatePageLastUpdated(string filePath, string date)
{
    const string START = "<!-- LAST_UPDATED_START -->";
    const string END   = "<!-- LAST_UPDATED_END -->";
    if (!File.Exists(filePath)) return;
    var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    var s = content.IndexOf(START, StringComparison.Ordinal);
    var e = content.IndexOf(END,   StringComparison.Ordinal);
    if (s < 0 || e <= s) return;
    var div = $"""<div class="page-updated">最終更新: {date}</div>""";
    var newContent = content[..(s + START.Length)] + div + content[e..];
    File.WriteAllText(filePath, newContent, System.Text.Encoding.UTF8);
}

public static void AppendChangelogEntry(string reviewedFilePath)
{
    var distDir      = Path.GetFullPath(GetDistDir());
    var changelogPath = Path.Combine(distDir, "changelog.html");
    if (!File.Exists(changelogPath)) return;

    var relPath = Path.GetRelativePath(distDir, reviewedFilePath).Replace('\\', '/');
    var today   = DateTime.Today.ToString("yyyy-MM-dd");
    var marker  = $"<!-- CL:{today}:{relPath} -->";

    var fileContent = File.ReadAllText(changelogPath, System.Text.Encoding.UTF8);
    if (fileContent.Contains(marker, StringComparison.Ordinal)) return;

    var pageTitle = ExtractPageTitle(reviewedFilePath);
    var newEntry  = $"{marker}\n      <li class=\"cl-entry\"><span class=\"cl-date\">{today}</span><a href=\"{relPath}\" class=\"cl-link\">{pageTitle}</a> のレビューを更新しました。</li>";
    InsertChangelogEntry(changelogPath, newEntry);
    UpdatePageLastUpdated(reviewedFilePath, today);
}

public static void AppendManualChangelogEntry(string entryText)
{
    var distDir       = Path.GetFullPath(GetDistDir());
    var changelogPath = Path.Combine(distDir, "changelog.html");
    if (!File.Exists(changelogPath)) return;

    var today   = DateTime.Today.ToString("yyyy-MM-dd");
    var escaped = System.Net.WebUtility.HtmlEncode(entryText.Trim());
    var newEntry = $"<!-- CL-MANUAL:{today} -->\n      <li class=\"cl-entry\"><span class=\"cl-date\">{today}</span>{escaped}</li>";

    InsertChangelogEntry(changelogPath, newEntry);
}

static void InsertChangelogEntry(string changelogPath, string newEntry)
{
    var review = ExtractReview(changelogPath);
    string newReview;
    const string LIST_OPEN = "<ul class=\"changelog-list\">";
    if (review.Contains(LIST_OPEN, StringComparison.Ordinal))
    {
        var idx = review.IndexOf(LIST_OPEN, StringComparison.Ordinal) + LIST_OPEN.Length;
        newReview = review[..idx] + "\n      " + newEntry + review[idx..];
    }
    else
    {
        newReview = $"\n    <ul class=\"changelog-list\">\n      {newEntry}\n    </ul>\n  ";
    }
    SaveReview(changelogPath, newReview);
}

static (IReadOnlyDictionary<string, string> map, Regex regex, HashSet<string> eventIds) BuildAutoLinkMap(CharData[] chars)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    var eventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var id in CardDatabaseService.GetAllEventIds())
    {
        eventIds.Add(id);
        var ja = CardDatabaseService.GetEventTitle(id, japanese: true);
        var en = CardDatabaseService.GetEventTitle(id, japanese: false);
        if (ja.Length > 1) map.TryAdd(ja, $"events/{id}.html");
        if (en.Length > 1 && en != ja) map.TryAdd(en, $"events/{id}.html");
    }

    foreach (var id in EncounterDatabaseService.GetAllEncounterIds())
    {
        var ja = EncounterDatabaseService.GetEncounterName(id, japanese: true);
        var en = EncounterDatabaseService.GetEncounterName(id, japanese: false);
        if (ja.Length > 1) map.TryAdd(ja, $"encounters/{id}.html");
        if (en.Length > 1 && en != ja) map.TryAdd(en, $"encounters/{id}.html");
    }

    foreach (var id in CardDatabaseService.GetAllCardIds())
    {
        var rawId   = RawId(id);
        var charDir = GetCardDir(id, chars);
        var ja = CardDatabaseService.GetName(id, japanese: true);
        var en = CardDatabaseService.GetName(id, japanese: false);
        if (ja.Length > 1) map.TryAdd(ja, $"cards/{charDir}/{rawId}.html");
        if (en.Length > 1 && en != ja) map.TryAdd(en, $"cards/{charDir}/{rawId}.html");
    }

    foreach (var id in CardDatabaseService.GetAllRelicIds())
    {
        var rawId = RawId(id);
        var ja = CardDatabaseService.GetRelicTitle(rawId, japanese: true);
        var en = CardDatabaseService.GetRelicTitle(rawId, japanese: false);
        if (ja.Length > 1) map.TryAdd(ja, $"relics/{rawId}.html");
        if (en.Length > 1 && en != ja) map.TryAdd(en, $"relics/{rawId}.html");
    }

    var regex = map.Count > 0
        ? new Regex(string.Join("|", map.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)),
                    RegexOptions.Compiled)
        : new Regex("(?!)", RegexOptions.Compiled);

    return (map, regex, eventIds);
}

// 既存の自動リンク <a href="{basePath}(events|encounters|cards|relics)/..."> を除去してテキストに戻す。
// AutoLinkHtml の再適用前に呼ぶことでクラス属性を最新化できる。
static string StripAutoLinks(string html, string basePath)
{
    var escaped = Regex.Escape(basePath);
    return Regex.Replace(html,
        $@"<a\b[^>]*\shref=""{escaped}(?:events|encounters|cards|relics)/[^""]*""[^>]*>(.*?)</a>",
        "$1");
}

// HTML のテキストノードのみを対象にキーワードをリンクへ置換する。<a>・<script> タグ内はスキップ。
static string AutoLinkHtml(string html, Regex termRegex, IReadOnlyDictionary<string, string> map, string basePath)
{
    if (string.IsNullOrEmpty(html) || map.Count == 0) return html;
    var sb = new System.Text.StringBuilder();
    int skipDepth = 0;
    foreach (Match m in Regex.Matches(html, @"(<(?:a|script)\b[^>]*>)|(</(?:a|script)>)|(<[^>]+>)|([^<]+)"))
    {
        if      (m.Groups[1].Success) { skipDepth++; sb.Append(m.Value); }
        else if (m.Groups[2].Success) { skipDepth--; sb.Append(m.Value); }
        else if (m.Groups[3].Success) { sb.Append(m.Value); }
        else if (skipDepth == 0)
            sb.Append(termRegex.Replace(m.Value,
                tm => $"""<a class="auto-link" href="{basePath}{map[tm.Value]}">{tm.Value}</a>"""));
        else sb.Append(m.Value);
    }
    return sb.ToString();
}

static string ExtractPageTitle(string filePath)
{
    if (!File.Exists(filePath)) return Path.GetFileNameWithoutExtension(filePath);
    var html = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    // run pages: card-title-ja holds English char name, so use <title> instead
    bool isRunPage = filePath.Replace('\\', '/').Contains("/run/");
    if (!isRunPage)
    {
        var mJa = Regex.Match(html, @"<div class=""card-title-ja"">(.+?)</div>");
        if (mJa.Success) return mJa.Groups[1].Value.Trim();
    }
    var m = Regex.Match(html, @"<title>(.+?)\s*\|");
    return m.Success ? m.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(filePath);
}
} // class SiteBuilderCore

// ── records & flag definitions ────────────────────────────────────────────────

record CharData(string Id, string EnName, string JaName, string Accent, string LightBg, string Desc);
record PageEntry(string Category, string Path, string TitleEn, string TitleJa, string Desc, string Color);
record FlagDef(string Key, string Label);
record ArticleMeta(string Slug, string Title, string Date, string Desc, string BodyHtml);

static class CardFlags
{
    // 新フラグを追加するときはここに定数を追加し、AllDefs と ComputeFlags() にも反映する
    public const string IsAncient           = "IsAncient";
    public const string IsGeneratedInCombat = "IsGeneratedInCombat";

    public static readonly FlagDef[] AllDefs =
    [
        new(IsAncient,           "エンシェント"),
        new(IsGeneratedInCombat, "戦闘中に生成"),
    ];
}
