using System.Diagnostics;
using System.Drawing.Imaging;
using StS2Toys.Services;
using StS2Shared.Services;

namespace StS2Toys;

public record DeckCard(string Id, string NameEn, string NameJa, string Cost, string Type, int Count, bool IsUpgraded = false, string EnchantmentId = "", int EnchantmentAmount = 0);
public record RelicEntry(string Id, string NameEn, string NameJa);
public record OverviewSection(string LabelEn, string LabelJa, IReadOnlyList<DeckCard> Cards, IReadOnlyList<RelicEntry> Relics);
record HitEntry(Rectangle Rect, string Id, bool IsRelic, string EnchantmentId = "", int EnchantmentAmount = 0);

public partial class DeckOverviewForm : Form
{
    const int CardW = 120, CardH = 91, RelicH = 56, Gap = 4, PadX = 8, PadY = 8, HeaderH = 28, SectionGap = 8;
    // レリックは名前を出さず PNG アイコンのみのため、正方形タイルで密に並べる。
    const int RelicTile = 56;

    private IReadOnlyList<DeckCard>? _cards;
    private IReadOnlyList<RelicEntry>? _relics;
    private IReadOnlyList<(string LabelEn, string LabelJa, Func<string, bool> Filter)>? _keywordGroups;
    private IReadOnlyList<OverviewSection>? _sections;
    private string _titleEn = "Deck Overview";
    private string _titleJa = "デッキ枚数理論値";
    private int? _deckTotalOverride;
    private readonly Dictionary<string, Bitmap?> _imageCache = new();
    readonly ToolTip _hoverTip = new() { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000, ShowAlways = true };
    // カード／レリッククリックで開く外部リンク（URLテンプレート）の選択メニュー。
    readonly ContextMenuStrip _linkMenu = new();
    List<HitEntry> _hitMap = [];
    string? _hoveredId;
    readonly HashSet<string> _collapsedSections = new();
    List<(Rectangle Rect, string Key)> _sectionHeaderMap = [];

    // キャラクター概観モード（5キャラ統合）。EnableCharacterMode で有効化。
    static readonly string[] CharacterLabels = { "Necrobinder", "Ironclad", "Silent", "Defect", "Regent" };
    bool _characterMode;
    // キャラクター概観の先頭に「戦闘中に消滅する」グループを表示する（旧「デッキ枚数理論値」フォームの統合先）。
    bool _showDisposableGroup;
    string? _autoCharacterId;

    public DeckOverviewForm()
    {
        InitializeComponent();
        VisibleChanged += (_, _) => { if (Visible) RecomposeIfNeeded(); };
        ResizeEnd += (_, _) => RecomposeIfNeeded();
        _pictureBox.MouseMove  += OnPictureBoxMouseMove;
        _pictureBox.MouseClick += OnPictureBoxClick;
        _pictureBox.MouseLeave += (_, _) => { _hoverTip.Hide(_pictureBox); _hoveredId = null; _pictureBox.Cursor = Cursors.Default; };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var bmp in _imageCache.Values) bmp?.Dispose();
            _pictureBox.Image?.Dispose();
            _hoverTip.Dispose();
            _linkMenu.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void UpdateDeck(IReadOnlyList<DeckCard> cards)
    {
        _cards = cards;
        if (_characterMode) ApplyCharacter();
        else if (Visible) RecomposeIfNeeded();
    }

    public void UpdateRelics(IReadOnlyList<RelicEntry> relics)
    {
        _relics = relics;
        if (_characterMode) ApplyCharacter();
        else if (Visible) RecomposeIfNeeded();
    }

    public void SetTitle(string titleEn, string titleJa)
    {
        _titleEn = titleEn;
        _titleJa = titleJa;
    }

    public void SetKeywordGroups(
        IReadOnlyList<(string LabelEn, string LabelJa, Func<string, bool> Filter)> groups,
        string titleEn, string titleJa)
    {
        _keywordGroups = groups;
        _titleEn = titleEn;
        _titleJa = titleJa;
    }

    public void SetStatsText(string text)
    {
        _statsLabel.Text = text;
        _statsPanel.Visible = !string.IsNullOrEmpty(text);
    }

    public void SetSections(IReadOnlyList<OverviewSection> sections, int deckTotal)
    {
        _sections = sections;
        _deckTotalOverride = deckTotal;
        _cards = null;
        _relics = null;
        _keywordGroups = null;
        _statsPanel.Visible = false;
        if (Visible) RecomposeIfNeeded();
    }

    /// <summary>キャラクター概観モードを有効化する。上部にキャラ選択ドロップダウンを表示し、
    /// 「自動（セーブ）」+5キャラを選べるようにする。5つの個別概観フォームの統合先。</summary>
    public void EnableCharacterMode()
    {
        _characterMode = true;
        _showDisposableGroup = true;
        _charSelector.Items.Clear();
        _charSelector.Items.Add("自動（セーブ）");
        foreach (var l in CharacterLabels) _charSelector.Items.Add(l);
        _charSelector.SelectedIndexChanged -= OnCharSelectorChanged;
        _charSelector.SelectedIndexChanged += OnCharSelectorChanged;
        _charSelector.SelectedIndex = 0;
        _charPanel.Visible = true;
    }

    void OnCharSelectorChanged(object? sender, EventArgs e) => ApplyCharacter();

    /// <summary>進行中ランのキャラクター（"CHARACTER.DEFECT" 等）を通知する。
    /// セレクタが「自動」のとき、このキャラの概観へ追従する。</summary>
    public void SetCurrentCharacter(string? characterId)
    {
        _autoCharacterId = SaveDataService.NormalizeCharacterId(characterId);
        if (_characterMode && _charSelector.SelectedIndex <= 0) ApplyCharacter();
    }

    void ApplyCharacter()
    {
        if (!_characterMode) return;
        // index<=0 は「自動（セーブ）」。それ以外は選択中のキャラ名。
        string? label = _charSelector.SelectedIndex <= 0
            ? CharacterLabels.FirstOrDefault(l => l.ToUpperInvariant() == _autoCharacterId)
            : _charSelector.SelectedItem as string;
        if (string.IsNullOrEmpty(label)) return; // キャラ未判定時は前回表示を維持

        SetKeywordGroups(
            CharacterMechanics.MechanicsFor(label).Select(m => (m.EnLabel, m.JaLabel, m.Filter)).ToArray(),
            $"{label} Overview", $"{label}概観");
        SetStatsText(BuildCharacterStats(label, _cards ?? []));
        if (Visible) RecomposeIfNeeded();
    }

    static string BuildCharacterStats(string label, IReadOnlyList<DeckCard> deck) =>
        string.Join("  ", CharacterMechanics.MechanicsFor(label)
            .Select(m => AppLanguage.IsJapanese
                ? $"{m.JaLabel}: {deck.Where(c => m.Filter(c.Id)).Sum(c => c.Count)}枚"
                : $"{m.EnLabel}: {deck.Where(c => m.Filter(c.Id)).Sum(c => c.Count)}"));

    void RecomposeIfNeeded()
    {
        if (_cards is null && _sections is null) return;
        Text = AppLanguage.IsJapanese ? _titleJa : _titleEn;
        var w = _scrollPanel.ClientSize.Width;
        if (w <= 0) return;

        var bmp = _sections is not null ? ComposeFromSections(w) : ComposeImage(w);
        var oldImage = _pictureBox.Image;
        _pictureBox.Size = new Size(bmp.Width, bmp.Height);
        _pictureBox.Image = bmp;
        oldImage?.Dispose();
    }

    Bitmap ComposeImage(int availableWidth)
    {
        if (_keywordGroups is not null)
            return ComposeImageKeyword(availableWidth);

        bool ja = AppLanguage.IsJapanese;
        var groups = (_cards ?? [])
            .GroupBy(c => c.Type)
            .OrderBy(g => TypeOrder(g.Key))
            .Select(g => (Label: TypeLabel(g.Key), Cards: g.OrderBy(c => ja ? c.NameJa : c.NameEn).ToList()))
            .ToList();
        var relics = _relics ?? [];

        int cardsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (CardW + Gap));
        int relicsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (RelicTile + Gap));

        int totalHeight = PadY;
        foreach (var (_, cards) in groups)
        {
            int rows = (cards.Count + cardsPerRow - 1) / cardsPerRow;
            totalHeight += HeaderH + rows * (CardH + Gap) + SectionGap;
        }
        int relicRows = relics.Count > 0 ? (relics.Count + relicsPerRow - 1) / relicsPerRow : 1;
        totalHeight += HeaderH + relicRows * (RelicH + Gap) + SectionGap;
        totalHeight += PadY;

        _hitMap.Clear();
        var bmp = new Bitmap(availableWidth, Math.Max(totalHeight, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int y = PadY;
        bool jaMode = AppLanguage.IsJapanese;
        int deckTotal = _deckTotalOverride ?? (_cards ?? []).Sum(c => c.Count);
        foreach (var (label, cards) in groups)
        {
            var countStr = FormatCardCount(cards.Sum(c => c.Count), deckTotal, jaMode);
            DrawSectionHeader(g, label, countStr,
                new Rectangle(PadX, y, availableWidth - 2 * PadX, HeaderH));
            y += HeaderH;

            for (int i = 0; i < cards.Count; i++)
            {
                int col = i % cardsPerRow;
                int row = i / cardsPerRow;
                var cardRect = new Rectangle(
                    PadX + col * (CardW + Gap),
                    y + row * (CardH + Gap),
                    CardW, CardH);
                _hitMap.Add(new HitEntry(cardRect, cards[i].Id, false, cards[i].EnchantmentId, cards[i].EnchantmentAmount));
                DrawCard(g, cards[i], cardRect);
            }

            int totalRows = (cards.Count + cardsPerRow - 1) / cardsPerRow;
            y += totalRows * (CardH + Gap) + SectionGap;
        }

        var relicHeader = jaMode ? "レリック" : "Relics";
        var relicCount  = jaMode ? (relics.Count > 0 ? $"{relics.Count}個" : "なし") : (relics.Count > 0 ? relics.Count.ToString() : "None");
        DrawSectionHeader(g, relicHeader, relicCount,
            new Rectangle(PadX, y, availableWidth - 2 * PadX, HeaderH));
        y += HeaderH;
        if (relics.Count > 0)
        {
            for (int i = 0; i < relics.Count; i++)
            {
                int col = i % relicsPerRow;
                int row = i / relicsPerRow;
                var relicRect = new Rectangle(
                    PadX + col * (RelicTile + Gap),
                    y + row * (RelicH + Gap),
                    RelicTile, RelicH);
                _hitMap.Add(new HitEntry(relicRect, relics[i].Id, true));
                DrawRelicTile(g, relics[i], relicRect);
            }
        }
        else
        {
            using var fgNone = new SolidBrush(SystemColors.GrayText);
            using var fontNone = new Font("Segoe UI", 8.5f);
            g.DrawString(jaMode ? "なし" : "None", fontNone, fgNone, new PointF(PadX + 4, y + 8));
        }

        return bmp;
    }

    Bitmap ComposeFromSections(int availableWidth)
    {
        int cardsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (CardW + Gap));
        int relicsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (RelicTile + Gap));
        bool jaMode = AppLanguage.IsJapanese;
        int deckTotal = _deckTotalOverride ?? 0;

        int totalH = PadY;
        foreach (var sec in _sections!)
        {
            bool collapsed = _collapsedSections.Contains(sec.LabelEn);
            totalH += HeaderH;
            if (!collapsed)
            {
                int cardRows  = sec.Cards.Count  > 0 ? (sec.Cards.Count  + cardsPerRow - 1) / cardsPerRow : 0;
                int relicRows = sec.Relics.Count > 0 ? (sec.Relics.Count + relicsPerRow - 1) / relicsPerRow : 0;
                if (cardRows > 0)  totalH += cardRows  * (CardH  + Gap);
                if (relicRows > 0) totalH += relicRows  * (RelicH + Gap) + (cardRows > 0 ? Gap : 0);
                if (cardRows == 0 && relicRows == 0) totalH += RelicH;
            }
            totalH += SectionGap;
        }
        totalH += PadY;

        _hitMap.Clear();
        _sectionHeaderMap.Clear();
        var bmp = new Bitmap(availableWidth, Math.Max(totalH, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int y = PadY;
        foreach (var sec in _sections!)
        {
            bool collapsed = _collapsedSections.Contains(sec.LabelEn);
            var label = (collapsed ? "▶ " : "▼ ") + (jaMode ? sec.LabelJa : sec.LabelEn);
            int cardCount = sec.Cards.Sum(c => c.Count);
            string countStr = sec.Cards.Count == 0 && sec.Relics.Count > 0
                ? (jaMode ? $"{sec.Relics.Count}個" : sec.Relics.Count.ToString())
                : FormatCardCount(cardCount, deckTotal, jaMode);
            var headerRect = new Rectangle(PadX, y, availableWidth - 2 * PadX, HeaderH);
            DrawSectionHeader(g, label, countStr, headerRect);
            _sectionHeaderMap.Add((headerRect, sec.LabelEn));
            y += HeaderH;

            if (!collapsed)
            {
                int cardRows = sec.Cards.Count > 0 ? (sec.Cards.Count + cardsPerRow - 1) / cardsPerRow : 0;
                for (int i = 0; i < sec.Cards.Count; i++)
                {
                    int col = i % cardsPerRow, row = i / cardsPerRow;
                    var cardRect = new Rectangle(PadX + col * (CardW + Gap), y + row * (CardH + Gap), CardW, CardH);
                    _hitMap.Add(new HitEntry(cardRect, sec.Cards[i].Id, false, sec.Cards[i].EnchantmentId, sec.Cards[i].EnchantmentAmount));
                    DrawCard(g, sec.Cards[i], cardRect);
                }
                if (cardRows > 0) y += cardRows * (CardH + Gap);

                if (sec.Relics.Count > 0)
                {
                    if (cardRows > 0) y += Gap;
                    int relicRows = (sec.Relics.Count + relicsPerRow - 1) / relicsPerRow;
                    for (int i = 0; i < sec.Relics.Count; i++)
                    {
                        int col = i % relicsPerRow, row = i / relicsPerRow;
                        var relicRect = new Rectangle(PadX + col * (RelicTile + Gap), y + row * (RelicH + Gap), RelicTile, RelicH);
                        _hitMap.Add(new HitEntry(relicRect, sec.Relics[i].Id, true));
                        DrawRelicTile(g, sec.Relics[i], relicRect);
                    }
                    y += relicRows * (RelicH + Gap);
                }
                else if (sec.Cards.Count == 0)
                {
                    using var fgNone = new SolidBrush(SystemColors.GrayText);
                    using var fontNone = new Font("Segoe UI", 8.5f);
                    g.DrawString(jaMode ? "なし" : "None", fontNone, fgNone, new PointF(PadX + 4, y + 8));
                    y += RelicH;
                }
            }

            y += SectionGap;
        }

        return bmp;
    }

    // 戦闘中に消滅する（＝戦闘終了まで残らない）カードか。Power・廃棄・幽体・消滅付与エンチャント。
    // 旧「デッキ枚数理論値」フォームの分類ロジックをそのまま移設。
    static bool IsDisposable(DeckCard c) =>
        c.Type == "Power"
        || CardDatabaseService.IsExhaustKeyword(c.Id)
        || CardDatabaseService.IsEtherealKeyword(c.Id)
        || CardDatabaseService.IsExhaustGainingEnchantment(c.EnchantmentId);

    List<(string Label, List<DeckCard> Cards, List<RelicEntry> Relics)> BuildKeywordGroups(
        IReadOnlyList<DeckCard> cards, IReadOnlyList<RelicEntry> relics)
    {
        bool ja = AppLanguage.IsJapanese;
        var assignedCards  = new HashSet<DeckCard>();
        var assignedRelics = new HashSet<RelicEntry>();
        var result = new List<(string Label, List<DeckCard> Cards, List<RelicEntry> Relics)>();

        foreach (var (labelEn, labelJa, filter) in _keywordGroups!)
        {
            var cardGroup  = cards.Where(c => filter(c.Id)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList();
            var relicGroup = relics.Where(r => filter(r.Id)).OrderBy(r => ja ? r.NameJa : r.NameEn).ToList();
            foreach (var c in cardGroup)  assignedCards.Add(c);
            foreach (var r in relicGroup) assignedRelics.Add(r);
            result.Add((ja ? labelJa : labelEn, cardGroup, relicGroup));
        }

        // 「戦闘中に消滅する」グループ（旧フォームの統合）。メカニクス群の後・「その他」の直前に出し、
        // 該当カードは assignedCards に登録して「その他」と重複させない。
        if (_showDisposableGroup)
        {
            var disposable = cards.Where(IsDisposable).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList();
            foreach (var c in disposable) assignedCards.Add(c);
            result.Add((ja ? "プレイすると消滅する" : "Disappears in Battle", disposable, new List<RelicEntry>()));
        }

        var otherCards  = cards.Where(c => !assignedCards.Contains(c)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList();
        var otherRelics = relics.Where(r => !assignedRelics.Contains(r)).OrderBy(r => ja ? r.NameJa : r.NameEn).ToList();
        if (otherCards.Count > 0 || otherRelics.Count > 0)
            result.Add((ja ? "その他" : "Other", otherCards, otherRelics));
        return result;
    }

    Bitmap ComposeImageKeyword(int availableWidth)
    {
        bool ja = AppLanguage.IsJapanese;
        var groups = BuildKeywordGroups(_cards ?? [], _relics ?? []);
        int cardsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (CardW + Gap));
        int relicsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (RelicTile + Gap));
        int deckTotal = _deckTotalOverride ?? (_cards ?? []).Sum(c => c.Count);

        int totalHeight = PadY;
        foreach (var (_, groupCards, groupRelics) in groups)
        {
            int cardRows  = groupCards.Count  > 0 ? (groupCards.Count  + cardsPerRow - 1) / cardsPerRow : 0;
            int relicRows = groupRelics.Count > 0 ? (groupRelics.Count + relicsPerRow - 1) / relicsPerRow : 0;
            totalHeight += HeaderH;
            if (cardRows  > 0) totalHeight += cardRows  * (CardH  + Gap);
            if (relicRows > 0) totalHeight += relicRows * (RelicH + Gap) + (cardRows > 0 ? Gap : 0);
            if (cardRows == 0 && relicRows == 0) totalHeight += RelicH;
            totalHeight += SectionGap;
        }
        totalHeight += PadY;

        _hitMap.Clear();
        var bmp = new Bitmap(availableWidth, Math.Max(totalHeight, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int y = PadY;
        foreach (var (label, groupCards, groupRelics) in groups)
        {
            var countStr = FormatCardCount(groupCards.Sum(c => c.Count), deckTotal, ja);
            DrawSectionHeader(g, label, countStr, new Rectangle(PadX, y, availableWidth - 2 * PadX, HeaderH));
            y += HeaderH;

            int cardRows = groupCards.Count > 0 ? (groupCards.Count + cardsPerRow - 1) / cardsPerRow : 0;
            for (int i = 0; i < groupCards.Count; i++)
            {
                int col = i % cardsPerRow, row = i / cardsPerRow;
                var cardRect = new Rectangle(PadX + col * (CardW + Gap), y + row * (CardH + Gap), CardW, CardH);
                _hitMap.Add(new HitEntry(cardRect, groupCards[i].Id, false, groupCards[i].EnchantmentId, groupCards[i].EnchantmentAmount));
                DrawCard(g, groupCards[i], cardRect);
            }
            if (cardRows > 0) y += cardRows * (CardH + Gap);

            if (groupRelics.Count > 0)
            {
                if (cardRows > 0) y += Gap;
                int relicRows = (groupRelics.Count + relicsPerRow - 1) / relicsPerRow;
                for (int i = 0; i < groupRelics.Count; i++)
                {
                    int col = i % relicsPerRow, row = i / relicsPerRow;
                    var relicRect = new Rectangle(PadX + col * (RelicTile + Gap), y + row * (RelicH + Gap), RelicTile, RelicH);
                    _hitMap.Add(new HitEntry(relicRect, groupRelics[i].Id, true));
                    DrawRelicTile(g, groupRelics[i], relicRect);
                }
                y += relicRows * (RelicH + Gap);
            }
            else if (groupCards.Count == 0)
            {
                using var fgNone = new SolidBrush(SystemColors.GrayText);
                using var fontNone = new Font("Segoe UI", 8.5f);
                g.DrawString(ja ? "なし" : "None", fontNone, fgNone, new PointF(PadX + 4, y + 8));
                y += RelicH;
            }

            y += SectionGap;
        }

        return bmp;
    }

    void DrawCard(Graphics g, DeckCard card, Rectangle rect)
    {
        var thumbnail = GetCardThumbnail(card.Id, card.Type);
        if (thumbnail != null)
            g.DrawImage(thumbnail, rect);
        else
            DrawPlaceholder(g, card.NameEn, rect);

        if (card.IsUpgraded)
        {
            using var outerPen = new Pen(Color.FromArgb(255, 220, 30, 30), 3f);
            g.DrawRectangle(outerPen, rect);
            DrawUpgradeBadge(g, rect);
        }
        else
        {
            using var borderPen = new Pen(Color.FromArgb(100, 0, 0, 0));
            g.DrawRectangle(borderPen, rect);
        }

        DrawCostBadge(g, card.Cost, rect);
        DrawCountBadge(g, card.Count, rect);

        if (!string.IsNullOrEmpty(card.EnchantmentId))
            DrawEnchantBadge(g, card.EnchantmentId, rect);
    }

    static void DrawUpgradeBadge(Graphics g, Rectangle cardRect)
    {
        var r = new Rectangle(cardRect.Right - 26, cardRect.Y + 2, 24, 24);
        // 白縁
        using var outline = new Pen(Color.White, 2f);
        g.DrawEllipse(outline, r);
        // 赤背景
        using var bg = new SolidBrush(Color.FromArgb(230, 210, 20, 20));
        g.FillEllipse(bg, r);
        // 「+」テキスト
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var fg = new SolidBrush(Color.White);
        using var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString("+", font, fg, (RectangleF)r, fmt);
    }

    static void DrawEnchantBadge(Graphics g, string enchantmentId, Rectangle cardRect)
    {
        const int Size = 26;
        var r = new Rectangle(cardRect.X + 2, cardRect.Bottom - Size - 2, Size, Size);

        // 半透明の暗い背景 + 白縁
        using var bg = new SolidBrush(Color.FromArgb(160, 20, 20, 20));
        g.FillEllipse(bg, r);
        using var outline = new Pen(Color.White, 1.5f);
        g.DrawEllipse(outline, r);

        var icon = Services.EnchantmentIconService.GetEnchantmentBitmap(enchantmentId);
        if (icon is not null)
        {
            const int Pad = 3;
            var iconRect = new Rectangle(r.X + Pad, r.Y + Pad, r.Width - Pad * 2, r.Height - Pad * 2);
            var oldInterp = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(icon, iconRect);
            g.InterpolationMode = oldInterp;
        }
        else
        {
            // アイコン未取得時のフォールバック
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var fg = new SolidBrush(Color.White);
            using var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString("✦", font, fg, (RectangleF)r, fmt);
        }
    }

    static void DrawPlaceholder(Graphics g, string name, Rectangle rect)
    {
        using var bg = new SolidBrush(Color.FromArgb(200, 200, 200));
        g.FillRectangle(bg, rect);
        using var font = new Font("Segoe UI", 7f);
        using var fg = new SolidBrush(Color.DimGray);
        using var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord,
        };
        g.DrawString(name, font, fg, (RectangleF)rect, fmt);
    }

    static void DrawCostBadge(Graphics g, string cost, Rectangle cardRect)
    {
        if (string.IsNullOrEmpty(cost) || cost == "-") return;
        var r = new Rectangle(cardRect.X + 2, cardRect.Y + 2, 22, 22);
        using var bg = new SolidBrush(Color.FromArgb(210, 20, 20, 20));
        g.FillEllipse(bg, r);
        using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var fg = new SolidBrush(Color.White);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(cost, font, fg, (RectangleF)r, fmt);
    }

    static void DrawCountBadge(Graphics g, int count, Rectangle cardRect)
    {
        if (count <= 1) return;
        string text = $"×{count}";
        using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        var sz = g.MeasureString(text, font);
        float bw = sz.Width + 4, bh = sz.Height + 2;
        var r = new RectangleF(cardRect.Right - bw - 1, cardRect.Bottom - bh - 1, bw, bh);
        using var bg = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
        g.FillRectangle(bg, r);
        using var fg = new SolidBrush(Color.White);
        g.DrawString(text, font, fg, r.X + 2, r.Y + 1);
    }

    static void DrawSectionHeader(Graphics g, string label, string countText, Rectangle rect)
    {
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var brush = new SolidBrush(SystemColors.ControlText);
        float textH = font.GetHeight(g);
        g.DrawString($"── {label}  ({countText})", font, brush,
            new PointF(rect.X, rect.Y + (rect.Height - textH) / 2f));
    }

    static void DrawRelicTile(Graphics g, RelicEntry relic, Rectangle rect)
    {
        var rarity = CardDatabaseService.GetRelicRarity(relic.Id);
        Color bgColor = rarity switch {
            "Rare"     => Color.FromArgb(255, 240, 195),
            "Uncommon" => Color.FromArgb(210, 240, 215),
            "Shop"     => Color.FromArgb(235, 215, 255),
            "Event"    => Color.FromArgb(210, 245, 240),
            "Ancient"  => Color.FromArgb(255, 225, 195),
            "Starter"  => Color.FromArgb(220, 220, 225),
            _          => Color.FromArgb(228, 228, 228),
        };
        Color fgColor = rarity switch {
            "Rare"     => Color.FromArgb(100, 60, 0),
            "Uncommon" => Color.FromArgb(20, 90, 40),
            "Shop"     => Color.FromArgb(70, 20, 120),
            "Event"    => Color.FromArgb(10, 90, 80),
            "Ancient"  => Color.FromArgb(120, 60, 0),
            _          => Color.DimGray,
        };
        using var bg = new SolidBrush(bgColor);
        g.FillRectangle(bg, rect);
        using var borderPen = new Pen(Color.FromArgb(120, fgColor.R, fgColor.G, fgColor.B));
        g.DrawRectangle(borderPen, rect);

        const int ImgPad = 2;
        // 個別 PNG（relic_images.json → relics_png/）を表示。atlas 切り出しは使わない。
        var img = StS2Toys.Services.RelicImageService.GetRelicPng(relic.Id);
        if (img is not null)
        {
            // アスペクト比を保持してタイル内に内接・中央寄せ。
            int boxW = rect.Width - ImgPad * 2;
            int boxH = rect.Height - ImgPad * 2;
            double scale = Math.Min((double)boxW / img.Width, (double)boxH / img.Height);
            int w = Math.Max(1, (int)Math.Round(img.Width * scale));
            int h = Math.Max(1, (int)Math.Round(img.Height * scale));
            int x = rect.X + (rect.Width - w) / 2;
            int y = rect.Y + (rect.Height - h) / 2;
            var oldInterp = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, new Rectangle(x, y, w, h));
            g.InterpolationMode = oldInterp;
            return;
        }

        // フォールバック：PNG が未マッピング／未生成のときは名前を中央表示（空白を避ける）。
        using var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        var name = AppLanguage.IsJapanese ? relic.NameJa : relic.NameEn;
        using var font = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var fg = new SolidBrush(fgColor);
        g.DrawString(name, font, fg,
            new RectangleF(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), fmt);
    }

    Bitmap? GetCardThumbnail(string cardId, string type)
    {
        var cacheKey = cardId + "|" + type;
        if (_imageCache.TryGetValue(cacheKey, out var cached)) return cached;

        var path = CardImageViewerForm.FindCardImage(cardId, type);
        if (path is not null)
        {
            try
            {
                using var original = Image.FromFile(path);
                return CacheThumb(cacheKey, original);
            }
            catch { }
        }

        // フォールバック: カードアトラスから取得
        var atlasBmp = Services.CardAtlasService.GetCardBitmap(cardId);
        if (atlasBmp is null) { _imageCache[cacheKey] = null; return null; }
        try { return CacheThumb(cacheKey, atlasBmp); }
        catch { _imageCache[cacheKey] = null; return null; }
    }

    Bitmap? CacheThumb(string cacheKey, Image source)
    {
        var thumb = new Bitmap(CardW, CardH);
        using var tg = Graphics.FromImage(thumb);
        tg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        tg.DrawImage(source, 0, 0, CardW, CardH);
        _imageCache[cacheKey] = thumb;
        return thumb;
    }

    void OnPictureBoxClick(object? sender, MouseEventArgs e)
    {
        // セクションヘッダ（セクション表示時のみ）→ 折りたたみトグル。
        var header = _sectionHeaderMap.FirstOrDefault(h => h.Rect.Contains(e.Location));
        if (header.Key is not null)
        {
            if (!_collapsedSections.Remove(header.Key))
                _collapsedSections.Add(header.Key);
            RecomposeIfNeeded();
            return;
        }

        // カード／レリック → 設定済み外部リンク（URLテンプレート）をブラウザで開く。
        if (e.Button != MouseButtons.Left) return;
        var hit = _hitMap.FirstOrDefault(h => h.Rect.Contains(e.Location));
        if (hit is null) return;

        var kind = hit.IsRelic ? "relic" : "card";
        var links = SiteLinkService.BuildLinks(UrlTemplateService.Load(), kind, hit.Id);
        if (links.Count == 0) return;
        if (links.Count == 1) { OpenUrl(links[0].Url); return; }

        // 複数テンプレートはメニューで選択。
        _linkMenu.Items.Clear();
        foreach (var link in links)
        {
            var url = link.Url;
            var item = new ToolStripMenuItem($"{link.Label}: {link.Url}");
            item.Click += (_, _) => OpenUrl(url);
            _linkMenu.Items.Add(item);
        }
        _linkMenu.Show(_pictureBox, e.Location);
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    void OnPictureBoxMouseMove(object? sender, MouseEventArgs e)
    {
        if (_sections is not null && _sectionHeaderMap.Any(h => h.Rect.Contains(e.Location)))
        {
            _pictureBox.Cursor = Cursors.Hand;
            _hoverTip.Hide(_pictureBox);
            _hoveredId = null;
            return;
        }
        _pictureBox.Cursor = Cursors.Default;
        var hit = _hitMap.FirstOrDefault(h => h.Rect.Contains(e.Location));
        if (hit is not null) _pictureBox.Cursor = Cursors.Hand; // クリックでリンクを開ける示唆
        if (hit?.Id == _hoveredId) return;
        _hoveredId = hit?.Id;
        if (hit is null) { _hoverTip.Hide(_pictureBox); return; }
        _hoverTip.Show(BuildTooltipText(hit), _pictureBox, e.X + 16, e.Y + 16);
    }

    static string BuildTooltipText(HitEntry hit)
    {
        bool ja = AppLanguage.IsJapanese;
        var name = CardDatabaseService.GetName(hit.Id, japanese: ja);
        var (descEn, descJa) = CardDatabaseService.GetDescription(hit.Id);
        var stats = hit.IsRelic ? null : CardDatabaseService.GetCardStats(hit.Id);
        var descText = DescriptionFormatter.Resolve(ja ? descJa : descEn, stats, japanese: ja);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(name);
        if (!string.IsNullOrWhiteSpace(descText)) sb.AppendLine(descText);

        if (!hit.IsRelic && !string.IsNullOrEmpty(hit.EnchantmentId))
        {
            var enchLabel = CardDatabaseService.FormatEnchantmentLabel(hit.EnchantmentId, hit.EnchantmentAmount, japanese: ja);
            sb.AppendLine();
            sb.Append($"[{enchLabel}]");
        }

        return sb.ToString().Trim();
    }

    static int TypeOrder(string type) => type switch
    {
        "Attack" => 0,
        "Skill"  => 1,
        "Power"  => 2,
        "Curse"  => 3,
        "Status" => 4,
        "Quest"  => 5,
        _        => 6
    };

    static string FormatCardCount(int groupCount, int deckTotal, bool japanese)
    {
        double pct = deckTotal > 0 ? 100.0 * groupCount / deckTotal : 0;
        return japanese
            ? $"{deckTotal}枚中{groupCount}枚（{pct:F0}%）"
            : $"{groupCount} / {deckTotal} ({pct:F0}%)";
    }

    static string TypeLabel(string type) => AppLanguage.IsJapanese
        ? type switch
        {
            "Attack" => "アタック",
            "Skill"  => "スキル",
            "Power"  => "パワー",
            "Curse"  => "呪い",
            "Status" => "状態異常",
            "Quest"  => "クエスト",
            _ => type.Length > 0 ? type : "その他"
        }
        : (type.Length > 0 ? type : "Other");
}
