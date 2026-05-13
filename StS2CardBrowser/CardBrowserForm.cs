using System.Runtime.InteropServices;
using StS2Shared.Services;

namespace StS2CardBrowser;

record CardEntry(string Id, string NameEn, string NameJa, string Type, string Rarity, string Cost, string Character);

public class CardBrowserForm : Form
{
    const int SidebarBtnH = 28;

    // ---- キャラクター × メカニクス定義 ----
    static readonly (string CharLabel, (string MecLabel, Func<string, bool> Filter)[] Mechanics)[] Characters =
    [
        ("Necrobinder",
        [
            ("Osty",  CardDatabaseService.IsNecroOsty),
            ("Soul",  CardDatabaseService.IsNecroSoul),
            ("Doom",  CardDatabaseService.IsNecroDoom),
        ]),
        ("Ironclad",
        [
            ("Strength", CardDatabaseService.IsIroncladStrength),
            ("Exhaust",  CardDatabaseService.IsIroncladExhaust),
        ]),
        ("Silent",
        [
            ("Poison", CardDatabaseService.IsSilentPoison),
            ("Shiv",   CardDatabaseService.IsSilentShiv),
        ]),
        ("Defect",
        [
            ("Channel", CardDatabaseService.IsDefectChannel),
            ("Evoke",   CardDatabaseService.IsDefectEvoke),
            ("Focus",   CardDatabaseService.IsDefectFocus),
        ]),
        ("Regent",
        [
            ("Forge / Sovereign Blade", id => CardDatabaseService.IsRegentForge(id) || CardDatabaseService.IsRegentBlade(id)),
            ("カード作成シナジー",        CardDatabaseService.IsRegentCreate),
        ]),
        ("その他", []),
    ];

    // ---- データ ----
    List<CardEntry> _allCards = [];
    List<CardEntry> _filtered = [];
    bool _isJp = true;

    // ---- レア度・コスト定義 ----
    static readonly string[] Rarities = ["Common", "Uncommon", "Rare", "Starter", "Event", "Shop", "Ancient"];
    static readonly string[] Costs = ["0", "1", "2", "3+", "X"];

    // ---- フィルタ状態 ----
    int _selectedChar = -1;
    readonly HashSet<int> _selectedMechanics = [];
    readonly HashSet<string> _selectedRarities = [];
    readonly HashSet<string> _selectedCosts = [];

    // ---- UI コントロール ----
    TextBox _filterBox = null!;
    Button _btnJp = null!, _btnEn = null!;
    Panel _charPanel = null!;
    Panel _subPanel = null!;
    Panel _rarityPanel = null!;
    Panel _costPanel = null!;
    ListView _cardList = null!;
    ImageList _imageList = null!;
    readonly Dictionary<string, Bitmap> _thumbCache = new(StringComparer.OrdinalIgnoreCase);
    bool _populatingList;
    RichTextBox _detailBox = null!;
    Label _countLabel = null!;
    SplitContainer _outerSplit = null!;
    SplitContainer _split = null!;
    Button[] _charButtons = [];
    Button[] _subButtons = [];
    Button[] _rarityButtons = [];
    Button[] _costButtons = [];

    // ---- Win32 アイコン間隔制御 ----
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    const int LVM_SETICONSPACING = 0x1035;

    // ---- カード画像 ----
    PictureBox _portraitBox = null!;
    CardEntry? _currentCard;
    List<string> _currentSynergies = [];
    Image? _portraitImage;

    static readonly string _portraitBaseDir = ComputePortraitBaseDir();
    static readonly Dictionary<string, string> _charToDir = new(StringComparer.Ordinal)
    {
        ["Ironclad"]    = "ironclad",
        ["Silent"]      = "silent",
        ["Defect"]      = "defect",
        ["Necrobinder"] = "necrobinder",
        ["Regent"]      = "regent",
    };
    static readonly string[] _otherDirs = ["colorless", "curse", "event", "status", "token", "quest"];

    static string ComputePortraitBaseDir()
    {
        // AppContext.BaseDirectory は末尾に \ が付くため、先に除去してから4階層上がる
        var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < 4; i++)
            dir = Path.GetDirectoryName(dir) ?? dir;
        return Path.Combine(dir, "tools", "extracted", "images", "card_portraits_png");
    }

    public CardBrowserForm()
    {
        Text = "StS2 Card Browser";
        Size = new Size(1200, 700);
        MinimumSize = new Size(900, 500);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadCards();
        ApplyFilter();
    }

    // ---- UI 構築 ----

    void BuildUi()
    {
        // 外側スプリッター: Panel1=フィルタサイドバー / Panel2=コンテンツ
        _outerSplit = new SplitContainer { Dock = DockStyle.Fill };
        Controls.Add(_outerSplit);

        BuildFilterPanel();
        BuildContentPanel();
    }

    void BuildFilterPanel()
    {
        var panel = _outerSplit.Panel1;
        panel.Padding = new Padding(4);

        // 件数ラベル（下部に固定）
        _countLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray,
        };
        panel.Controls.Add(_countLabel);  // Dock=Bottom → 下部固定

        // サブフィルタパネル（キャラ選択時のみ表示）
        _subPanel = new Panel { Dock = DockStyle.Top, Height = 0, Visible = false };
        panel.Controls.Add(_subPanel);

        // キャラクターボタンパネル
        _charPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = SidebarBtnH * (Characters.Length + 1),
        };
        var charBtns = new List<Button>();
        for (int i = Characters.Length - 1; i >= 0; i--)
        {
            int idx = i;
            charBtns.Insert(0, AddSidebarButton(_charPanel, Characters[i].CharLabel, () => SelectChar(idx)));
        }
        charBtns.Insert(0, AddSidebarButton(_charPanel, "全て", () => SelectChar(-1)));
        _charButtons = [.. charBtns];
        panel.Controls.Add(_charPanel);

        // セパレータ（レア度 ↔ キャラ間）
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 6 });
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = SystemColors.ControlDark });
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 6 });

        // レア度フィルタパネル
        _rarityPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = SidebarBtnH * Rarities.Length,
        };
        var rarityBtns = new Button[Rarities.Length];
        for (int i = Rarities.Length - 1; i >= 0; i--)
        {
            string rarity = Rarities[i];
            int idx = i;
            rarityBtns[idx] = AddSidebarButton(_rarityPanel, rarity, () => ToggleRarity(rarity));
        }
        _rarityButtons = rarityBtns;
        panel.Controls.Add(_rarityPanel);

        // セパレータ（コスト ↔ レア度間）
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 6 });
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = SystemColors.ControlDark });
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 6 });

        // コストフィルタパネル
        _costPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = SidebarBtnH * Costs.Length,
        };
        var costBtns = new Button[Costs.Length];
        for (int i = Costs.Length - 1; i >= 0; i--)
        {
            string cost = Costs[i];
            costBtns[i] = AddSidebarButton(_costPanel, cost, () => ToggleCost(cost));
        }
        _costButtons = costBtns;
        panel.Controls.Add(_costPanel);

        // セパレータ（言語 ↔ コスト間）
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 4 });
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = SystemColors.ControlDark });
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 4 });

        // JP / EN 言語切替
        var langPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 2),
        };
        _btnJp = MakeLangButton("JP", true);
        _btnEn = MakeLangButton("EN", false);
        langPanel.Controls.AddRange([_btnJp, _btnEn]);
        panel.Controls.Add(langPanel);

        // 検索ボックス（最後に追加 → 最上部）
        _filterBox = new TextBox
        {
            Dock = DockStyle.Top,
            PlaceholderText = "カード名・ID で検索",
        };
        _filterBox.TextChanged += (_, _) => ApplyFilter();
        panel.Controls.Add(_filterBox);
    }

    void BuildContentPanel()
    {
        // 内側スプリッター: Panel1=リスト / Panel2=詳細
        _split = new SplitContainer { Dock = DockStyle.Fill };
        _outerSplit.Panel2.Controls.Add(_split);

        _imageList = new ImageList
        {
            ImageSize = new Size(120, 91),
            ColorDepth = ColorDepth.Depth32Bit,
        };

        _cardList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            LargeImageList = _imageList,
            MultiSelect = false,
            HideSelection = false,
            Font = new Font("Meiryo", 8f),
        };
        _cardList.SelectedIndexChanged += OnCardSelected;
        _split.Panel1.Controls.Add(_cardList);

        // 説明テキスト（先に追加 → Dock=Fill で残りを埋める）
        _detailBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Font = new Font("Meiryo", 9.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true,
        };
        _split.Panel2.Controls.Add(_detailBox);

        // カードポートレート（後から追加 → Dock=Top で上部に配置）
        _portraitBox = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 280,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(30, 30, 30),
        };
        _portraitBox.Paint += OnPortraitPaint;
        _split.Panel2.Controls.Add(_portraitBox);
        _split.Panel2.Resize += (_, _) => UpdatePortraitHeight();
    }

    void UpdatePortraitHeight()
    {
        var img = _portraitBox.Image;
        if (img is null) { _portraitBox.Height = 280; return; }
        float aspect = (float)img.Width / img.Height;
        int h = (int)(_split.Panel2.ClientSize.Width / aspect);
        _portraitBox.Height = Math.Clamp(h, 120, 460);
    }

    // サイドバー用の全幅ボタンを parent に追加して返す
    Button AddSidebarButton(Panel parent, string label, Action onClick, int leftPad = 6)
    {
        var btn = new Button
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = SidebarBtnH,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(leftPad, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        parent.Controls.Add(btn);
        return btn;
    }

    Button MakeLangButton(string text, bool isJp)
    {
        var btn = new Button
        {
            Text = text,
            Width = 40,
            Height = 24,
            Margin = new Padding(0, 0, 4, 0),
            FlatStyle = FlatStyle.Flat,
        };
        btn.Click += (_, _) =>
        {
            _isJp = isJp;
            RefreshLangButtons();
            ClearThumbCache();
            PopulateList();
        };
        return btn;
    }

    // ---- データ読み込み ----

    void LoadCards()
    {
        _allCards = CardDatabaseService.GetAllCardIds()
            .Select(id => new CardEntry(
                id,
                CardDatabaseService.GetName(id, japanese: false),
                CardDatabaseService.GetName(id, japanese: true),
                CardDatabaseService.GetCardType(id),
                CardDatabaseService.GetCardRarity(id),
                CardDatabaseService.GetCardCost(id),
                CardDatabaseService.GetCardCharacter(id)))
            .OrderBy(c => _isJp ? c.NameJa : c.NameEn)
            .ToList();
    }

    // ---- フィルタ ----

    void SelectChar(int charIndex)
    {
        _selectedChar = charIndex;
        _selectedMechanics.Clear();
        RebuildSubPanel();
        RefreshCharButtons();
        ApplyFilter();
    }

    void ToggleCost(string cost)
    {
        if (!_selectedCosts.Add(cost))
            _selectedCosts.Remove(cost);
        RefreshCostButtons();
        ApplyFilter();
    }

    static bool MatchesCostFilter(string cardCost, string filter) => filter switch
    {
        "3+" => int.TryParse(cardCost, out var n) && n >= 3,
        _    => cardCost == filter,
    };

    void ToggleRarity(string rarity)
    {
        if (!_selectedRarities.Add(rarity))
            _selectedRarities.Remove(rarity);
        RefreshRarityButtons();
        ApplyFilter();
    }

    void ToggleMechanic(int mecIndex)
    {
        if (!_selectedMechanics.Add(mecIndex))
            _selectedMechanics.Remove(mecIndex);
        RefreshSubButtons();
        ApplyFilter();
    }

    void ApplyFilter()
    {
        var query = _allCards.AsEnumerable();

        // キャラクターフィルタ
        if (_selectedChar >= 0)
        {
            var charLabel = Characters[_selectedChar].CharLabel;
            var mechanics = Characters[_selectedChar].Mechanics;
            if (_selectedMechanics.Count > 0)
                // サブメカニクス選択時: そのシナジーに該当するカード
                query = query.Where(c => _selectedMechanics.Any(i => mechanics[i].Filter(c.Id)));
            else
                // キャラクター選択のみ: キャラクター帰属データで絞り込む
                // 「その他」は帰属なし（空文字）のカードを対象とする
                query = query.Where(c => charLabel == "その他"
                    ? c.Character == ""
                    : c.Character == charLabel);
        }

        // コストフィルタ
        if (_selectedCosts.Count > 0)
            query = query.Where(c => _selectedCosts.Any(f => MatchesCostFilter(c.Cost, f)));

        // レア度フィルタ
        if (_selectedRarities.Count > 0)
            query = query.Where(c => _selectedRarities.Contains(c.Rarity));

        // テキスト検索
        var text = _filterBox.Text.Trim();
        if (!string.IsNullOrEmpty(text))
            query = query.Where(c =>
                c.NameEn.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                c.NameJa.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                c.Id.Contains(text, StringComparison.OrdinalIgnoreCase));

        _filtered = [.. query.OrderBy(c => _isJp ? c.NameJa : c.NameEn)];
        PopulateList();
    }

    void PopulateList()
    {
        _populatingList = true;
        _cardList.BeginUpdate();
        _cardList.Items.Clear();
        _imageList.Images.Clear();

        ListViewItem? selectItem = null;
        for (int i = 0; i < _filtered.Count; i++)
        {
            var c = _filtered[i];
            _imageList.Images.Add(GetOrCreateThumb(c));
            var item = new ListViewItem("", i) { Tag = c };
            _cardList.Items.Add(item);
            if (c.Id == _currentCard?.Id) selectItem = item;
        }

        _cardList.EndUpdate();
        _populatingList = false;

        _countLabel.Text = $"{_filtered.Count} 件";

        selectItem ??= _filtered.Count > 0 ? _cardList.Items[0] : null;
        if (selectItem is not null)
        {
            selectItem.Selected = true;
            selectItem.Focused = true;
            _cardList.EnsureVisible(_cardList.Items.IndexOf(selectItem));
            ShowDetail((CardEntry)selectItem.Tag!);
        }
        else
        {
            ShowDetail(null);
        }
    }

    // ---- 詳細表示 ----

    void OnCardSelected(object? sender, EventArgs e)
    {
        if (_populatingList) return;
        if (_cardList.SelectedItems.Count == 0) return;
        ShowDetail((CardEntry)_cardList.SelectedItems[0].Tag!);
    }

    void ShowDetail(CardEntry? card)
    {
        _currentCard = card;
        _currentSynergies = card is null ? [] : CollectSynergies(card.Id);
        ShowPortrait(card);

        _detailBox.Clear();
        if (card is null) return;

        var (descEn, descJa) = CardDatabaseService.GetDescription(card.Id);
        var stats = CardDatabaseService.GetCardStats(card.Id);
        var descEnClean = DescriptionFormatter.Resolve(descEn, stats);
        var descJaClean = DescriptionFormatter.Resolve(descJa, stats);

        var rtb = _detailBox;
        rtb.SuspendLayout();

        if (!string.IsNullOrEmpty(descJaClean))
        {
            AppendBold("説明 (JP)\n", 9.5f);
            AppendNormal(descJaClean + "\n", 9.5f);
        }
        if (!string.IsNullOrEmpty(descEnClean))
        {
            if (!string.IsNullOrEmpty(descJaClean)) AppendNormal("\n");
            AppendBold("説明 (EN)\n", 9.5f);
            AppendNormal(descEnClean + "\n", 9.5f);
        }

        rtb.ResumeLayout();
        rtb.SelectionStart = 0;
        rtb.ScrollToCaret();
    }

    void ShowPortrait(CardEntry? card)
    {
        var old = _portraitImage;
        _portraitImage = null;
        _portraitBox.Image = null;
        old?.Dispose();

        if (card is not null && Directory.Exists(_portraitBaseDir))
        {
            var path = ResolvePortraitPath(card);
            if (path is not null)
            {
                _portraitImage = Image.FromFile(path);
                _portraitBox.Image = _portraitImage;
            }
        }

        UpdatePortraitHeight();
        _portraitBox.Invalidate();
    }

    string? ResolvePortraitPath(CardEntry card)
    {
        var name = (card.Id.Contains('.') ? card.Id[(card.Id.IndexOf('.') + 1)..] : card.Id)
                   .ToLowerInvariant();

        if (card.Character != "" && _charToDir.TryGetValue(card.Character, out var dir))
        {
            var p = Path.Combine(_portraitBaseDir, dir, name + ".png");
            if (File.Exists(p)) return p;
        }

        foreach (var d in _otherDirs)
        {
            var p = Path.Combine(_portraitBaseDir, d, name + ".png");
            if (File.Exists(p)) return p;
        }

        var beta = Path.Combine(_portraitBaseDir, "beta.png");
        return File.Exists(beta) ? beta : null;
    }

    List<string> CollectSynergies(string id)
    {
        var result = new List<string>();
        foreach (var (charLabel, mechanics) in Characters)
            foreach (var (mecLabel, filter) in mechanics)
                if (filter(id))
                    result.Add($"{charLabel}:{mecLabel}");
        return result;
    }

    // ---- ポートレートオーバーレイ描画 ----

    void OnPortraitPaint(object? sender, PaintEventArgs e)
    {
        if (_currentCard is null) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var imgRect = _portraitBox.Image is not null
            ? GetZoomedRect(_portraitBox)
            : _portraitBox.ClientRectangle;

        if (_portraitBox.Image is null)
        {
            using var bg = new SolidBrush(Color.FromArgb(50, 50, 50));
            g.FillRectangle(bg, imgRect);
        }

        DrawCostBadge(g, imgRect, _currentCard.Cost);
        DrawTypeBadge(g, imgRect, _currentCard.Type);
        DrawBottomBar(g, imgRect, _currentCard);
    }

    static Rectangle GetZoomedRect(PictureBox pb)
    {
        if (pb.Image is null) return pb.ClientRectangle;
        float iw = pb.Image.Width, ih = pb.Image.Height;
        float bw = pb.ClientSize.Width, bh = pb.ClientSize.Height;
        float scale = Math.Min(bw / iw, bh / ih);
        int w = (int)(iw * scale), h = (int)(ih * scale);
        return new Rectangle((int)((bw - w) / 2), (int)((bh - h) / 2), w, h);
    }

    static void DrawCostBadge(Graphics g, Rectangle imgRect, string cost)
    {
        const int r = 20, margin = 8;
        var rect = new Rectangle(imgRect.X + margin, imgRect.Y + margin, r * 2, r * 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillEllipse(bgBrush, rect);
        using var font = new Font("Meiryo", 12, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(string.IsNullOrEmpty(cost) ? "?" : cost, font, Brushes.White, rect, sf);
    }

    static void DrawTypeBadge(Graphics g, Rectangle imgRect, string type)
    {
        var baseColor = type switch
        {
            "Attack" => Color.FromArgb(180, 55, 55),
            "Skill"  => Color.FromArgb(55, 95, 175),
            "Power"  => Color.FromArgb(115, 55, 175),
            _        => Color.FromArgb(95, 95, 95),
        };
        var bgColor = Color.FromArgb(190, baseColor.R, baseColor.G, baseColor.B);
        const int margin = 8, padX = 8, padY = 3;
        using var font = new Font("Meiryo", 9);
        var text = string.IsNullOrEmpty(type) ? "?" : type;
        var sz = g.MeasureString(text, font);
        int bw = (int)sz.Width + padX * 2, bh = (int)sz.Height + padY * 2;
        var rect = new Rectangle(imgRect.Right - bw - margin, imgRect.Y + margin, bw, bh);
        using var bgBrush = new SolidBrush(bgColor);
        FillRoundedRect(g, bgBrush, rect, 4);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, Brushes.White, rect, sf);
    }

    void DrawBottomBar(Graphics g, Rectangle imgRect, CardEntry card)
    {
        int barH = Math.Max(84, (int)(imgRect.Height * 0.32f));
        var barRect = new Rectangle(imgRect.X, imgRect.Bottom - barH, imgRect.Width, barH);

        using var grad = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Point(barRect.X, barRect.Y),
            new Point(barRect.X, barRect.Bottom),
            Color.FromArgb(0, 0, 0, 0),
            Color.FromArgb(215, 0, 0, 0));
        g.FillRectangle(grad, barRect);

        const int padX = 8;
        int y = barRect.Y + (int)(barH * 0.14f);

        // カード名
        var nameText = _isJp
            ? $"{card.NameJa}  /  {card.NameEn}"
            : $"{card.NameEn}  /  {card.NameJa}";
        using var nameFont = new Font("Meiryo", 12, FontStyle.Bold);
        g.DrawString(nameText, nameFont, Brushes.White,
            new RectangleF(imgRect.X + padX, y, imgRect.Width - padX * 2, 24));
        y += 26;

        // キャラクター · レア度 · ID
        var infoParts = new[] { card.Character, card.Rarity, card.Id }
            .Where(s => !string.IsNullOrEmpty(s));
        using var infoFont = new Font("Meiryo", 8);
        using var grayBrush = new SolidBrush(Color.FromArgb(210, 200, 200, 200));
        g.DrawString(string.Join("  ·  ", infoParts), infoFont, grayBrush,
            new RectangleF(imgRect.X + padX, y, imgRect.Width - padX * 2, 16));
        y += 20;

        // シナジーチップ
        if (_currentSynergies.Count > 0)
            DrawSynergyChips(g, imgRect.X + padX, y, _currentSynergies);
    }

    static void DrawSynergyChips(Graphics g, int x, int y, List<string> synergies)
    {
        using var font = new Font("Meiryo", 7.5f);
        using var bgBrush = new SolidBrush(Color.FromArgb(185, 35, 100, 55));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        const int padX = 5, padY = 2, gap = 4;
        foreach (var syn in synergies)
        {
            var sz = g.MeasureString(syn, font);
            var rect = new Rectangle(x, y, (int)sz.Width + padX * 2, (int)sz.Height + padY * 2);
            FillRoundedRect(g, bgBrush, rect, 3);
            g.DrawString(syn, font, Brushes.White, rect, sf);
            x += rect.Width + gap;
        }
    }

    static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int r)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    // ---- サムネイル ----

    Bitmap GetOrCreateThumb(CardEntry card)
    {
        if (_thumbCache.TryGetValue(card.Id, out var cached)) return cached;
        var thumb = CreateThumbnail(card);
        _thumbCache[card.Id] = thumb;
        return thumb;
    }

    Bitmap CreateThumbnail(CardEntry card)
    {
        var sz = _imageList.ImageSize;
        var bmp = new Bitmap(sz.Width, sz.Height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(40, 40, 40));
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var path = ResolvePortraitPath(card);
        if (path is not null)
        {
            try
            {
                using var orig = Image.FromFile(path);
                float scale = Math.Min((float)sz.Width / orig.Width, (float)sz.Height / orig.Height);
                int w = (int)(orig.Width * scale), h = (int)(orig.Height * scale);
                g.DrawImage(orig, (sz.Width - w) / 2, (sz.Height - h) / 2, w, h);
            }
            catch { }
        }

        DrawThumbCostBadge(g, card.Cost, sz);
        DrawThumbNameBar(g, _isJp ? card.NameJa : card.NameEn, sz);
        return bmp;
    }

    static void DrawThumbCostBadge(Graphics g, string cost, Size sz)
    {
        const int r = 9, margin = 3;
        var rect = new Rectangle(margin, margin, r * 2, r * 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        g.FillEllipse(bgBrush, rect);
        using var font = new Font("Meiryo", 7.5f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(string.IsNullOrEmpty(cost) ? "?" : cost, font, Brushes.White, rect, sf);
    }

    static void DrawThumbNameBar(Graphics g, string name, Size sz)
    {
        const int barH = 24;
        var barRect = new Rectangle(0, sz.Height - barH, sz.Width, barH);
        using var grad = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Point(barRect.X, barRect.Y),
            new Point(barRect.X, barRect.Bottom),
            Color.FromArgb(0, 0, 0, 0),
            Color.FromArgb(210, 0, 0, 0));
        g.FillRectangle(grad, barRect);
        using var font = new Font("Meiryo", 7f, FontStyle.Bold);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(name, font, Brushes.White, new RectangleF(4, barRect.Y, sz.Width - 8, barH), sf);
    }

    void ClearThumbCache()
    {
        foreach (var bmp in _thumbCache.Values) bmp.Dispose();
        _thumbCache.Clear();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _portraitImage?.Dispose();
        ClearThumbCache();
    }

    void AppendBold(string text, float size, Color? color = null)
    {
        _detailBox.SelectionFont = new Font("Meiryo", size, FontStyle.Bold);
        _detailBox.SelectionColor = color ?? Color.Black;
        _detailBox.AppendText(text);
    }

    void AppendNormal(string text, float size = 9.5f, Color? color = null)
    {
        _detailBox.SelectionFont = new Font("Meiryo", size, FontStyle.Regular);
        _detailBox.SelectionColor = color ?? Color.Black;
        _detailBox.AppendText(text);
    }

    // ---- ボタン状態の更新 ----

    void RefreshCharButtons()
    {
        // _charButtons[0] = 「全て」、[1] 以降がキャラクター
        for (int i = 0; i < _charButtons.Length; i++)
        {
            bool active = (i == 0 && _selectedChar == -1) || (i > 0 && _selectedChar == i - 1);
            _charButtons[i].BackColor = active ? SystemColors.Highlight : SystemColors.Control;
            _charButtons[i].ForeColor = active ? SystemColors.HighlightText : SystemColors.ControlText;
        }
    }

    void RebuildSubPanel()
    {
        _subPanel.Controls.Clear();
        _subButtons = [];
        _subPanel.Visible = _selectedChar >= 0;

        if (_selectedChar < 0)
        {
            _subPanel.Height = 0;
            return;
        }

        var mechanics = Characters[_selectedChar].Mechanics;
        _subButtons = new Button[mechanics.Length];

        // 逆順に追加して上から mechanics[0]→[1]→... の表示順にする
        for (int i = mechanics.Length - 1; i >= 0; i--)
        {
            int idx = i;
            var btn = AddSidebarButton(_subPanel, mechanics[i].MecLabel, () => ToggleMechanic(idx), leftPad: 16);
            _subButtons[i] = btn;
        }

        // 区切り線（Dock=Top で最後追加 → 最上部に表示）
        _subPanel.Controls.Add(new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = SystemColors.ControlDark,
        });

        _subPanel.Height = SidebarBtnH * mechanics.Length + 1;
        RefreshSubButtons();
    }

    void RefreshCostButtons()
    {
        for (int i = 0; i < _costButtons.Length; i++)
        {
            bool active = _selectedCosts.Contains(Costs[i]);
            _costButtons[i].BackColor = active ? SystemColors.Highlight : SystemColors.Control;
            _costButtons[i].ForeColor = active ? SystemColors.HighlightText : SystemColors.ControlText;
        }
    }

    void RefreshRarityButtons()
    {
        for (int i = 0; i < _rarityButtons.Length; i++)
        {
            bool active = _selectedRarities.Contains(Rarities[i]);
            _rarityButtons[i].BackColor = active ? SystemColors.Highlight : SystemColors.Control;
            _rarityButtons[i].ForeColor = active ? SystemColors.HighlightText : SystemColors.ControlText;
        }
    }

    void RefreshSubButtons()
    {
        for (int i = 0; i < _subButtons.Length; i++)
        {
            bool active = _selectedMechanics.Contains(i);
            _subButtons[i].BackColor = active ? SystemColors.Highlight : SystemColors.Control;
            _subButtons[i].ForeColor = active ? SystemColors.HighlightText : SystemColors.ControlText;
        }
    }

    void RefreshLangButtons()
    {
        _btnJp.BackColor = _isJp ? SystemColors.Highlight : SystemColors.Control;
        _btnJp.ForeColor = _isJp ? SystemColors.HighlightText : SystemColors.ControlText;
        _btnEn.BackColor = !_isJp ? SystemColors.Highlight : SystemColors.Control;
        _btnEn.ForeColor = !_isJp ? SystemColors.HighlightText : SystemColors.ControlText;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // フォームの実サイズ確定後にスプリッター位置を設定
        _outerSplit.Panel1MinSize = 140;
        _outerSplit.SplitterDistance = 200;
        _split.Panel1MinSize = 260;
        _split.Panel2MinSize = 200;
        _split.SplitterDistance = 320;

        // LVM_SETICONSPACING: ラベルなし表示用に画像サイズ+最小パディング
        var spacing = (IntPtr)(((91 + 4) << 16) | ((120 + 4) & 0xFFFF));
        SendMessage(_cardList.Handle, LVM_SETICONSPACING, IntPtr.Zero, spacing);

        RefreshCharButtons();
        RefreshCostButtons();
        RefreshRarityButtons();
        RefreshLangButtons();
    }
}
