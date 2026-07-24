using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// リポジトリの成果物を実スキャンして自己完結の HTML レポートを組み立てる。
// - StS2Shared/Resources/v*/ のバージョン × ファイル presence マトリクス（最新版はサイズ・件数つき）
// - Resources 直下の手動管理 JSON
// - tools/extracted/images/ の画像出力（*_images.json / monster_names.json との突き合わせで欠落検出）
static class ArtifactInventory
{
    const long MaxJsonBytesForCount = 20_000_000; // これ超の JSON は件数カウントを省略

    record ImageScan(string Key, string DirLabel, bool DirExists, int Found, long Bytes,
                     int? Expected, List<string> Missing, bool MissingIsError, string Detail);

    public static string BuildHtml(string repoRoot)
    {
        var resourcesDir = Path.Combine(repoRoot, "StS2Shared", "Resources");
        var toolsRoot    = Path.Combine(repoRoot, "tools", "extracted");
        bool hasTools    = Directory.Exists(toolsRoot);

        // ── バージョンフォルダ（昇順、末尾が最新）──
        var versions = Directory.GetDirectories(resourcesDir, "v*")
            .Select(d => Path.GetFileName(d)!)
            .OrderBy(VersionKey, StringComparer.Ordinal)
            .ToList();
        var latest = versions.Count > 0 ? versions[^1] : null;

        // バージョンごとの相対ファイル一覧（ルート *.json ＋ localization/{lang}/*.json）
        var filesByVersion = versions.ToDictionary(v => v, v => ListVersionFiles(Path.Combine(resourcesDir, v)));
        var allFiles = filesByVersion.Values.SelectMany(f => f)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f.Contains('/') ? 1 : 0).ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ── Resources 直下の手動管理 JSON ──
        var manualFiles = Directory.GetFiles(resourcesDir, "*.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        // ── 画像出力スキャン ──
        var imageScans = new List<ImageScan>();
        if (hasTools)
        {
            imageScans.Add(ScanPngGroup(toolsRoot, resourcesDir, latest, "card_portraits_png", "card_images.json"));
            imageScans.Add(ScanPngGroup(toolsRoot, resourcesDir, latest, "relics_png",   "relic_images.json"));
            imageScans.Add(ScanPngGroup(toolsRoot, resourcesDir, latest, "events_png",   "event_images.json"));
            imageScans.Add(ScanPngGroup(toolsRoot, resourcesDir, latest, "ancients_png", "ancient_images.json"));
            imageScans.Add(ScanPngGroup(toolsRoot, resourcesDir, latest, "potions_png",  "potion_images.json"));
            imageScans.Add(ScanMonsters(toolsRoot, resourcesDir, latest));
            imageScans.Add(ScanAtlases(toolsRoot));
        }

        // ── HTML 組み立て ──
        var sb = new StringBuilder();
        sb.AppendLine("""
            <!doctype html>
            <html lang="ja">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>SpireScope 成果物インベントリ</title>
            <style>
              :root { color-scheme: light dark; }
              body { font-family: "Segoe UI", "Yu Gothic UI", sans-serif; margin: 2rem auto; max-width: 72rem; padding: 0 1rem; line-height: 1.55; }
              h1 { border-bottom: 3px solid #7c5cbf; padding-bottom: .3rem; }
              h2 { margin-top: 2.2rem; border-left: 6px solid #7c5cbf; padding-left: .5rem; }
              table { border-collapse: collapse; width: 100%; font-size: .85rem; }
              th, td { border: 1px solid #8884; padding: .25rem .5rem; text-align: left; vertical-align: top; }
              th { background: #7c5cbf22; }
              td.num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
              code { background: #8882; padding: 0 .3em; border-radius: 3px; font-size: .95em; }
              .meta { color: #888; font-size: .85rem; }
              .ok   { color: #2c9b4b; font-weight: bold; }
              .warn { color: #c47f17; font-weight: bold; }
              .err  { color: #d43a3a; font-weight: bold; }
              .banner { background: #d43a3a22; border: 1px solid #d43a3a; border-radius: 6px; padding: .7rem 1rem; margin: 1rem 0; }
              .dot { text-align: center; }
              .missing { font-size: .8rem; color: #c47f17; margin: .2rem 0 0 0; }
              .scroll { overflow-x: auto; }
              ul { margin: .3rem 0; padding-left: 1.4rem; }
            </style>
            </head>
            <body>
            """);
        sb.AppendLine("<h1>SpireScope 成果物インベントリ</h1>");
        sb.AppendLine($"<p class=\"meta\">生成: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ／ リポジトリ: <code>{H(repoRoot)}</code> ／ tools/extracted: " +
                      (hasTools ? "<span class=\"ok\">あり</span>" : "<span class=\"err\">なし</span>") + "</p>");

        if (!hasTools)
            sb.AppendLine($"<div class=\"banner\"><b>tools/extracted がこのマシンに存在しません</b>（PCK 展開が未実行）。" +
                          $"画像系セクションはスキップされます。展開手順は CLAUDE.md の「クリーン展開チェックリスト」を参照。<br>" +
                          $"<span class=\"meta\">探索パス: <code>{H(toolsRoot)}</code></span></div>");

        // ── 1. 成果物グループ一覧（静的カタログ × 実測サマリ）──
        sb.AppendLine("<h2>成果物グループ一覧</h2>");
        sb.AppendLine("<div class=\"scroll\"><table>");
        sb.AppendLine("<tr><th>成果物</th><th>生成元 / コマンド</th><th>出力先</th><th>実測</th><th>参照元</th><th>備考</th></tr>");
        foreach (var g in ArtifactCatalog.Groups)
        {
            var live = g.Key switch
            {
                "resources" => latest is null
                    ? "<span class=\"err\">バージョンフォルダなし</span>"
                    : $"{versions.Count} バージョン ／ 最新 <b>{H(latest)}</b>（{filesByVersion[latest].Count} ファイル）",
                "manual" => $"{manualFiles.Count} ファイル",
                _ when !hasTools => "<span class=\"meta\">未展開</span>",
                _ => imageScans.FirstOrDefault(s => s.Key == g.Key) is { } s ? SummarizeScan(s) : "—",
            };
            sb.AppendLine("<tr>" +
                $"<td><b>{H(g.Title)}</b></td>" +
                $"<td>{H(g.Producer)}<br><code>{H(g.Command)}</code></td>" +
                $"<td><code>{H(g.Output)}</code></td>" +
                $"<td>{live}</td>" +
                $"<td><ul>{string.Join("", g.Consumers.Select(c => $"<li>{H(c)}</li>"))}</ul></td>" +
                $"<td class=\"meta\">{H(g.Notes)}</td></tr>");
        }
        sb.AppendLine("</table></div>");

        // ── 2. Resources バージョンマトリクス ──
        sb.AppendLine("<h2>メタデータ JSON — バージョン × ファイル マトリクス</h2>");
        sb.AppendLine("<p class=\"meta\">最新版はサイズ・トップレベル件数つき。旧版は ● = 存在。バージョン間のファイル構成ドリフトの確認用。</p>");
        sb.AppendLine("<div class=\"scroll\"><table>");
        sb.Append("<tr><th>ファイル</th>");
        foreach (var v in versions)
            sb.Append(v == latest ? $"<th>{H(v)}（最新）</th><th>サイズ</th><th>件数</th>" : $"<th>{H(v)}</th>");
        sb.AppendLine("</tr>");
        foreach (var file in allFiles)
        {
            sb.Append($"<tr><td><code>{H(file)}</code></td>");
            foreach (var v in versions)
            {
                bool exists = filesByVersion[v].Contains(file, StringComparer.OrdinalIgnoreCase);
                if (v == latest)
                {
                    if (exists)
                    {
                        var path = Path.Combine(resourcesDir, v, file.Replace('/', Path.DirectorySeparatorChar));
                        var info = new FileInfo(path);
                        sb.Append($"<td class=\"dot ok\">●</td><td class=\"num\">{FormatBytes(info.Length)}</td><td class=\"num\">{TopLevelCount(path, info.Length)}</td>");
                    }
                    else
                        sb.Append("<td class=\"dot warn\">−</td><td></td><td></td>");
                }
                else
                    sb.Append(exists ? "<td class=\"dot\">●</td>" : "<td class=\"dot meta\">−</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table></div>");

        // ── 3. 手動管理 JSON ──
        sb.AppendLine("<h2>手動管理 JSON（Resources 直下・バージョン非依存）</h2>");
        if (manualFiles.Count == 0)
            sb.AppendLine("<p class=\"meta\">なし</p>");
        else
        {
            sb.AppendLine("<table><tr><th>ファイル</th><th>サイズ</th><th>件数</th></tr>");
            foreach (var f in manualFiles)
            {
                var info = new FileInfo(f);
                sb.AppendLine($"<tr><td><code>{H(Path.GetFileName(f))}</code></td>" +
                              $"<td class=\"num\">{FormatBytes(info.Length)}</td><td class=\"num\">{TopLevelCount(f, info.Length)}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        // ── 4. 画像出力詳細 ──
        if (hasTools)
        {
            sb.AppendLine("<h2>画像出力（tools/extracted/images/）</h2>");
            sb.AppendLine("<div class=\"scroll\"><table>");
            sb.AppendLine("<tr><th>ディレクトリ</th><th>実ファイル</th><th>合計サイズ</th><th>期待数</th><th>欠落</th></tr>");
            foreach (var s in imageScans)
            {
                string missingCell;
                if (!s.DirExists)
                    missingCell = "<span class=\"err\">ディレクトリなし</span>";
                else if (s.Expected is null)
                    missingCell = "<span class=\"meta\">—</span>";
                else if (s.Missing.Count == 0)
                    missingCell = "<span class=\"ok\">なし</span>";
                else
                {
                    var cls = s.MissingIsError ? "err" : "warn";
                    var shown = s.Missing.Take(30).Select(H);
                    var more = s.Missing.Count > 30 ? $" +{s.Missing.Count - 30} more" : "";
                    var note = s.MissingIsError ? "" : "<br><span class=\"meta\">（visible=false の ID は正当に画像なし — 情報表示）</span>";
                    missingCell = $"<span class=\"{cls}\">{s.Missing.Count} 件</span>" +
                                  $"<div class=\"missing\">{string.Join(", ", shown)}{H(more)}</div>{note}";
                }
                sb.AppendLine("<tr>" +
                    $"<td><code>{H(s.DirLabel)}</code></td>" +
                    $"<td class=\"num\">{s.Detail}</td>" +
                    $"<td class=\"num\">{FormatBytes(s.Bytes)}</td>" +
                    $"<td class=\"num\">{(s.Expected is int e ? e.ToString() : "—")}</td>" +
                    $"<td>{missingCell}</td></tr>");
            }
            sb.AppendLine("</table></div>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ── スキャン ──────────────────────────────────────────────────────────────

    /// <summary>バージョンフォルダ内の JSON 相対パス一覧（ルート直下 ＋ localization/{lang}/）。区切りは '/'。</summary>
    static List<string> ListVersionFiles(string versionDir)
    {
        var list = Directory.GetFiles(versionDir, "*.json").Select(f => Path.GetFileName(f)!).ToList();
        var locDir = Path.Combine(versionDir, "localization");
        if (Directory.Exists(locDir))
            foreach (var langDir in Directory.GetDirectories(locDir))
                list.AddRange(Directory.GetFiles(langDir, "*.json")
                    .Select(f => $"localization/{Path.GetFileName(langDir)}/{Path.GetFileName(f)}"));
        return list;
    }

    /// <summary>PNG グループ: 実ファイル数と、最新版 *_images.json の値集合から期待数・欠落を出す。</summary>
    static ImageScan ScanPngGroup(string toolsRoot, string resourcesDir, string? latest, string dirName, string mapJson)
    {
        var dir = Path.Combine(toolsRoot, "images", dirName);
        bool exists = Directory.Exists(dir);
        int found = 0; long bytes = 0;
        if (exists)
            foreach (var f in Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories))
            { found++; bytes += new FileInfo(f).Length; }

        int? expected = null;
        var missing = new List<string>();
        if (latest is not null && TryReadMapValues(Path.Combine(resourcesDir, latest, mapJson)) is { } rels)
        {
            expected = rels.Count;
            if (exists)
                missing = rels.Where(r => !File.Exists(Path.Combine(dir, r.Replace('/', Path.DirectorySeparatorChar))))
                              .OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
        }
        return new(dirName, $"images/{dirName}/", exists, found, bytes, expected, missing,
                   MissingIsError: true, Detail: $"PNG {found}");
    }

    /// <summary>monsters: ルート直下の PNG/GIF のみカウント（.import/.ctex 混在のため）。期待値は monster_names.json の dirName 数。</summary>
    static ImageScan ScanMonsters(string toolsRoot, string resourcesDir, string? latest)
    {
        var dir = Path.Combine(toolsRoot, "images", "monsters");
        bool exists = Directory.Exists(dir);
        int png = 0, gif = 0; long bytes = 0;
        if (exists)
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*.png")) { png++; bytes += new FileInfo(f).Length; }
            foreach (var f in Directory.EnumerateFiles(dir, "*.gif")) { gif++; bytes += new FileInfo(f).Length; }
        }

        int? expected = null;
        var missing = new List<string>();
        if (latest is not null)
        {
            var namesPath = Path.Combine(resourcesDir, latest, "monster_names.json");
            if (File.Exists(namesPath))
            {
                try
                {
                    // 形式: [{ "dirName": "aeonglass", "en": ..., "ja": ... }, ...]
                    using var doc = JsonDocument.Parse(File.ReadAllText(namesPath));
                    var ids = doc.RootElement.EnumerateArray()
                        .Select(e => e.GetProperty("dirName").GetString())
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Select(id => id!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    expected = ids.Count;
                    if (exists)
                        missing = ids.Where(id => !File.Exists(Path.Combine(dir, id + ".png")))
                                     .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
                }
                catch { /* 壊れた JSON は期待値なし扱い */ }
            }
        }
        return new("monsters", "images/monsters/", exists, png + gif, bytes, expected, missing,
                   MissingIsError: false, Detail: $"PNG {png} ／ GIF {gif}");
    }

    static ImageScan ScanAtlases(string toolsRoot)
    {
        var dir = Path.Combine(toolsRoot, "images", "atlases");
        bool exists = Directory.Exists(dir);
        int found = 0; long bytes = 0;
        if (exists)
            foreach (var f in Directory.EnumerateFiles(dir, "*.png")) { found++; bytes += new FileInfo(f).Length; }
        return new("atlases", "images/atlases/", exists, found, bytes, null, [], true, $"PNG {found}");
    }

    /// <summary>ID → 相対パス形式の *_images.json の値集合を返す（無ければ null）。</summary>
    static List<string>? TryReadMapValues(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.EnumerateObject()
                .Select(p => p.Value.GetString())
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return null; }
    }

    static string SummarizeScan(ImageScan s)
    {
        if (!s.DirExists) return "<span class=\"err\">ディレクトリなし</span>";
        var summary = s.Detail;
        if (s.Expected is int e)
        {
            var cls = s.Missing.Count == 0 ? "ok" : (s.MissingIsError ? "err" : "warn");
            summary += $" ／ 期待 {e} <span class=\"{cls}\">（欠落 {s.Missing.Count}）</span>";
        }
        return summary;
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    /// <summary>JSON のトップレベル件数（オブジェクト=キー数 / 配列=要素数）。巨大・破損時は省略表記。</summary>
    static string TopLevelCount(string path, long length)
    {
        if (length > MaxJsonBytesForCount) return "—";
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Object => doc.RootElement.EnumerateObject().Count().ToString(),
                JsonValueKind.Array  => doc.RootElement.GetArrayLength().ToString(),
                _ => "—",
            };
        }
        catch { return "<span class=\"err\">parse!</span>"; }
    }

    // バージョン token 内の整数列を 0 埋め連結して文字列比較で数値順にする
    // （StS2Shared/Services/ResourceResolver.cs の VersionKey と同方針）。
    static string VersionKey(string name) =>
        string.Join('.', Regex.Matches(name, @"\d+").Select(m => m.Value.PadLeft(8, '0')));

    static string FormatBytes(long b) => b switch
    {
        >= 1024 * 1024 => $"{b / (1024.0 * 1024):0.0} MB",
        >= 1024        => $"{b / 1024.0:0.0} KB",
        _              => $"{b} B",
    };

    static string H(string s) => System.Net.WebUtility.HtmlEncode(s);
}
