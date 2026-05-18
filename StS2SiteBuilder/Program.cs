using StS2Shared.Services;

var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var distDir    = Path.Combine(projectDir, "dist");
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

var mechanicsMap = CharacterMechanics.All
    .ToDictionary(c => c.CharLabel,
                  c => c.Mechanics.Select(m => m.MecLabel).ToArray(),
                  StringComparer.OrdinalIgnoreCase);

PageEntry[] pages =
[
    ..chars.Select(ch => new PageEntry("キャラクター", $"{ch.Id}.html", ch.EnName, ch.JaName, ch.Desc, ch.Accent)),
    new PageEntry("カード", "cards.html", "Card List", "カード一覧",
        "全カードをタイプ・レアリティ・フラグ付きで一覧表示。", "#2c3e50"),
];

var allCardIds = CardDatabaseService.GetAllCardIds().ToArray();

File.WriteAllText(Path.Combine(distDir, "index.html"), BuildIndex(chars),              System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "pages.html"), BuildPageList(pages, chars),    System.Text.Encoding.UTF8);
File.WriteAllText(Path.Combine(distDir, "cards.html"), BuildCardListPage(allCardIds, chars), System.Text.Encoding.UTF8);
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
    var outPath = Path.Combine(cardDir, $"{RawId(cardId)}.html");
    var review  = ExtractReview(outPath);
    File.WriteAllText(outPath,
        BuildCardPage(cardId, chars, basePath: "../../", review: review),
        System.Text.Encoding.UTF8);
}

Console.WriteLine($"Generated {3 + chars.Length + allCardIds.Length} files -> {distDir}");

// ── helpers ───────────────────────────────────────────────────────────────────

static string RawId(string id) => id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;

static string GetCardDir(string cardId, CharData[] chars)
{
    var charName = CardDatabaseService.GetCardCharacter(cardId);
    var ch = chars.FirstOrDefault(c => c.EnName.Equals(charName, StringComparison.OrdinalIgnoreCase));
    return ch?.Id ?? "shared";
}

// ── page builders ─────────────────────────────────────────────────────────────

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
          <p class="hero-sub">カードリファレンス</p>
          <p class="hero-desc">5人のキャラクターのカード・メカニクスを確認できます。</p>
        </div>
        <div class="char-grid">
          {cards}
        </div>
        """);
}

static string BuildPageList(PageEntry[] pages, CharData[] chars)
{
    string[] allCategories = ["キャラクター", "カード", "レリック", "イベント", "エンカウンター"];

    var sections = string.Concat(allCategories.Select(cat =>
    {
        var catPages = pages.Where(p => p.Category == cat).ToArray();

        string content;
        if (catPages.Length == 0)
        {
            content = """<p class="placeholder">ページはまだ追加されていません。</p>""";
        }
        else
        {
            var cardHtml = string.Concat(catPages.Select(p => $"""
                      <a href="{p.Path}" class="char-card">
                        <div class="char-card-header" style="background:{p.Color}">
                          <div class="char-name-en">{p.TitleEn}</div>
                          <div class="char-name-ja">{p.TitleJa}</div>
                        </div>
                        <div class="char-card-body">
                          <p class="char-desc">{p.Desc}</p>
                        </div>
                        <div class="char-card-footer">ページへ &rarr;</div>
                      </a>
                """));
            content = $"""<div class="char-grid">{cardHtml}</div>""";
        }

        var pendingBadge = catPages.Length == 0 ? """ <span class="pending-badge">準備中</span>""" : "";
        var sectionClass = catPages.Length == 0 ? " section-pending" : "";

        return $"""
            <section class="section{sectionClass}">
              <h2 class="section-title">{cat}{pendingBadge}</h2>
              {content}
            </section>
            """;
    }));

    return Layout("ページ一覧", "pages", "#4a90d9", chars, $"""
        <div class="page-hero">
          <h1 class="hero-title">ページ一覧</h1>
          <p class="hero-sub">全ページの一覧</p>
        </div>
        {sections}
        """);
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

            var typeBadge   = type   != "" ? $"""<span class="badge type-{type.ToLower()}">{type}</span>""" : "";
            var rarityBadge = rarity != "" ? $"""<span class="badge rarity-{rarity.ToLower()}">{rarity}</span>""" : "";
            var flagBadges  = string.Concat(
                CardFlags.AllDefs
                    .Where(f => flags.Contains(f.Key))
                    .Select(f => $"""<span class="badge flag-badge">{f.Label}</span>"""));
            var jaSpan = nameJa != nameEn ? $"""<span class="card-name-ja">{nameJa}</span>""" : "";

            return $"""
                      <tr>
                        <td class="col-name">
                          <a href="{href}" class="card-name-link">{nameEn}</a>{jaSpan}
                        </td>
                        <td class="col-type">{typeBadge}</td>
                        <td class="col-rarity">{rarityBadge}</td>
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
                    <th>カード名</th><th>タイプ</th><th>レアリティ</th><th>特性</th>
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
            <span class="filter-label">カード名</span>
            <input type="text" id="card-search" class="search-input" placeholder="名前で検索…" autocomplete="off">
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
                                      || (nj && nj.textContent.toLowerCase().includes(q)));
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
        """, extraHead: FILTER_CSS, extraFoot: FILTER_JS);
}

static string BuildCardPage(string cardId, CharData[] chars, string basePath, string review = "")
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
    var descEn = DescriptionFormatter.Resolve(rawDescEn, stats).Replace("\n", "<br>");
    var descJa = DescriptionFormatter.Resolve(rawDescJa, stats, japanese: true).Replace("\n", "<br>");

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

    var descSection = (descEn != "") ? $"""
        <section class="section">
          <h2 class="section-title">効果テキスト</h2>
          <p class="card-desc-en">{descEn}</p>
          {(descJa != "" && descJa != descEn ? $"""<p class="card-desc-ja">{descJa}</p>""" : "")}
        </section>
        """ : "";

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
          <div>
            <div class="card-breadcrumb">{charLink}</div>
            <h1 class="card-title-en" style="color:{accent}">{nameEn}</h1>
            {(nameJa != nameEn ? $"""<div class="card-title-ja">{nameJa}</div>""" : "")}
            <div class="card-badges">{typeBadge}{rarityBadge}{costBadge}</div>
          </div>
        </div>
        {descSection}
        {reviewZone}
        {statsSection}
        {flagsSection}
        """;

    return Layout(nameEn, "cards", accent, chars, content, basePath);
}

static string BuildCharPage(CharData ch, CharData[] chars, string[] mecs)
{
    var mecHtml = mecs.Length > 0
        ? $"""<div class="mec-tags">{string.Concat(mecs.Select(m => $"""<span class="mec-tag">{m}</span>"""))}</div>"""
        : """<p class="placeholder">メカニクス情報なし</p>""";

    return Layout(ch.EnName, ch.Id, ch.Accent, chars, $"""
        <div class="char-header" style="border-left:5px solid {ch.Accent};background:{ch.LightBg}">
          <div class="char-header-body">
            <h1 class="char-title-en" style="color:{ch.Accent}">{ch.EnName}</h1>
            <div class="char-title-ja">{ch.JaName}</div>
            <p class="char-desc-full">{ch.Desc}</p>
          </div>
          <img src="images/characters/{ch.Id}.jpg" class="char-hero-img" alt="{ch.EnName}">
        </div>
        <section class="section">
          <h2 class="section-title">メカニクス / シナジー</h2>
          {mecHtml}
        </section>
        <section class="section">
          <h2 class="section-title">カード一覧</h2>
          <p class="placeholder">準備中...</p>
        </section>
        """);
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

static int RarityOrder(string rarity) => rarity switch
{
    "Starter" => 0, "Common" => 1, "Uncommon" => 2, "Rare" => 3,
    "Ancient" => 4, "Event"  => 5, "Shop"     => 6,
    _ => 99,
};

static string Layout(string title, string activeId, string accent, CharData[] chars, string content,
                     string basePath = "", string extraHead = "", string extraFoot = "")
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
        .card-badges    { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 14px; }
        .card-desc-en   { font-size: 14px; color: #333; line-height: 1.75; }
        .card-desc-ja   { font-size: 13px; color: #777; line-height: 1.75; margin-top: 12px; }
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
          padding: 5px 13px; background: #f5f6f8; border: 1px solid #e4e6ea;
          border-radius: 20px; font-size: 13px; color: #444;
        }
        .placeholder { font-size: 13.5px; color: #bbb; font-style: italic; }

        /* ── Page list ── */
        .section-pending { opacity: 0.55; }
        .pending-badge {
          display: inline-block; font-size: 10px; font-weight: 600;
          background: #f0f0f0; color: #aaa; border-radius: 10px;
          padding: 2px 8px; margin-left: 8px; vertical-align: middle;
          letter-spacing: 0.5px; font-style: normal;
        }

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
        .wiki-link { display: inline-block; margin-top: 12px; font-size: 12.5px; color: #1a5799; }
        .wiki-link:hover { text-decoration: underline; }
        """;

    var homeActive  = activeId == "index";
    var homeStyle   = homeActive  ? " style=\"border-left-color:#4a90d9\"" : "";
    var homeClass   = homeActive  ? " active" : "";
    var pagesActive = activeId == "pages";
    var pagesStyle  = pagesActive ? " style=\"border-left-color:#4a90d9\"" : "";
    var pagesClass  = pagesActive ? " active" : "";
    var cardsActive = activeId == "cards";
    var cardsStyle  = cardsActive ? " style=\"border-left-color:#4a90d9\"" : "";
    var cardsClass  = cardsActive ? " active" : "";

    var navItems = string.Concat(chars.Select(ch => {
        var isActive    = ch.Id == activeId;
        var activeStyle = isActive ? $" style=\"border-left-color:{ch.Accent}\"" : "";
        var cls         = isActive ? " active" : "";
        return $"""
                  <a href="{basePath}{ch.Id}.html" class="nav-link{cls}"{activeStyle}>
                    <span class="nav-dot" style="background:{ch.Accent}"></span>
                    {ch.EnName}
                    <span class="nav-name-ja">{ch.JaName}</span>
                  </a>
            """;
    }));

    return $"""
        <!DOCTYPE html>
        <html lang="ja">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>{title} | StS2 カードリファレンス</title>
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
                <div class="brand-label">カードリファレンス</div>
              </div>
              <div class="nav-section">
                <div class="nav-group-label">ページ</div>
                <a href="{basePath}index.html" class="nav-link{homeClass}"{homeStyle}>
                  <span class="nav-icon">&#8962;</span>トップ
                </a>
                <a href="{basePath}pages.html" class="nav-link{pagesClass}"{pagesStyle}>
                  <span class="nav-icon">&#9776;</span>ページ一覧
                </a>
                <a href="{basePath}cards.html" class="nav-link{cardsClass}"{cardsStyle}>
                  <span class="nav-icon">&#9670;</span>カード一覧
                </a>
              </div>
              <div class="nav-section">
                <div class="nav-group-label">キャラクター</div>
                {navItems}
              </div>
            </nav>
            <main class="main">
              {content}
            </main>
          </div>
          <script src="{basePath}wiki-link.js"></script>
          {extraFoot}
        </body>
        </html>
        """;
}

static string ExtractReview(string filePath)
{
    const string START = "<!-- REVIEW_START -->";
    const string END   = "<!-- REVIEW_END -->";
    if (!File.Exists(filePath)) return "";
    var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    var s = content.IndexOf(START, StringComparison.Ordinal);
    var e = content.IndexOf(END,   StringComparison.Ordinal);
    if (s < 0 || e <= s) return "";
    return content[(s + START.Length)..e];
}

// ── records & flag definitions ────────────────────────────────────────────────

record CharData(string Id, string EnName, string JaName, string Accent, string LightBg, string Desc);
record PageEntry(string Category, string Path, string TitleEn, string TitleJa, string Desc, string Color);
record FlagDef(string Key, string Label);

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
