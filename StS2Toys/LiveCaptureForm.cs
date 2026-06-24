using System.Diagnostics;
using StS2Capture;
using StS2Capture.Capture;
using StS2Capture.Recognition;
using StS2Shared.Models;
using StS2Shared.Services;
using StS2Toys.Services;

namespace StS2Toys;

/// <summary>
/// ライブキャプチャ表示フォーム。プレイ中のゲーム画面を監視し、カード提示画面のカードや
/// ショップのレリック／ポーションを検出して一覧表示する。検出・キャプチャのロジックは
/// StS2Capture.Core を流用（仕様書フェーズ2）。現在状態パネルや情報ページリンクはフェーズ3/4 で統合。
/// </summary>
public sealed class LiveCaptureForm : Form
{
    readonly CaptureLoop _loop;
    readonly ShopItemRecognizer _shop = new();
    readonly ScreenRecognizer _screen;

    readonly Label _status = new();
    readonly RadioButton _rbWgc = new() { Text = "WGC", AutoSize = true };
    readonly RadioButton _rbGdi = new() { Text = "GDI", AutoSize = true };
    readonly Button _btnCapture = new() { Text = "手動キャプチャ", AutoSize = true };
    readonly Button _btnLinks = new() { Text = "リンク設定", AutoSize = true };
    readonly CheckBox _cbAuto = new() { Text = "自動監視", AutoSize = true };
    readonly ComboBox _cbCharacter = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 150,
        Margin = new Padding(8, 3, 0, 0),
    };

    // 枠色プロファイルの手動上書き候補。先頭は「自動（セーブから解決）」。
    static readonly string[] CharacterChoices =
        { "DEFECT", "SILENT", "IRONCLAD", "NECROBINDER", "REGENT" };

    readonly ListView _list = new();
    readonly ListView _ocrList = new();
    readonly PictureBox _capturePreview = new();
    readonly PictureBox _thumb = new();
    readonly SplitContainer _outer = new();
    readonly SplitContainer _left = new();
    readonly SplitContainer _right = new();

    // 現在状態パネル（current_run.save 由来）。
    readonly SplitContainer _mainSplit = new();
    readonly Label _stateText = new();
    readonly ListView _relicList = new();
    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 1000 };
    DateTime _lastSaveMtime;

    // 情報ページリンク（URL テンプレート）。検出結果の右クリックで開く。
    readonly ContextMenuStrip _linkMenu = new();
    List<UrlTemplate> _templates = UrlTemplateService.Load();

    /// <summary>リストアイテムに付与する、リンク生成用の種別＋ID。</summary>
    sealed record LinkTarget(string Kind, string Id);

    string _lastSignature = "";
    readonly string? _portraitsDir = ResolvePortraitsDir();

    public LiveCaptureForm()
    {
        Text = "ライブキャプチャ（カード／ショップ検出）";
        Width = 760;
        Height = 580;
        StartPosition = FormStartPosition.Manual;

        // 固定矩形レイアウト方式：画面（カードを選択／ショップ）ごとに固定座標を probe して
        // カード・レリック・ポーションを1パスで検出する（枠色は候補プールの絞り込みにのみ使用）。
        _screen = new ScreenRecognizer(_shop);

        // 既定：WGC（GPU 描画対応・主）。検出は ScreenRecognizer に一本化。
        _loop = new CaptureLoop(new WgcFrameSource());
        _loop.ScreenRecognizer = _screen;
        _loop.Updated += OnLoopUpdated;

        BuildLayout();
        WireEvents();

        _rbWgc.Checked = true;

        _saveTimer.Tick += (_, _) => RefreshSaveState();
        _saveTimer.Start();
        RefreshSaveState();

        UpdateStartupStatus();
    }

    void BuildLayout()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 8, 8, 4),
            WrapContents = true,
        };
        // 取得（WGC/GDI）の相互排他ラジオ群。
        top.Controls.Add(RadioGroup("取得:", _rbWgc, _rbGdi));
        top.Controls.Add(_cbAuto);
        top.Controls.Add(_btnCapture);
        top.Controls.Add(_btnLinks);
        top.Controls.Add(new Label { Text = "  枠キャラ:", AutoSize = true, Margin = new Padding(8, 6, 2, 0) });
        _cbCharacter.Items.Add("自動（セーブ）");
        foreach (var c in CharacterChoices) _cbCharacter.Items.Add(c);
        _cbCharacter.SelectedIndex = 0;
        top.Controls.Add(_cbCharacter);

        _status.Dock = DockStyle.Top;
        _status.AutoSize = false;
        _status.Height = 26;
        _status.Padding = new Padding(8, 4, 8, 4);
        _status.Text = "初期化中...";

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.Columns.Add("CardId", 180);
        _list.Columns.Add("EN", 130);
        _list.Columns.Add("JP", 120);
        _list.Columns.Add("確信度", 60);
        _list.Columns.Add("認識器", 70);

        _ocrList.Dock = DockStyle.Fill;
        _ocrList.View = View.Details;
        _ocrList.FullRowSelect = true;
        _ocrList.Columns.Add("検出テキスト／候補", 240);
        _ocrList.Columns.Add("種別", 45);
        _ocrList.Columns.Add("一致", 80);
        _ocrList.Columns.Add("距離", 90);

        _capturePreview.Dock = DockStyle.Fill;
        _capturePreview.SizeMode = PictureBoxSizeMode.Zoom;
        _capturePreview.BackColor = SystemColors.ControlDarkDark;

        _thumb.Dock = DockStyle.Fill;
        _thumb.SizeMode = PictureBoxSizeMode.Zoom;
        _thumb.BackColor = SystemColors.ControlLight;

        // 左：検出カード（上）＋ テキスト/ショップ候補（下）
        _left.Dock = DockStyle.Fill;
        _left.Orientation = Orientation.Horizontal;
        _left.Panel1.Controls.Add(_list);
        _left.Panel2.Controls.Add(WithHeader("ショップ候補（レリック／ポーション）", _ocrList));

        // 右：キャプチャプレビュー（上）＋ portrait サムネ（下）
        _right.Dock = DockStyle.Fill;
        _right.Orientation = Orientation.Horizontal;
        _right.Panel1.Controls.Add(WithHeader("キャプチャ画像（縮小プレビュー）", _capturePreview));
        _right.Panel2.Controls.Add(WithHeader("選択カードの portrait", _thumb));

        _outer.Dock = DockStyle.Fill;
        _outer.Orientation = Orientation.Vertical;
        _outer.Panel1.Controls.Add(_left);
        _outer.Panel2.Controls.Add(_right);

        // 現在状態パネル（左）：サマリ（上）＋所有レリック一覧（下）。
        _stateText.Dock = DockStyle.Top;
        _stateText.AutoSize = false;
        _stateText.Height = 150;
        _stateText.Padding = new Padding(6, 4, 4, 4);
        _stateText.Font = new Font(FontFamily.GenericSansSerif, 9f);
        _stateText.Text = "セーブ読込待ち...";

        _relicList.Dock = DockStyle.Fill;
        _relicList.View = View.Details;
        _relicList.FullRowSelect = true;
        _relicList.Columns.Add("所有レリック", 200);
        _relicList.Columns.Add("床", 45);

        var statePanel = new Panel { Dock = DockStyle.Fill };
        statePanel.Controls.Add(WithHeader("所有レリック", _relicList));
        statePanel.Controls.Add(_stateText);
        statePanel.Controls.Add(new Label
        {
            Text = "現在状態（current_run.save）",
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SystemColors.ControlDark,
            ForeColor = SystemColors.ControlLightLight,
            Padding = new Padding(4, 0, 0, 0),
        });

        // 左＝現在状態パネル / 右＝検出（カード・プレビュー）。
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.Orientation = Orientation.Vertical;
        _mainSplit.Panel1.Controls.Add(statePanel);
        _mainSplit.Panel2.Controls.Add(_outer);

        Controls.Add(_mainSplit);
        Controls.Add(_status);
        Controls.Add(top);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // フォームが実サイズを持ってから SplitterDistance を設定（早すぎると例外になるため）。
        SetSplitterDistance(_mainSplit, 240);
        SetSplitterDistance(_outer, (int)(_outer.Width * 0.55));
        SetSplitterDistance(_left, (int)(_left.Height * 0.45));
        SetSplitterDistance(_right, (int)(_right.Height * 0.5));
    }

    static void SetSplitterDistance(SplitContainer sc, int distance)
    {
        int extent = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
        int min = sc.Panel1MinSize;
        int max = extent - sc.Panel2MinSize - sc.SplitterWidth;
        if (max < min) return;
        try { sc.SplitterDistance = Math.Clamp(distance, min, max); } catch { }
    }

    /// <summary>
    /// ラベル＋ラジオ群を独立した親（FlowLayoutPanel）にまとめて返す。
    /// ラジオは直接の親ごとに相互排他グループになるため、論理グループごとに本メソッドで包む。
    /// </summary>
    static Control RadioGroup(string label, params RadioButton[] radios)
    {
        var group = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        group.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
        foreach (var rb in radios) group.Controls.Add(rb);
        return group;
    }

    /// <summary>コントロールの上に見出しラベルを付けた Panel を返す。</summary>
    static Control WithHeader(string title, Control inner)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        inner.Dock = DockStyle.Fill;
        var header = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SystemColors.ControlDark,
            ForeColor = SystemColors.ControlLightLight,
            Padding = new Padding(4, 0, 0, 0),
        };
        panel.Controls.Add(inner);
        panel.Controls.Add(header);
        return panel;
    }

    void WireEvents()
    {
        _rbWgc.CheckedChanged += (_, _) => { if (_rbWgc.Checked) _loop.SetFrameSource(new WgcFrameSource()); };
        _rbGdi.CheckedChanged += (_, _) => { if (_rbGdi.Checked) _loop.SetFrameSource(new GdiFrameSource()); };

        _cbAuto.CheckedChanged += (_, _) =>
        {
            if (_cbAuto.Checked) _loop.Start();
            else _loop.Stop();
        };

        _btnCapture.Click += (_, _) => Task.Run(_loop.CaptureOnce);
        _btnLinks.Click += (_, _) => EditLinkTemplates();

        // 検出結果リスト（カード／ショップ）右クリックで情報ページリンクを開く。
        _list.ContextMenuStrip = _linkMenu;
        _ocrList.ContextMenuStrip = _linkMenu;
        _linkMenu.Opening += OnLinkMenuOpening;

        _cbCharacter.SelectedIndexChanged += (_, _) =>
            _loop.CharacterOverride = _cbCharacter.SelectedIndex <= 0
                ? null
                : (string)_cbCharacter.SelectedItem!;

        _list.SelectedIndexChanged += (_, _) => UpdateThumbnail();

        FormClosed += (_, _) => { _saveTimer.Stop(); _saveTimer.Dispose(); _loop.Dispose(); };
    }

    // ---- 現在状態（current_run.save） ----

    void RefreshSaveState()
    {
        try
        {
            var path = SaveDataService.GetDefaultSavePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (_lastSaveMtime != default) { _stateText.Text = "セーブ未検出"; _relicList.Items.Clear(); }
                _lastSaveMtime = default;
                return;
            }
            var mtime = File.GetLastWriteTimeUtc(path);
            if (mtime == _lastSaveMtime) return;
            _lastSaveMtime = mtime;
            DisplaySaveState(SaveDataService.Load(path));
        }
        catch (Exception ex)
        {
            _stateText.Text = "セーブ読込エラー：" + ex.Message;
        }
    }

    void DisplaySaveState(RunSaveData data)
    {
        bool jp = AppLanguage.IsJapanese;
        var player = data.Players.FirstOrDefault();
        if (player is null) { _stateText.Text = "プレイヤー情報なし"; _relicList.Items.Clear(); return; }

        var charName = CardDatabaseService.GetName(player.CharacterId, jp);
        var nodeType = data.MapPointHistory.LastOrDefault()?.LastOrDefault()?.MapPointType;
        var lines = new List<string>
        {
            $"キャラ: {charName}",
            $"Act: {data.CurrentActIndex + 1}    アセンション: {data.Ascension}",
            $"HP: {player.CurrentHp}/{player.MaxHp}    ゴールド: {player.Gold}",
            $"エナジー: {player.MaxEnergy}    デッキ: {player.Deck.Count} 枚",
            $"現在ノード: {NodeTypeLabel(nodeType)}",
        };
        if (data.Acts.Count > data.CurrentActIndex && data.Acts[data.CurrentActIndex].Rooms is { } rooms)
        {
            if (!string.IsNullOrEmpty(rooms.BossId))
                lines.Add($"ボス: {EncounterDatabaseService.GetEncounterName(rooms.BossId!, jp)}");
        }
        _stateText.Text = string.Join(Environment.NewLine, lines);

        _relicList.BeginUpdate();
        _relicList.Items.Clear();
        foreach (var r in player.Relics)
        {
            var item = new ListViewItem(CardDatabaseService.GetRelicTitle(r.Id, jp));
            item.SubItems.Add(r.FloorAddedToDeck.ToString());
            _relicList.Items.Add(item);
        }
        _relicList.EndUpdate();
    }

    static string NodeTypeLabel(string? type) => type switch
    {
        "monster" => "通常戦闘",
        "elite" => "エリート",
        "boss" => "ボス",
        "shop" => "ショップ",
        "treasure" => "宝箱",
        "rest_site" => "休憩",
        null or "" => "-",
        _ => type,
    };

    // ---- 情報ページリンク ----

    void EditLinkTemplates()
    {
        using var dlg = new UrlTemplateSettingsForm();
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _templates = UrlTemplateService.Load();
    }

    void OnLinkMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _linkMenu.Items.Clear();
        var lv = _linkMenu.SourceControl as ListView;
        var target = lv is { SelectedItems.Count: > 0 } ? lv.SelectedItems[0].Tag as LinkTarget : null;
        if (target is null) { e.Cancel = true; return; }

        var links = SiteLinkService.BuildLinks(_templates, target.Kind, target.Id);
        if (links.Count == 0)
        {
            var none = _linkMenu.Items.Add("（リンク設定なし）");
            none.Enabled = false;
            return;
        }
        foreach (var link in links)
        {
            var url = link.Url;
            var item = new ToolStripMenuItem($"{link.Label}: {url}");
            item.Click += (_, _) => OpenUrl(url);
            _linkMenu.Items.Add(item);
        }
    }

    void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _status.Text = $"リンク起動エラー：{ex.Message}"; }
    }

    void UpdateStartupStatus()
    {
        var game = GameWindowLocator.Find();
        var parts = new List<string>
        {
            game is null ? "ゲーム未検出" : $"ゲーム検出: {game.Value.Title}",
        };
        if (!_screen.IsAvailable) parts.Add("（注意: portraits ディレクトリ未検出）");
        _status.Text = string.Join("  ", parts);
    }

    void OnLoopUpdated(CaptureLoop.Result result)
    {
        if (IsDisposed) return;
        try { BeginInvoke(() => ApplyResult(result)); }
        catch { /* フォーム破棄中 */ }
    }

    void ApplyResult(CaptureLoop.Result result)
    {
        _status.Text = result.Status;

        var oldPreview = _capturePreview.Image;
        _capturePreview.Image = result.Preview;
        oldPreview?.Dispose();

        bool isShop = result.Shop is { IsShop: true };
        var signature = string.Join("|",
            result.Cards.Select(c => $"{c.CardId}:{c.Confidence:F2}"))
            + "##" + string.Join("|", result.TextSpans.Select(s => s.Text))
            + "##SHOP:" + (isShop
                ? string.Join("|", result.Shop!.Items.Select(i =>
                    $"{i.Kind}:{string.Join(",", i.Candidates.Select(c => c.Id))}"))
                : "");
        if (signature == _lastSignature) return;
        _lastSignature = signature;

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var c in result.Cards)
        {
            var item = new ListViewItem(c.CardId);
            item.SubItems.Add(CardDatabaseService.GetName(c.CardId, japanese: false));
            item.SubItems.Add(CardDatabaseService.GetName(c.CardId, japanese: true));
            item.SubItems.Add(c.Confidence.ToString("F2"));
            item.SubItems.Add(c.Recognizer);
            item.Tag = new LinkTarget("card", c.CardId);
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        // ショップ画面ならショップ候補、そうでなければ OCR テキスト行を同じリストに描画（更新経路は1本）。
        _ocrList.BeginUpdate();
        _ocrList.Items.Clear();
        if (isShop)
            foreach (var lvi in BuildShopRows(result.Shop!)) _ocrList.Items.Add(lvi);
        else
            foreach (var lvi in BuildOcrRows(result.TextSpans)) _ocrList.Items.Add(lvi);
        _ocrList.EndUpdate();
    }

    static IEnumerable<ListViewItem> BuildOcrRows(IReadOnlyList<OcrTextSpan> spans)
    {
        foreach (var s in spans)
        {
            var item = new ListViewItem(s.Text);
            item.SubItems.Add(s.Source == "title" ? "帯" : "全");
            item.SubItems.Add(s.MatchedCardId ?? "(none)");
            item.SubItems.Add(s.Distance?.ToString() ?? "-");
            if (s.MatchedCardId is not null) item.Tag = new LinkTarget("card", s.MatchedCardId);
            if (s.MatchedCardId is not null)
                item.BackColor = s.Source == "title"
                    ? Color.FromArgb(255, 235, 200)
                    : Color.FromArgb(220, 245, 220);
            else if (s.Source == "title")
                item.BackColor = Color.FromArgb(245, 245, 245);
            yield return item;
        }
    }

    static IEnumerable<ListViewItem> BuildShopRows(ShopItemRecognizer.Result res)
    {
        foreach (var it in res.Items)
        {
            var label = it.Candidates.Count == 0
                ? "(no match)"
                : string.Join(" / ", it.Candidates.Select(c => $"{c.Name} [{c.Id}]"));
            var dist = it.Candidates.Count == 0
                ? "-"
                : string.Join(" / ", it.Candidates.Select(c => c.Distance.ToString("F2")));
            var lvi = new ListViewItem(label);
            lvi.SubItems.Add(it.Kind == ShopItemRecognizer.Kind.Relic ? "R" : "P");
            lvi.SubItems.Add(it.Accepted ? (it.Candidates.Count > 1 ? $"OK×{it.Candidates.Count}" : "OK") : "-");
            lvi.SubItems.Add(dist);
            if (it.Candidates.Count > 0)
                lvi.Tag = new LinkTarget(
                    it.Kind == ShopItemRecognizer.Kind.Relic ? "relic" : "potion",
                    it.Candidates[0].Id);
            if (it.Accepted)
                lvi.BackColor = it.Kind == ShopItemRecognizer.Kind.Relic
                    ? Color.FromArgb(220, 245, 220) : Color.FromArgb(255, 235, 200);
            yield return lvi;
        }
    }

    void UpdateThumbnail()
    {
        var old = _thumb.Image;
        Image? img = null;
        if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is LinkTarget { Kind: "card" } lt
            && _portraitsDir is not null)
        {
            var cardId = lt.Id;
            var path = CardImageService.GetSourcePath(_portraitsDir, cardId);
            if (path is not null && File.Exists(path))
            {
                try { img = Image.FromFile(path); } catch { img = null; }
            }
        }
        _thumb.Image = img;
        old?.Dispose();
    }

    static string? ResolvePortraitsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "extracted", "images",
                CardImageService.PortraitsDirName);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
