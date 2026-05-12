using StS2Shared.Services;

namespace StS2CardBrowser;

record CardEntry(string Id, string NameEn, string NameJa, string Type, string Rarity, string Cost);

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
    ];

    // ---- データ ----
    List<CardEntry> _allCards = [];
    List<CardEntry> _filtered = [];
    bool _isJp = true;

    // ---- レア度定義 ----
    static readonly string[] Rarities = ["Common", "Uncommon", "Rare", "Starter", "Event", "Shop", "Ancient"];

    // ---- フィルタ状態 ----
    int _selectedChar = -1;
    readonly HashSet<int> _selectedMechanics = [];
    readonly HashSet<string> _selectedRarities = [];

    // ---- UI コントロール ----
    TextBox _filterBox = null!;
    Button _btnJp = null!, _btnEn = null!;
    Panel _charPanel = null!;
    Panel _subPanel = null!;
    Panel _rarityPanel = null!;
    ListBox _cardList = null!;
    RichTextBox _detailBox = null!;
    Label _countLabel = null!;
    SplitContainer _outerSplit = null!;
    SplitContainer _split = null!;
    Button[] _charButtons = [];
    Button[] _subButtons = [];
    Button[] _rarityButtons = [];

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

        // セパレータ（言語 ↔ レア度間）
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

        _cardList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Meiryo", 9.5f),
            ScrollAlwaysVisible = true,
            IntegralHeight = false,
        };
        _cardList.SelectedIndexChanged += OnCardSelected;
        _split.Panel1.Controls.Add(_cardList);

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
            PopulateList();
            ShowDetail(_filtered.Count == 0 ? null
                : _cardList.SelectedIndex >= 0 ? _filtered[_cardList.SelectedIndex] : null);
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
                CardDatabaseService.GetCardCost(id)))
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
            var mechanics = Characters[_selectedChar].Mechanics;
            if (_selectedMechanics.Count > 0)
                query = query.Where(c => _selectedMechanics.Any(i => mechanics[i].Filter(c.Id)));
            else
                query = query.Where(c => mechanics.Any(m => m.Filter(c.Id)));
        }

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
        _cardList.BeginUpdate();
        _cardList.Items.Clear();
        foreach (var c in _filtered)
            _cardList.Items.Add(_isJp ? c.NameJa : c.NameEn);
        _cardList.EndUpdate();

        _countLabel.Text = $"{_filtered.Count} 件";

        if (_filtered.Count > 0)
            _cardList.SelectedIndex = 0;
        else
            ShowDetail(null);
    }

    // ---- 詳細表示 ----

    void OnCardSelected(object? sender, EventArgs e)
    {
        if (_cardList.SelectedIndex < 0 || _cardList.SelectedIndex >= _filtered.Count) return;
        ShowDetail(_filtered[_cardList.SelectedIndex]);
    }

    void ShowDetail(CardEntry? card)
    {
        _detailBox.Clear();
        if (card is null) return;

        var (descEn, descJa) = CardDatabaseService.GetDescription(card.Id);
        var stats = CardDatabaseService.GetCardStats(card.Id);
        var descEnClean = DescriptionFormatter.Resolve(descEn, stats);
        var descJaClean = DescriptionFormatter.Resolve(descJa, stats);

        var synergies = CollectSynergies(card.Id);

        var rtb = _detailBox;
        rtb.SuspendLayout();

        AppendBold($"{card.NameJa}  /  {card.NameEn}\n", 13f);
        var rarityText = string.IsNullOrEmpty(card.Rarity) ? "" : $"     レア度: {card.Rarity}";
        AppendNormal($"タイプ: {card.Type}{rarityText}     コスト: {card.Cost}\n", 9.5f);
        AppendNormal($"ID: {card.Id}\n", 8.5f, Color.Gray);

        if (synergies.Count > 0)
        {
            AppendNormal("\n");
            AppendBold("シナジー: ", 9.5f);
            AppendNormal(string.Join("  /  ", synergies) + "\n", 9.5f, Color.DarkGreen);
        }

        AppendNormal("\n");
        if (!string.IsNullOrEmpty(descJaClean))
        {
            AppendBold("説明 (JP)\n", 9.5f);
            AppendNormal(descJaClean + "\n", 9.5f);
        }
        if (!string.IsNullOrEmpty(descEnClean))
        {
            AppendNormal("\n");
            AppendBold("説明 (EN)\n", 9.5f);
            AppendNormal(descEnClean + "\n", 9.5f);
        }

        rtb.ResumeLayout();
        rtb.SelectionStart = 0;
        rtb.ScrollToCaret();
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
        _split.Panel1MinSize = 150;
        _split.Panel2MinSize = 200;
        _split.SplitterDistance = 280;
        RefreshCharButtons();
        RefreshRarityButtons();
        RefreshLangButtons();
    }
}
