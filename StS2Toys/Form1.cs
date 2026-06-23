using StS2Shared.Models;
using StS2Toys.Services;
using StS2Shared.Services;

namespace StS2Toys
{
    public partial class Form1 : Form
    {
        private FileSystemWatcher? _watcher;
        private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 500 };
        private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = 2000 };
        private CardImageViewerForm? _imageViewer;
        private CardDetailForm? _detailViewer;
        private DeckOverviewForm? _combinedOverview;
        private DeckOverviewForm? _necroOverview;
        private DeckOverviewForm? _ironcladOverview;
        private DeckOverviewForm? _silentOverview;
        private DeckOverviewForm? _defectOverview;
        private DeckOverviewForm? _regentOverview;
        private DeckOverviewForm? _disappearanceOverview;
        private EncounterOverviewForm? _encounterOverview;
        private HpHistoryForm? _hpHistory;
        private LiveCaptureForm? _liveCapture;
        private SubWindowSettings? _liveCaptureSettings;
        private SubWindowSettings? _imageViewerSettings;
        private SubWindowSettings? _cardDetailSettings;
        private SubWindowSettings? _combinedOverviewSettings;
        private SubWindowSettings? _encounterOverviewSettings;
        private SubWindowSettings? _hpHistorySettings;
        private SubWindowSettings? _necroOverviewSettings;
        private SubWindowSettings? _ironcladOverviewSettings;
        private SubWindowSettings? _silentOverviewSettings;
        private SubWindowSettings? _defectOverviewSettings;
        private SubWindowSettings? _regentOverviewSettings;
        private SubWindowSettings? _disappearanceOverviewSettings;
        private IReadOnlyList<DeckCard>? _lastDeckCards;
        private IReadOnlyList<RelicEntry>? _lastRelics;
        private RunSaveData? _lastRunData;

        // デッキリストのソート状態
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        // ブロック関連カード絞り込み
        private bool _blockFilter = false;

        static string[] DeckColumnTexts => AppLanguage.IsJapanese
            ? ["カード名 (EN)", "カード名 (JP)", "コスト", "種別", "エンチャント", "枚数"]
            : ["Card Name (EN)", "Card Name (JP)", "Cost", "Type", "Enchantment", "Count"];

        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
            _reloadTimer.Tick += (_, _) => { _reloadTimer.Stop(); ReloadCurrentFile(); };
            _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); lblUpdateFlash.Text = ""; };
        }

        void Form1_Load(object? sender, EventArgs e)
        {
            RestoreWindowSettings();
            var defaultPath = SaveDataService.GetDefaultSavePath();
            if (File.Exists(defaultPath))
            {
                OpenFile(defaultPath);
                RestoreSubWindowVisibility();
            }
        }

        void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveWindowSettings();
            StopWatching();
            _reloadTimer.Dispose();
            _flashTimer.Dispose();
            _imageViewer?.Close();
            _detailViewer?.Close();
            _combinedOverview?.Close();
            _encounterOverview?.Close();
            _hpHistory?.Close();
            _necroOverview?.Close();
            _ironcladOverview?.Close();
            _silentOverview?.Close();
            _defectOverview?.Close();
            _regentOverview?.Close();
            _disappearanceOverview?.Close();
        }

        void RestoreWindowSettings()
        {
            var app = WindowSettingsService.Load();
            AppLanguage.IsJapanese = app.Language != "en";
            UpdateLangButton();
            _imageViewerSettings = app.ImageViewer;
            _cardDetailSettings = app.CardDetail;
            _combinedOverviewSettings = app.CombinedOverview;
            _encounterOverviewSettings = app.EncounterOverview;
            _hpHistorySettings = app.HpHistory;
            _necroOverviewSettings    = app.NecroOverview;
            _ironcladOverviewSettings = app.IroncladOverview;
            _silentOverviewSettings   = app.SilentOverview;
            _defectOverviewSettings   = app.DefectOverview;
            _regentOverviewSettings       = app.RegentOverview;
            _disappearanceOverviewSettings = app.DisappearanceOverview;
            _liveCaptureSettings = app.LiveCapture;

            if (app.SidePanelWidth is int w)
                splitContainerOuter.SplitterDistance = w;

            var main = app.Main;
            if (main is null) return;

            var savedBounds = new Rectangle(main.X, main.Y, main.Width, main.Height);
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(savedBounds))) return;

            StartPosition = FormStartPosition.Manual;
            Bounds = savedBounds;

            if (main.State == nameof(FormWindowState.Maximized))
                WindowState = FormWindowState.Maximized;
        }

        void SaveWindowSettings()
        {
            if (_imageViewer is { IsDisposed: false })
                _imageViewerSettings = WindowToSub(_imageViewer);
            if (_detailViewer is { IsDisposed: false })
                _cardDetailSettings = WindowToSub(_detailViewer);
            if (_combinedOverview is { IsDisposed: false })
                _combinedOverviewSettings = WindowToSub(_combinedOverview);
            if (_necroOverview is { IsDisposed: false })
                _necroOverviewSettings = WindowToSub(_necroOverview);
            if (_ironcladOverview is { IsDisposed: false })
                _ironcladOverviewSettings = WindowToSub(_ironcladOverview);
            if (_silentOverview is { IsDisposed: false })
                _silentOverviewSettings = WindowToSub(_silentOverview);
            if (_defectOverview is { IsDisposed: false })
                _defectOverviewSettings = WindowToSub(_defectOverview);
            if (_regentOverview is { IsDisposed: false })
                _regentOverviewSettings = WindowToSub(_regentOverview);
            if (_disappearanceOverview is { IsDisposed: false })
                _disappearanceOverviewSettings = WindowToSub(_disappearanceOverview);
            if (_encounterOverview is { IsDisposed: false })
                _encounterOverviewSettings = WindowToSub(_encounterOverview);
            if (_hpHistory is { IsDisposed: false })
                _hpHistorySettings = WindowToSub(_hpHistory);
            if (_liveCapture is { IsDisposed: false })
                _liveCaptureSettings = WindowToSub(_liveCapture);

            var state = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var main = new WindowSettings(bounds.X, bounds.Y, bounds.Width, bounds.Height, state.ToString());
            WindowSettingsService.Save(new AppSettings(main, _imageViewerSettings, _cardDetailSettings, _hpHistorySettings, _encounterOverviewSettings, splitContainerOuter.SplitterDistance, _necroOverviewSettings, _ironcladOverviewSettings, _silentOverviewSettings, _defectOverviewSettings, _regentOverviewSettings, _combinedOverviewSettings, _disappearanceOverviewSettings, AppLanguage.IsJapanese ? "ja" : "en", _liveCaptureSettings));
        }

        static SubWindowSettings WindowToSub(Form form) =>
            new(form.Bounds.X, form.Bounds.Y, form.Bounds.Width, form.Bounds.Height, form.Visible);

        void RestoreSubWindowVisibility()
        {
            if (_combinedOverviewSettings?.Visible == true)  BtnCombinedOverview_Click(null, EventArgs.Empty);
            if (_encounterOverviewSettings?.Visible == true)  BtnEncounterOverview_Click(null, EventArgs.Empty);
            if (_hpHistorySettings?.Visible == true)          BtnHpHistory_Click(null, EventArgs.Empty);
            if (_necroOverviewSettings?.Visible == true)      BtnNecroOverview_Click(null, EventArgs.Empty);
            if (_ironcladOverviewSettings?.Visible == true)   BtnIroncladOverview_Click(null, EventArgs.Empty);
            if (_silentOverviewSettings?.Visible == true)     BtnSilentOverview_Click(null, EventArgs.Empty);
            if (_defectOverviewSettings?.Visible == true)     BtnDefectOverview_Click(null, EventArgs.Empty);
            if (_regentOverviewSettings?.Visible == true)        BtnRegentOverview_Click(null, EventArgs.Empty);
            if (_disappearanceOverviewSettings?.Visible == true) BtnDisappearanceOverview_Click(null, EventArgs.Empty);
            if (_liveCaptureSettings?.Visible == true)           BtnLiveCapture_Click(null, EventArgs.Empty);
        }

        static SubWindowSettings BoundsToSub(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

        void ApplySubWindowSettings(Form form, SubWindowSettings? s, Point defaultLocation)
        {
            form.StartPosition = FormStartPosition.Manual;
            if (s is not null)
            {
                var bounds = new Rectangle(s.X, s.Y, s.Width, s.Height);
                if (Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(bounds)))
                {
                    form.Location = new Point(s.X, s.Y);
                    form.Size = new Size(s.Width, s.Height);
                    return;
                }
            }
            form.Location = defaultLocation;
        }

        void BtnOpen_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "セーブファイルを選択",
                Filter = "セーブファイル (*.save)|*.save|すべてのファイル (*.*)|*.*",
                InitialDirectory = Path.GetDirectoryName(SaveDataService.GetDefaultSavePath()),
            };

            if (dialog.ShowDialog() == DialogResult.OK)
                OpenFile(dialog.FileName);
        }

        void BtnToggleAuto_Click(object? sender, EventArgs e)
        {
            if (_watcher != null)
                StopWatching();
            else if (!string.IsNullOrEmpty(txtFilePath.Text))
                StartWatching(txtFilePath.Text);
        }

        void BtnLang_Click(object? sender, EventArgs e)
        {
            AppLanguage.IsJapanese = !AppLanguage.IsJapanese;
            UpdateLangButton();
            if (_lastRunData is not null) DisplayData(_lastRunData);
        }

        void UpdateLangButton() => btnLang.Text = AppLanguage.IsJapanese ? "JP" : "EN";

        void OpenFile(string path)
        {
            try
            {
                var data = SaveDataService.Load(path);
                txtFilePath.Text = path;
                DisplayData(data);
                lblLastUpdated.Text = $"最終更新: {DateTime.Now:HH:mm:ss}";
                StartWatching(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"読み込みエラー:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void StartWatching(string path)
        {
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path)!)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileChanged;
            UpdateAutoButton(watching: true);
        }

        void StopWatching()
        {
            _watcher?.Dispose();
            _watcher = null;
            UpdateAutoButton(watching: false);
        }

        void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // FileSystemWatcher は非UIスレッドから発火するため Invoke が必要。
            // 連続イベントをデバウンスするためタイマーをリセットする。
            Invoke(() => { _reloadTimer.Stop(); _reloadTimer.Start(); });
        }

        void ReloadCurrentFile()
        {
            if (string.IsNullOrEmpty(txtFilePath.Text)) return;
            try
            {
                var data = SaveDataService.Load(txtFilePath.Text);
                DisplayData(data);
                lblLastUpdated.Text = $"最終更新: {DateTime.Now:HH:mm:ss}";
                lblUpdateFlash.Text = "✓ 更新しました";
                _flashTimer.Stop();
                _flashTimer.Start();
            }
            catch
            {
                // ゲームがファイルを書き込み中の場合は無視（次の変更イベントで再試行される）
            }
        }

        void UpdateAutoButton(bool watching)
        {
            btnToggleAuto.Text = watching
                ? (AppLanguage.IsJapanese ? "● 監視中" : "● Watching")
                : (AppLanguage.IsJapanese ? "○ 自動更新" : "○ Auto");
            btnToggleAuto.ForeColor = watching ? Color.DarkGreen : SystemColors.ControlText;
        }

        void DisplayData(RunSaveData data)
        {
            _lastRunData = data;
            if (data.Players.Count == 0) return;
            var player = data.Players[0];

            var characterEn = CardDatabaseService.GetName(player.CharacterId, japanese: false);
            var characterJa = CardDatabaseService.GetName(player.CharacterId, japanese: true);
            lblInfo.Text = AppLanguage.IsJapanese
                ? $"キャラクター: {characterJa} ({characterEn})　アセンション: {data.Ascension}　Act: {data.CurrentActIndex + 1}　　HP: {player.CurrentHp}/{player.MaxHp}　ゴールド: {player.Gold}　エネルギー: {player.MaxEnergy}"
                : $"Character: {characterEn} ({characterJa})  Ascension: {data.Ascension}  Act: {data.CurrentActIndex + 1}    HP: {player.CurrentHp}/{player.MaxHp}  Gold: {player.Gold}  Energy: {player.MaxEnergy}";

            DisplayDeck(player);
            DisplayRelics(player);
            RefreshHpHistory();
            RefreshEncounterOverview();
        }

        void DisplayDeck(PlayerData player)
        {
            _lastDeckCards = player.Deck
                .GroupBy(c => (
                    c.Id,
                    IsUpgraded: (c.CurrentUpgradeLevel ?? 0) >= 1,
                    TinkerType: c.GetPropInt("TinkerTimeType"),
                    EnchantmentId: c.Enchantment?.Id ?? "",
                    EnchantmentAmount: c.Enchantment?.Amount ?? 0))
                .OrderBy(g => CardDatabaseService.GetName(g.Key.Id, japanese: AppLanguage.IsJapanese))
                .ThenBy(g => g.Key.IsUpgraded)
                .ThenBy(g => g.Key.EnchantmentId)
                .Select(g =>
                {
                    bool upgraded = g.Key.IsUpgraded;
                    string suffix = upgraded ? "+" : "";
                    string runtimeType = g.Key.TinkerType switch
                    {
                        1 => "Attack",
                        2 => "Skill",
                        3 => "Power",
                        _ => CardDatabaseService.GetCardType(g.Key.Id)
                    };
                    return new DeckCard(
                        g.Key.Id,
                        CardDatabaseService.GetName(g.Key.Id, japanese: false) + suffix,
                        CardDatabaseService.GetName(g.Key.Id, japanese: true)  + suffix,
                        CardDatabaseService.GetCardCost(g.Key.Id),
                        runtimeType,
                        g.Count(),
                        upgraded,
                        g.Key.EnchantmentId,
                        g.Key.EnchantmentAmount);
                })
                .ToList();

            RefreshDeckList();
        }

        void RefreshDeckList()
        {
            if (_lastDeckCards is null) return;

            var blockCards = _lastDeckCards.Where(c => CardDatabaseService.IsBlockGiver(c.Id)).ToList();
            int total = _lastDeckCards.Sum(c => c.Count);
            int blockCount = blockCards.Sum(c => c.Count);

            var cards = _blockFilter ? (IReadOnlyList<DeckCard>)blockCards : _lastDeckCards;

            bool ja = AppLanguage.IsJapanese;
            lblDeckTitle.Text = _blockFilter
                ? (ja ? $"デッキ（ブロック関連 {blockCount}/{total}枚）" : $"Deck (Block-related {blockCount}/{total})")
                : (ja ? $"デッキ ({total}枚)" : $"Deck ({total})");

            var colTexts = DeckColumnTexts;
            for (int ci = 0; ci < listViewDeck.Columns.Count; ci++)
                listViewDeck.Columns[ci].Text = colTexts[ci] + (ci == _sortColumn ? (_sortAscending ? " ▲" : " ▼") : "");

            listViewDeck.BeginUpdate();
            listViewDeck.Items.Clear();
            foreach (var c in cards)
            {
                var item = new ListViewItem(c.NameEn);
                item.SubItems.Add(c.NameJa);
                item.SubItems.Add(c.Cost);
                item.SubItems.Add(LocalizeType(c.Type));
                item.SubItems.Add(CardDatabaseService.FormatEnchantmentLabel(c.EnchantmentId, c.EnchantmentAmount, japanese: true));
                item.SubItems.Add(c.Count.ToString());
                item.Tag = c;
                listViewDeck.Items.Add(item);
            }
            listViewDeck.EndUpdate();

            if (_sortColumn >= 0)
                listViewDeck.ListViewItemSorter = new DeckItemComparer(_sortColumn, _sortAscending);

            RefreshCombinedOverview();
            RefreshNecroOverview();
            RefreshIroncladOverview();
            RefreshSilentOverview();
            RefreshDefectOverview();
            RefreshRegentOverview();
            RefreshDisappearanceOverview();
        }

        void DisplayRelics(PlayerData player)
        {
            _lastRelics = player.Relics
                .Select(r => new RelicEntry(
                    r.Id,
                    CardDatabaseService.GetName(r.Id, japanese: false),
                    CardDatabaseService.GetName(r.Id, japanese: true)))
                .ToList();

            lblRelicsTitle.Text = AppLanguage.IsJapanese
                ? $"レリック ({player.Relics.Count}個)"
                : $"Relics ({player.Relics.Count})";

            listViewRelics.BeginUpdate();
            listViewRelics.Items.Clear();
            foreach (var relic in _lastRelics)
            {
                var item = new ListViewItem(relic.NameEn);
                item.SubItems.Add(relic.NameJa);
                item.Tag = relic.Id;
                listViewRelics.Items.Add(item);
            }
            listViewRelics.EndUpdate();

            RefreshCombinedOverview();
            RefreshNecroOverview();
            RefreshIroncladOverview();
            RefreshSilentOverview();
            RefreshDefectOverview();
            RefreshRegentOverview();
            RefreshDisappearanceOverview();
        }

        void RefreshCombinedOverview()
        {
            if (_combinedOverview is null || _combinedOverview.IsDisposed || !_combinedOverview.Visible) return;
            if (_lastDeckCards is null) return;

            bool ja = AppLanguage.IsJapanese;
            var cards   = _lastDeckCards;
            var relics  = _lastRelics ?? [];
            int deckTotal = cards.Sum(c => c.Count);
            var charId  = _lastRunData?.Players.FirstOrDefault()?.CharacterId ?? "";
            bool isNecro = charId.Contains("NECRO", StringComparison.OrdinalIgnoreCase);

            var drawRelics  = relics.Where(r => CardDatabaseService.IsRelicDrawRelated(r.Id)).ToList();
            var blockRelics = relics.Where(r => CardDatabaseService.IsRelicBlockGiver(r.Id)).ToList();
            var otherRelics = relics.Where(r => !CardDatabaseService.IsRelicDrawRelated(r.Id) && !CardDatabaseService.IsRelicBlockGiver(r.Id)).ToList();

            var sections = new List<OverviewSection>();

            // 1. タイプ別
            foreach (var g in cards.GroupBy(c => c.Type).OrderBy(g => CombinedTypeOrder(g.Key)))
                sections.Add(new OverviewSection(CombinedTypeLabelEn(g.Key), CombinedTypeLabelJa(g.Key),
                    g.OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(), []));

            // 2. ドロー関連
            sections.Add(new OverviewSection("Draw-related", "ドロー関連",
                cards.Where(c => CardDatabaseService.IsDrawRelated(c.Id)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(),
                drawRelics));

            // 3. ブロック関連
            sections.Add(new OverviewSection("Block-related", "ブロック関連",
                cards.Where(c => CardDatabaseService.IsBlockGiver(c.Id)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(),
                blockRelics));

            // 4. 全体攻撃
            sections.Add(new OverviewSection("Attacks All Enemies", "全体攻撃",
                cards.Where(c => CardDatabaseService.IsAllEnemiesAttack(c.Id)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(),
                []));

            // 5. 召喚 (Necrobinder only)
            if (isNecro)
                sections.Add(new OverviewSection("Summon", "召喚",
                    cards.Where(c => CardDatabaseService.IsNecroSummon(c.Id)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(),
                    []));

            // 6. 共通キーワード（レリックも振り分ける）
            var claimedRelics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in CharacterMechanics.MechanicsFor("Common"))
            {
                var kwRelics = relics.Where(r => m.Filter(r.Id)).ToList();
                foreach (var r in kwRelics) claimedRelics.Add(r.Id);
                sections.Add(new OverviewSection(m.EnLabel, m.JaLabel,
                    cards.Where(c => m.Filter(c.Id)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(),
                    kwRelics));
            }

            // 7. その他のレリック（共通キーワードに振り分け済みのものは除外）
            var finalOtherRelics = otherRelics.Where(r => !claimedRelics.Contains(r.Id)).ToList();
            sections.Add(new OverviewSection("Other Relics", "その他のレリック", [], finalOtherRelics));

            _combinedOverview.SetSections(sections, deckTotal);
        }

        static int CombinedTypeOrder(string type) => type switch
        {
            "Attack" => 0, "Skill" => 1, "Power" => 2, "Curse" => 3, "Status" => 4, "Quest" => 5, _ => 6
        };

        static string CombinedTypeLabelEn(string type) => type.Length > 0 ? type : "Other";
        static string CombinedTypeLabelJa(string type) => type switch
        {
            "Attack" => "アタック", "Skill" => "スキル", "Power" => "パワー",
            "Curse" => "呪い", "Status" => "状態異常", "Quest" => "クエスト",
            _ => type.Length > 0 ? type : "その他"
        };

        void BtnDisappearanceOverview_Click(object? sender, EventArgs e)
        {
            if (_disappearanceOverview is null || _disappearanceOverview.IsDisposed || !_disappearanceOverview.Visible)
            {
                if (_disappearanceOverview is null || _disappearanceOverview.IsDisposed)
                {
                    _disappearanceOverview = new DeckOverviewForm();
                    ApplySubWindowSettings(_disappearanceOverview, _disappearanceOverviewSettings, new Point(Right + 4, Top));
                    _disappearanceOverview.FormClosed += (_, _) =>
                    {
                        _disappearanceOverviewSettings = BoundsToSub(_disappearanceOverview.Bounds);
                        UpdateDisappearanceOverviewButton(false);
                    };
                }
                _disappearanceOverview.Show(this);
                UpdateDisappearanceOverviewButton(true);
                RefreshDisappearanceOverview();
            }
            else
            {
                _disappearanceOverview.Hide();
                UpdateDisappearanceOverviewButton(false);
            }
        }

        void UpdateDisappearanceOverviewButton(bool visible)
        {
            btnDisappearanceOverview.Text = visible ? "● デッキ枚数理論値" : "○ デッキ枚数理論値";
            btnDisappearanceOverview.ForeColor = visible ? Color.DarkSlateBlue : SystemColors.ControlText;
        }

        void RefreshDisappearanceOverview()
        {
            if (_disappearanceOverview is null || _disappearanceOverview.IsDisposed || !_disappearanceOverview.Visible) return;
            if (_lastDeckCards is null) return;

            bool ja = AppLanguage.IsJapanese;
            var cards  = _lastDeckCards;
            var relics = _lastRelics ?? [];
            int deckTotal = cards.Sum(c => c.Count);

            bool IsDisposable(DeckCard c) =>
                c.Type == "Power"
                || CardDatabaseService.IsExhaustKeyword(c.Id)
                || CardDatabaseService.IsEtherealKeyword(c.Id)
                || CardDatabaseService.IsExhaustGainingEnchantment(c.EnchantmentId);

            var sections = new List<OverviewSection>
            {
                new("Disappears in Battle", "戦闘中に消滅する",
                    cards.Where(c =>  IsDisposable(c)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(), []),
                new("Persists in Battle", "戦闘中に消滅しない",
                    cards.Where(c => !IsDisposable(c)).OrderBy(c => ja ? c.NameJa : c.NameEn).ToList(),
                    relics),
            };

            _disappearanceOverview.SetSections(sections, deckTotal);
        }

        void RefreshEncounterOverview()
        {
            if (_encounterOverview is null || _encounterOverview.IsDisposed || !_encounterOverview.Visible) return;
            if (_lastRunData is null) return;
            _encounterOverview.UpdateData(_lastRunData);
        }

        void BtnEncounterOverview_Click(object? sender, EventArgs e)
        {
            if (_encounterOverview is null || _encounterOverview.IsDisposed || !_encounterOverview.Visible)
            {
                if (_encounterOverview is null || _encounterOverview.IsDisposed)
                {
                    _encounterOverview = new EncounterOverviewForm();
                    ApplySubWindowSettings(_encounterOverview, _encounterOverviewSettings, new Point(Right + 4, Top));
                    _encounterOverview.FormClosed += (_, _) =>
                    {
                        _encounterOverviewSettings = BoundsToSub(_encounterOverview.Bounds);
                        UpdateEncounterOverviewButton(false);
                    };
                }
                _encounterOverview.Show(this);
                UpdateEncounterOverviewButton(true);
                RefreshEncounterOverview();
            }
            else
            {
                _encounterOverview.Hide();
                UpdateEncounterOverviewButton(false);
            }
        }

        void UpdateEncounterOverviewButton(bool visible)
        {
            btnEncounterOverview.Text      = visible ? "● 敵情報" : "○ 敵情報";
            btnEncounterOverview.ForeColor = visible ? Color.DarkSlateBlue : SystemColors.ControlText;
        }

        void BtnCombinedOverview_Click(object? sender, EventArgs e)
        {
            if (_combinedOverview is null || _combinedOverview.IsDisposed || !_combinedOverview.Visible)
            {
                if (_combinedOverview is null || _combinedOverview.IsDisposed)
                {
                    _combinedOverview = new DeckOverviewForm();
                    _combinedOverview.SetTitle("Deck Overview", "デッキ概観");
                    ApplySubWindowSettings(_combinedOverview, _combinedOverviewSettings, new Point(Right + 4, Top));
                    _combinedOverview.FormClosed += (_, _) =>
                    {
                        _combinedOverviewSettings = BoundsToSub(_combinedOverview.Bounds);
                        UpdateCombinedOverviewButton(false);
                    };
                }
                _combinedOverview.Show(this);
                UpdateCombinedOverviewButton(true);
                RefreshCombinedOverview();
            }
            else
            {
                _combinedOverview.Hide();
                UpdateCombinedOverviewButton(false);
            }
        }

        void UpdateCombinedOverviewButton(bool visible)
        {
            btnCombinedOverview.Text = visible ? "● デッキ概観" : "○ デッキ概観";
            btnCombinedOverview.ForeColor = visible ? Color.DarkRed : SystemColors.ControlText;
        }

        void BtnNecroOverview_Click(object? sender, EventArgs e)
        {
            if (_necroOverview is null || _necroOverview.IsDisposed || !_necroOverview.Visible)
            {
                if (_necroOverview is null || _necroOverview.IsDisposed)
                {
                    _necroOverview = new DeckOverviewForm();
                    _necroOverview.SetKeywordGroups(
                        CharacterMechanics.MechanicsFor("Necrobinder")
                            .Select(m => (m.EnLabel, m.JaLabel, m.Filter)).ToArray(),
                        "Necrobinder Overview", "Necrobinder概観");
                    ApplySubWindowSettings(_necroOverview, _necroOverviewSettings, new Point(Right + 4, Top));
                    _necroOverview.FormClosed += (_, _) =>
                    {
                        _necroOverviewSettings = BoundsToSub(_necroOverview.Bounds);
                        UpdateNecroOverviewButton(false);
                    };
                }
                _necroOverview.Show(this);
                UpdateNecroOverviewButton(true);
                RefreshNecroOverview();
            }
            else
            {
                _necroOverview.Hide();
                UpdateNecroOverviewButton(false);
            }
        }

        void UpdateNecroOverviewButton(bool visible)
        {
            btnNecroOverview.Text = visible ? "● Necrobinder概観" : "○ Necrobinder概観";
            btnNecroOverview.ForeColor = visible ? Color.DarkMagenta : SystemColors.ControlText;
        }

        void RefreshNecroOverview()
        {
            if (_necroOverview is null || _necroOverview.IsDisposed || !_necroOverview.Visible) return;
            if (_lastDeckCards is null) return;
            _necroOverview.UpdateDeck(_lastDeckCards);
            _necroOverview.UpdateRelics(_lastRelics ?? []);
            _necroOverview.SetStatsText(BuildStatsText("Necrobinder", _lastDeckCards));
        }

        void BtnIroncladOverview_Click(object? sender, EventArgs e)
        {
            if (_ironcladOverview is null || _ironcladOverview.IsDisposed || !_ironcladOverview.Visible)
            {
                if (_ironcladOverview is null || _ironcladOverview.IsDisposed)
                {
                    _ironcladOverview = new DeckOverviewForm();
                    _ironcladOverview.SetKeywordGroups(
                        CharacterMechanics.MechanicsFor("Ironclad")
                            .Select(m => (m.EnLabel, m.JaLabel, m.Filter)).ToArray(),
                        "Ironclad Overview", "Ironclad概観");
                    ApplySubWindowSettings(_ironcladOverview, _ironcladOverviewSettings, new Point(Right + 4, Top));
                    _ironcladOverview.FormClosed += (_, _) =>
                    {
                        _ironcladOverviewSettings = BoundsToSub(_ironcladOverview.Bounds);
                        UpdateIroncladOverviewButton(false);
                    };
                }
                _ironcladOverview.Show(this);
                UpdateIroncladOverviewButton(true);
                RefreshIroncladOverview();
            }
            else
            {
                _ironcladOverview.Hide();
                UpdateIroncladOverviewButton(false);
            }
        }

        void UpdateIroncladOverviewButton(bool visible)
        {
            btnIroncladOverview.Text = visible ? "● Ironclad概観" : "○ Ironclad概観";
            btnIroncladOverview.ForeColor = visible ? Color.Firebrick : SystemColors.ControlText;
        }

        void RefreshIroncladOverview()
        {
            if (_ironcladOverview is null || _ironcladOverview.IsDisposed || !_ironcladOverview.Visible) return;
            if (_lastDeckCards is null) return;
            _ironcladOverview.UpdateDeck(_lastDeckCards);
            _ironcladOverview.UpdateRelics(_lastRelics ?? []);
            _ironcladOverview.SetStatsText(BuildStatsText("Ironclad", _lastDeckCards));
        }

        void BtnSilentOverview_Click(object? sender, EventArgs e)
        {
            if (_silentOverview is null || _silentOverview.IsDisposed || !_silentOverview.Visible)
            {
                if (_silentOverview is null || _silentOverview.IsDisposed)
                {
                    _silentOverview = new DeckOverviewForm();
                    _silentOverview.SetKeywordGroups(
                        CharacterMechanics.MechanicsFor("Silent")
                            .Select(m => (m.EnLabel, m.JaLabel, m.Filter)).ToArray(),
                        "Silent Overview", "Silent概観");
                    ApplySubWindowSettings(_silentOverview, _silentOverviewSettings, new Point(Right + 4, Top));
                    _silentOverview.FormClosed += (_, _) =>
                    {
                        _silentOverviewSettings = BoundsToSub(_silentOverview.Bounds);
                        UpdateSilentOverviewButton(false);
                    };
                }
                _silentOverview.Show(this);
                UpdateSilentOverviewButton(true);
                RefreshSilentOverview();
            }
            else
            {
                _silentOverview.Hide();
                UpdateSilentOverviewButton(false);
            }
        }

        void UpdateSilentOverviewButton(bool visible)
        {
            btnSilentOverview.Text = visible ? "● Silent概観" : "○ Silent概観";
            btnSilentOverview.ForeColor = visible ? Color.DarkSlateGray : SystemColors.ControlText;
        }

        void RefreshSilentOverview()
        {
            if (_silentOverview is null || _silentOverview.IsDisposed || !_silentOverview.Visible) return;
            if (_lastDeckCards is null) return;
            _silentOverview.UpdateDeck(_lastDeckCards);
            _silentOverview.UpdateRelics(_lastRelics ?? []);
            _silentOverview.SetStatsText(BuildStatsText("Silent", _lastDeckCards));
        }

        void BtnDefectOverview_Click(object? sender, EventArgs e)
        {
            if (_defectOverview is null || _defectOverview.IsDisposed || !_defectOverview.Visible)
            {
                if (_defectOverview is null || _defectOverview.IsDisposed)
                {
                    _defectOverview = new DeckOverviewForm();
                    _defectOverview.SetKeywordGroups(
                        CharacterMechanics.MechanicsFor("Defect")
                            .Select(m => (m.EnLabel, m.JaLabel, m.Filter)).ToArray(),
                        "Defect Overview", "Defect概観");
                    ApplySubWindowSettings(_defectOverview, _defectOverviewSettings, new Point(Right + 4, Top));
                    _defectOverview.FormClosed += (_, _) =>
                    {
                        _defectOverviewSettings = BoundsToSub(_defectOverview.Bounds);
                        UpdateDefectOverviewButton(false);
                    };
                }
                _defectOverview.Show(this);
                UpdateDefectOverviewButton(true);
                RefreshDefectOverview();
            }
            else
            {
                _defectOverview.Hide();
                UpdateDefectOverviewButton(false);
            }
        }

        void UpdateDefectOverviewButton(bool visible)
        {
            btnDefectOverview.Text = visible ? "● Defect概観" : "○ Defect概観";
            btnDefectOverview.ForeColor = visible ? Color.RoyalBlue : SystemColors.ControlText;
        }

        void RefreshDefectOverview()
        {
            if (_defectOverview is null || _defectOverview.IsDisposed || !_defectOverview.Visible) return;
            if (_lastDeckCards is null) return;
            _defectOverview.UpdateDeck(_lastDeckCards);
            _defectOverview.UpdateRelics(_lastRelics ?? []);
            _defectOverview.SetStatsText(BuildStatsText("Defect", _lastDeckCards));
        }

        void BtnRegentOverview_Click(object? sender, EventArgs e)
        {
            if (_regentOverview is null || _regentOverview.IsDisposed || !_regentOverview.Visible)
            {
                if (_regentOverview is null || _regentOverview.IsDisposed)
                {
                    _regentOverview = new DeckOverviewForm();
                    _regentOverview.SetKeywordGroups(
                        CharacterMechanics.MechanicsFor("Regent")
                            .Select(m => (m.EnLabel, m.JaLabel, m.Filter)).ToArray(),
                        "Regent Overview", "Regent概観");
                    ApplySubWindowSettings(_regentOverview, _regentOverviewSettings, new Point(Right + 4, Top));
                    _regentOverview.FormClosed += (_, _) =>
                    {
                        _regentOverviewSettings = BoundsToSub(_regentOverview.Bounds);
                        UpdateRegentOverviewButton(false);
                    };
                }
                _regentOverview.Show(this);
                UpdateRegentOverviewButton(true);
                RefreshRegentOverview();
            }
            else
            {
                _regentOverview.Hide();
                UpdateRegentOverviewButton(false);
            }
        }

        void UpdateRegentOverviewButton(bool visible)
        {
            btnRegentOverview.Text = visible ? "● Regent概観" : "○ Regent概観";
            btnRegentOverview.ForeColor = visible ? Color.SaddleBrown : SystemColors.ControlText;
        }

        void RefreshRegentOverview()
        {
            if (_regentOverview is null || _regentOverview.IsDisposed || !_regentOverview.Visible) return;
            if (_lastDeckCards is null) return;
            _regentOverview.UpdateDeck(_lastDeckCards);
            _regentOverview.UpdateRelics(_lastRelics ?? []);
            _regentOverview.SetStatsText(BuildStatsText("Regent", _lastDeckCards));
        }

        static string BuildStatsText(string charLabel, IReadOnlyList<DeckCard> deck) =>
            string.Join("  ", CharacterMechanics.MechanicsFor(charLabel)
                .Select(m => AppLanguage.IsJapanese
                    ? $"{m.JaLabel}: {deck.Where(c => m.Filter(c.Id)).Sum(c => c.Count)}枚"
                    : $"{m.EnLabel}: {deck.Where(c => m.Filter(c.Id)).Sum(c => c.Count)}"));

        void ListViewDeck_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (_sortColumn == e.Column)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }

            var colTexts = DeckColumnTexts;
            for (int i = 0; i < listViewDeck.Columns.Count; i++)
                listViewDeck.Columns[i].Text = colTexts[i] + (i == _sortColumn ? (_sortAscending ? " ▲" : " ▼") : "");

            listViewDeck.ListViewItemSorter = new DeckItemComparer(_sortColumn, _sortAscending);
        }

        static string LocalizeType(string type) => AppLanguage.IsJapanese
            ? type switch
            {
                "Attack" => "アタック",
                "Skill"  => "スキル",
                "Power"  => "パワー",
                "Status" => "状態",
                "Curse"  => "呪い",
                "Quest"  => "クエスト",
                _        => type
            }
            : type;

        void BtnImageViewer_Click(object? sender, EventArgs e)
        {
            if (_imageViewer is null || _imageViewer.IsDisposed || !_imageViewer.Visible)
            {
                if (_imageViewer is null || _imageViewer.IsDisposed)
                {
                    _imageViewer = new CardImageViewerForm();
                    ApplySubWindowSettings(_imageViewer, _imageViewerSettings, new Point(Right + 4, Top));
                    _imageViewer.FormClosed += (_, _) =>
                    {
                        _imageViewerSettings = BoundsToSub(_imageViewer.Bounds);
                        UpdateImageViewerButton(false);
                    };
                }
                _imageViewer.Show(this);
                UpdateImageViewerButton(true);

                if (listViewDeck.SelectedItems.Count > 0 &&
                    listViewDeck.SelectedItems[0].Tag is DeckCard selectedCard)
                    _imageViewer.ShowCard(selectedCard.Id, selectedCard.Type);
            }
            else
            {
                _imageViewer.Hide();
                UpdateImageViewerButton(false);
            }
        }

        void UpdateImageViewerButton(bool visible)
        {
            btnImageViewer.Text = visible ? "● 画像ビューア" : "○ 画像ビューア";
            btnImageViewer.ForeColor = visible ? Color.DarkBlue : SystemColors.ControlText;
        }

        void BtnLiveCapture_Click(object? sender, EventArgs e)
        {
            if (_liveCapture is null || _liveCapture.IsDisposed || !_liveCapture.Visible)
            {
                if (_liveCapture is null || _liveCapture.IsDisposed)
                {
                    _liveCapture = new LiveCaptureForm();
                    ApplySubWindowSettings(_liveCapture, _liveCaptureSettings, new Point(Right + 4, Top));
                    _liveCapture.FormClosed += (_, _) =>
                    {
                        _liveCaptureSettings = BoundsToSub(_liveCapture.Bounds);
                        UpdateLiveCaptureButton(false);
                    };
                }
                _liveCapture.Show(this);
                UpdateLiveCaptureButton(true);
            }
            else
            {
                _liveCapture.Hide();
                UpdateLiveCaptureButton(false);
            }
        }

        void UpdateLiveCaptureButton(bool visible)
        {
            btnLiveCapture.Text = visible ? "● ライブキャプチャ" : "○ ライブキャプチャ";
            btnLiveCapture.ForeColor = visible ? Color.DarkBlue : SystemColors.ControlText;
        }

        void BtnCardDetail_Click(object? sender, EventArgs e)
        {
            if (_detailViewer is null || _detailViewer.IsDisposed || !_detailViewer.Visible)
            {
                if (_detailViewer is null || _detailViewer.IsDisposed)
                {
                    _detailViewer = new CardDetailForm();
                    ApplySubWindowSettings(_detailViewer, _cardDetailSettings, new Point(Right + 4, Top));
                    _detailViewer.FormClosed += (_, _) =>
                    {
                        _cardDetailSettings = BoundsToSub(_detailViewer.Bounds);
                        UpdateCardDetailButton(false);
                    };
                }
                _detailViewer.Show(this);
                UpdateCardDetailButton(true);

                if (listViewDeck.SelectedItems.Count > 0 && listViewDeck.SelectedItems[0].Tag is DeckCard selDeck)
                    _detailViewer.UpdateCard(selDeck.Id, isRelic: false, selDeck.EnchantmentId, selDeck.EnchantmentAmount);
                else if (listViewRelics.SelectedItems.Count > 0 && listViewRelics.SelectedItems[0].Tag is string relicId)
                    _detailViewer.UpdateCard(relicId, isRelic: true);
            }
            else
            {
                _detailViewer.Hide();
                UpdateCardDetailButton(false);
            }
        }

        void UpdateCardDetailButton(bool visible)
        {
            btnCardDetail.Text = visible ? "● カード詳細" : "○ カード詳細";
            btnCardDetail.ForeColor = visible ? Color.DarkGreen : SystemColors.ControlText;
        }

        void BtnFilterBlock_Click(object? sender, EventArgs e)
        {
            _blockFilter = !_blockFilter;
            UpdateBlockFilterButton(_blockFilter);
            RefreshDeckList();
        }

        void UpdateBlockFilterButton(bool active)
        {
            btnFilterBlock.Text = active ? "● ブロック関連のみ" : "○ ブロック関連絞り込み";
            btnFilterBlock.ForeColor = active ? Color.DarkBlue : SystemColors.ControlText;
        }

        void BtnHpHistory_Click(object? sender, EventArgs e)
        {
            if (_hpHistory is null || _hpHistory.IsDisposed || !_hpHistory.Visible)
            {
                if (_hpHistory is null || _hpHistory.IsDisposed)
                {
                    _hpHistory = new HpHistoryForm();
                    ApplySubWindowSettings(_hpHistory, _hpHistorySettings, new Point(Right + 4, Top));
                    _hpHistory.FormClosed += (_, _) =>
                    {
                        _hpHistorySettings = BoundsToSub(_hpHistory.Bounds);
                        UpdateHpHistoryButton(false);
                    };
                }
                _hpHistory.Show(this);
                UpdateHpHistoryButton(true);
                RefreshHpHistory();
            }
            else
            {
                _hpHistory.Hide();
                UpdateHpHistoryButton(false);
            }
        }

        void UpdateHpHistoryButton(bool visible)
        {
            btnHpHistory.Text = visible ? "● HP変動" : "○ HP変動";
            btnHpHistory.ForeColor = visible ? Color.DarkRed : SystemColors.ControlText;
        }

        void RefreshHpHistory()
        {
            if (_hpHistory is null || _hpHistory.IsDisposed || !_hpHistory.Visible) return;
            if (_lastRunData is null) return;
            _hpHistory.UpdateHistory(_lastRunData.MapPointHistory);
        }

        void ListViewDeck_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (listViewDeck.SelectedItems.Count == 0) return;
            if (listViewDeck.SelectedItems[0].Tag is not DeckCard card) return;

            if (_imageViewer is { IsDisposed: false } iv && iv.Visible)
                iv.ShowCard(card.Id, card.Type);
            if (_detailViewer is { IsDisposed: false } dv && dv.Visible)
                dv.UpdateCard(card.Id, isRelic: false, card.EnchantmentId, card.EnchantmentAmount);
        }

        void ListViewRelics_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_detailViewer is null || _detailViewer.IsDisposed || !_detailViewer.Visible) return;
            if (listViewRelics.SelectedItems.Count == 0) return;
            if (listViewRelics.SelectedItems[0].Tag is not string id) return;
            _detailViewer.UpdateCard(id, isRelic: true);
        }
    }

    sealed class DeckItemComparer(int column, bool ascending) : System.Collections.IComparer
    {
        // 枚数カラム (index 3) は数値、それ以外は文字列比較
        public int Compare(object? x, object? y)
        {
            var a = (ListViewItem)x!;
            var b = (ListViewItem)y!;
            string sa = column < a.SubItems.Count ? a.SubItems[column].Text : "";
            string sb = column < b.SubItems.Count ? b.SubItems[column].Text : "";

            // コスト (index 2) と枚数 (index 5) は数値比較
            int result = column is 2 or 5 && int.TryParse(sa, out int ia) && int.TryParse(sb, out int ib)
                ? ia.CompareTo(ib)
                : string.Compare(sa, sb, StringComparison.CurrentCulture);

            return ascending ? result : -result;
        }
    }
}
