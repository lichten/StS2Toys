using System.Drawing.Imaging;
using StS2Toys.Services;

namespace StS2Toys;

public record DeckCard(string Id, string NameEn, string NameJa, string Cost, string Type, int Count, bool IsUpgraded = false);
public record RelicEntry(string Id, string NameEn, string NameJa);

public partial class DeckOverviewForm : Form
{
    const int CardW = 120, CardH = 91, RelicH = 56, Gap = 4, PadX = 8, PadY = 8, HeaderH = 28, SectionGap = 8;

    private IReadOnlyList<DeckCard>? _cards;
    private IReadOnlyList<RelicEntry>? _relics;
    private readonly Dictionary<string, Bitmap?> _imageCache = new();

    public DeckOverviewForm()
    {
        InitializeComponent();
        VisibleChanged += (_, _) => { if (Visible) RecomposeIfNeeded(); };
        ResizeEnd += (_, _) => RecomposeIfNeeded();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var bmp in _imageCache.Values) bmp?.Dispose();
            _pictureBox.Image?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void UpdateDeck(IReadOnlyList<DeckCard> cards)
    {
        _cards = cards;
        if (Visible) RecomposeIfNeeded();
    }

    public void UpdateRelics(IReadOnlyList<RelicEntry> relics)
    {
        _relics = relics;
        if (Visible) RecomposeIfNeeded();
    }

    public void SetBlockStats(int blockCount, int totalCount, int relicCount)
    {
        double pct = totalCount > 0 ? 100.0 * blockCount / totalCount : 0;
        var relicPart = relicCount > 0 ? $"  レリック: {relicCount}個" : "";
        _statsLabel.Text = $"ブロック関連: {blockCount}枚 / デッキ全体: {totalCount}枚 ({pct:F0}%){relicPart}";
        _statsPanel.Visible = true;
        Text = "ブロック関連カード概観";
    }

    void RecomposeIfNeeded()
    {
        if (_cards is null) return;
        var w = _scrollPanel.ClientSize.Width;
        if (w <= 0) return;

        var bmp = ComposeImage(w);
        var oldImage = _pictureBox.Image;
        _pictureBox.Size = new Size(bmp.Width, bmp.Height);
        _pictureBox.Image = bmp;
        oldImage?.Dispose();
    }

    Bitmap ComposeImage(int availableWidth)
    {
        var groups = (_cards ?? [])
            .GroupBy(c => c.Type)
            .OrderBy(g => TypeOrder(g.Key))
            .Select(g => (Label: TypeLabel(g.Key), Cards: g.OrderBy(c => c.NameJa).ToList()))
            .ToList();
        var relics = _relics ?? [];

        int cardsPerRow = Math.Max(1, (availableWidth - 2 * PadX + Gap) / (CardW + Gap));

        int totalHeight = PadY;
        foreach (var (_, cards) in groups)
        {
            int rows = (cards.Count + cardsPerRow - 1) / cardsPerRow;
            totalHeight += HeaderH + rows * (CardH + Gap) + SectionGap;
        }
        int relicRows = relics.Count > 0 ? (relics.Count + cardsPerRow - 1) / cardsPerRow : 1;
        totalHeight += HeaderH + relicRows * (RelicH + Gap) + SectionGap;
        totalHeight += PadY;

        var bmp = new Bitmap(availableWidth, Math.Max(totalHeight, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int y = PadY;
        foreach (var (label, cards) in groups)
        {
            DrawSectionHeader(g, label, $"{cards.Sum(c => c.Count)}枚",
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
                DrawCard(g, cards[i], cardRect);
            }

            int totalRows = (cards.Count + cardsPerRow - 1) / cardsPerRow;
            y += totalRows * (CardH + Gap) + SectionGap;
        }

        DrawSectionHeader(g, "レリック", relics.Count > 0 ? $"{relics.Count}個" : "なし",
            new Rectangle(PadX, y, availableWidth - 2 * PadX, HeaderH));
        y += HeaderH;
        if (relics.Count > 0)
        {
            for (int i = 0; i < relics.Count; i++)
            {
                int col = i % cardsPerRow;
                int row = i / cardsPerRow;
                DrawRelicTile(g, relics[i], new Rectangle(
                    PadX + col * (CardW + Gap),
                    y + row * (RelicH + Gap),
                    CardW, RelicH));
            }
        }
        else
        {
            using var fgNone = new SolidBrush(SystemColors.GrayText);
            using var fontNone = new Font("Segoe UI", 8.5f);
            g.DrawString("なし", fontNone, fgNone, new PointF(PadX + 4, y + 8));
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
        using var bg = new SolidBrush(Color.FromArgb(220, 235, 255));
        g.FillRectangle(bg, rect);
        using var borderPen = new Pen(Color.FromArgb(120, 80, 100, 180));
        g.DrawRectangle(borderPen, rect);

        const int ImgPad = 2;
        int imgSize = rect.Height - ImgPad * 2;
        var img = RelicImageService.GetRelicBitmap(relic.Id);
        if (img is not null)
        {
            var oldInterp = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, new Rectangle(rect.X + ImgPad, rect.Y + ImgPad, imgSize, imgSize));
            g.InterpolationMode = oldInterp;
        }

        // テキストエリア：画像の右側（画像なしの場合は全幅）
        int textX = rect.X + (img is not null ? imgSize + ImgPad * 2 + 2 : 4);
        int textW = rect.Right - textX - 2;
        if (textW > 8)
        {
            using var fmt = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            int halfH = rect.Height / 2;
            using var fontJa = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var fgJa = new SolidBrush(Color.DarkSlateBlue);
            g.DrawString(relic.NameJa, fontJa, fgJa,
                new RectangleF(textX, rect.Y, textW, halfH), fmt);

            using var fontEn = new Font("Segoe UI", 6f);
            using var fgEn = new SolidBrush(Color.FromArgb(150, 50, 50, 120));
            g.DrawString(relic.NameEn, fontEn, fgEn,
                new RectangleF(textX, rect.Y + halfH, textW, halfH), fmt);
        }
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

    static string TypeLabel(string type) => type switch
    {
        "Attack" => "アタック",
        "Skill"  => "スキル",
        "Power"  => "パワー",
        "Curse"  => "呪い",
        "Status" => "状態異常",
        "Quest"  => "クエスト",
        _ => type.Length > 0 ? type : "その他"
    };
}
