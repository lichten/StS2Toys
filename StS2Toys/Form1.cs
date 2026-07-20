using System.Diagnostics;
using StS2Capture;
using StS2Capture.Capture;
using StS2Capture.Recognition;
using StS2Shared.Models;
using StS2Toys.Services;
using StS2Shared.Services;
using StS2Shared.Assets;

namespace StS2Toys
{
    public partial class Form1 : Form
    {
        private FileSystemWatcher? _watcher;
        private string _currentPath = "";
        private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 500 };
        private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = 2000 };
        private DeckOverviewForm? _combinedOverview;
        private DeckOverviewForm? _characterOverview;
        private EncounterOverviewForm? _encounterOverview;
        private HpHistoryForm? _hpHistory;
        private SubWindowSettings? _combinedOverviewSettings;
        private SubWindowSettings? _encounterOverviewSettings;
        private SubWindowSettings? _hpHistorySettings;
        private SubWindowSettings? _characterOverviewSettings;
        private IReadOnlyList<DeckCard>? _lastDeckCards;
        private IReadOnlyList<RelicEntry>? _lastRelics;
        private RunSaveData? _lastRunData;

        // ---- ライブキャプチャ（カード／ショップ検出。旧 LiveCaptureForm から統合） ----
        private readonly CaptureLoop _loop;
        private readonly ShopItemRecognizer _shop = new();
        private readonly ScreenRecognizer _screen;
        private string _lastSignature = "";
        private bool _useGdi;   // false=WGC（既定）、true=GDI。左パネルの取得方式トグルで切替。
        private readonly ContextMenuStrip _linkMenu = new();
        private List<UrlTemplate> _templates = UrlTemplateService.Load();

        /// <summary>検出結果リスト行に付与する、リンク生成用の種別＋ID。</summary>
        sealed record LinkTarget(string Kind, string Id);

        /// <summary>デザイナ既定のウィンドウタイトル（バージョン付与前）。</summary>
        readonly string _baseTitle;

        public Form1()
        {
            // 固定矩形レイアウト方式：画面ごとに固定座標を probe してカード・レリック・ポーションを検出。
            _screen = new ScreenRecognizer(_shop);
            _loop = new CaptureLoop(new WgcFrameSource());
            _loop.ScreenRecognizer = _screen;
            _loop.Updated += OnLoopUpdated;

            InitializeComponent();
            // タイトルにバージョンを付け足すため、デザイナ既定のタイトルを基準として控えておく
            // （UpdateTitle は複数回呼ばれるので、毎回ここから組み直して二重付与を防ぐ）。
            _baseTitle = Text;
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
            _reloadTimer.Tick += (_, _) => { _reloadTimer.Stop(); ReloadCurrentFile(); };
            _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); lblUpdateFlash.Text = ""; };

            WireCaptureEvents();
            UpdateStartupStatus();
        }

        void Form1_Load(object? sender, EventArgs e)
        {
            RestoreWindowSettings();
            MaybeRunAssetSetup();
            // ウィザードでデータバージョンが変わりうるため、MaybeRunAssetSetup の後に組み立てる。
            UpdateTitle();
            var defaultPath = SaveDataService.GetDefaultSavePath();
            if (File.Exists(defaultPath))
            {
                OpenFile(defaultPath);
                RestoreSubWindowVisibility();
            }

            // キャプチャ内容ペインの初期分割（実サイズ確定後に設定）。
            SetSplitterDistance(_outer, (int)(_outer.Width * 0.55));
            SetSplitterDistance(_left, (int)(_left.Height * 0.5));

            // 画面認識の照合 DB（数百枚の画像→HSV ヒストグラム、cold で十数秒）を背景で事前構築し、
            // 初回「手動キャプチャ」の待ちを不可視化する。長時間 CPU を使うため、スレッドプール
            // （通常優先度・キャプチャの Task.Run と競合）ではなく低優先度の専用スレッドで実行する。
            // アセット未解決（未セットアップ）なら各ビルダが空で即返るためスキップ相当。
            new System.Threading.Thread(() =>
            {
                try { if (_screen.IsAvailable) _screen.Warmup(); }
                catch { /* 起動を妨げない */ }
            })
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal,
                Name = "RecognizerWarmup",
            }.Start();
        }

        /// <summary>
        /// 配布モードで、アセット未セットアップ（初回）またはゲーム更新を検出したら
        /// セットアップウィザードを表示する。開発環境（tools/extracted あり）では何もしない。
        /// </summary>
        void MaybeRunAssetSetup()
        {
            // 開発環境ではウォークアップで tools/extracted がヒットするため、ウィザードは出さない。
            if (AssetLocator.HasDevExtracted()) return;

            string? gameVer;
            try { gameVer = SteamLocator.Locate()?.Version; } catch { gameVer = null; }

            var distVer = AssetLocator.InstalledDistributionVersion;      // 導入済み配布バージョン or null
            var skipped = WindowSettingsService.Load().AssetsSkippedVersion;

            bool firstTime = distVer is null;
            bool updated = distVer is not null && gameVer is not null && gameVer != distVer;
            bool suppressed = skipped is not null && skipped == (gameVer ?? DeclinedMarker);

            if ((!firstTime && !updated) || suppressed) return;

            using var wizard = new SetupWizardForm(updated ? distVer : null);
            wizard.ShowDialog(this);

            if (wizard.Outcome == SetupWizardForm.SetupOutcome.Completed)
                SaveAssetState(installed: wizard.InstalledVersion, skipped: null);
            else
                SaveAssetState(installed: WindowSettingsService.Load().AssetsInstalledVersion,
                               skipped: gameVer ?? DeclinedMarker);
        }

        const string DeclinedMarker = "declined";

        /// <summary>
        /// ウィンドウタイトルを「{既定タイトル} v{アプリ版}（データ v{ゲーム版}）」に組み直す。
        /// データバージョンが解決できない（配布モードで未セットアップ）場合は括弧ごと省く。
        /// </summary>
        void UpdateTitle()
        {
            var data = DataVersionService.Current;
            Text = data is null
                ? $"{_baseTitle} v{AppVersion.Display}"
                : $"{_baseTitle} v{AppVersion.Display}（データ {data}）";
        }

        /// <summary>「画像アセット設定」ボタン：いつでもウィザードを手動起動する（スキップした人・更新したい人向け）。</summary>
        void BtnAssetSetup_Click(object? sender, EventArgs e)
        {
            if (AssetLocator.HasDevExtracted())
            {
                MessageBox.Show(this,
                    "開発モード（tools/extracted）でアセットが解決されています。セットアップは不要です。",
                    "画像アセット設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var wizard = new SetupWizardForm(AssetLocator.InstalledDistributionVersion);
            wizard.ShowDialog(this);

            if (wizard.Outcome == SetupWizardForm.SetupOutcome.Completed)
            {
                SaveAssetState(installed: wizard.InstalledVersion, skipped: null);
                UpdateTitle();   // 取り込んだデータバージョンをタイトルへ反映
                MessageBox.Show(this,
                    "抽出が完了しました。画像の表示を反映するにはアプリを再起動してください。",
                    "画像アセット設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        static void SaveAssetState(string? installed, string? skipped)
        {
            var s = WindowSettingsService.Load() with
            {
                AssetsInstalledVersion = installed,
                AssetsSkippedVersion = skipped,
            };
            WindowSettingsService.Save(s);
        }

        void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveWindowSettings();
            StopWatching();
            _reloadTimer.Dispose();
            _flashTimer.Dispose();
            _combinedOverview?.Close();
            _encounterOverview?.Close();
            _hpHistory?.Close();
            _characterOverview?.Close();
            _loop.Dispose();
        }

        void RestoreWindowSettings()
        {
            var app = WindowSettingsService.Load();
            AppLanguage.IsJapanese = app.Language != "en";
            UpdateLangButton();
            _combinedOverviewSettings = app.CombinedOverview;
            _encounterOverviewSettings = app.EncounterOverview;
            _hpHistorySettings = app.HpHistory;
            _characterOverviewSettings = app.CharacterOverview;

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
            if (_combinedOverview is { IsDisposed: false })
                _combinedOverviewSettings = WindowToSub(_combinedOverview);
            if (_characterOverview is { IsDisposed: false })
                _characterOverviewSettings = WindowToSub(_characterOverview);
            if (_encounterOverview is { IsDisposed: false })
                _encounterOverviewSettings = WindowToSub(_encounterOverview);
            if (_hpHistory is { IsDisposed: false })
                _hpHistorySettings = WindowToSub(_hpHistory);

            var state = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var main = new WindowSettings(bounds.X, bounds.Y, bounds.Width, bounds.Height, state.ToString());
            // アセットセットアップ状態は MaybeRunAssetSetup が即時保存するため、ここでは現在値を引き継ぐ。
            var current = WindowSettingsService.Load();
            WindowSettingsService.Save(new AppSettings(main, _hpHistorySettings, _encounterOverviewSettings, splitContainerOuter.SplitterDistance, _characterOverviewSettings, _combinedOverviewSettings, AppLanguage.IsJapanese ? "ja" : "en", current.AssetsInstalledVersion, current.AssetsSkippedVersion));
        }

        static SubWindowSettings WindowToSub(Form form) =>
            new(form.Bounds.X, form.Bounds.Y, form.Bounds.Width, form.Bounds.Height, form.Visible);

        void RestoreSubWindowVisibility()
        {
            if (_combinedOverviewSettings?.Visible == true)  BtnCombinedOverview_Click(null, EventArgs.Empty);
            if (_encounterOverviewSettings?.Visible == true)  BtnEncounterOverview_Click(null, EventArgs.Empty);
            if (_hpHistorySettings?.Visible == true)          BtnHpHistory_Click(null, EventArgs.Empty);
            if (_characterOverviewSettings?.Visible == true)  BtnCharacterOverview_Click(null, EventArgs.Empty);
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
            else if (!string.IsNullOrEmpty(_currentPath))
                StartWatching(_currentPath);
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
                _currentPath = path;
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
            if (string.IsNullOrEmpty(_currentPath)) return;
            try
            {
                var data = SaveDataService.Load(_currentPath);
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

            DisplayDeck(player);
            DisplayRelics(player);
            DisplayPotionOdds(player);
            RefreshHpHistory();
            RefreshEncounterOverview();
        }

        /// <summary>
        /// 次戦闘のポーション報酬ドロップ確率をサイドパネルに表示する（<see cref="PotionOddsService"/>）。
        /// セーブの現在オッズをそのまま反映するため、自動リロードで戦闘後に更新される。
        /// </summary>
        void DisplayPotionOdds(PlayerData player)
        {
            bool ja = AppLanguage.IsJapanese;
            var odds = PotionOddsService.Compute(player);

            if (odds is null)
            {
                lblPotionOdds.Text = ja ? "ポーション: --" : "Potion: --";
                return;
            }

            var o = odds.Value;
            if (o.ForcedByRelic)
            {
                lblPotionOdds.Text = ja
                    ? "ポーション報酬\n確定100%（白き獣の像）"
                    : "Potion reward\n100% (White Beast Statue)";
                return;
            }

            lblPotionOdds.Text = ja
                ? $"ポーション報酬\n通常 {FormatPct(o.Normal)} / エリート {FormatPct(o.Elite)}"
                : $"Potion reward\nNormal {FormatPct(o.Normal)} / Elite {FormatPct(o.Elite)}";
        }

        // ±10% 刻み＋エリート +12.5% なので最大小数第1位。整数なら小数点は付けない。
        static string FormatPct(float v)
        {
            double p = Math.Round(v * 100.0, 1);
            return p == Math.Floor(p) ? $"{p:0}%" : $"{p:0.0}%";
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
        }

        void DisplayRelics(PlayerData player)
        {
            _lastRelics = player.Relics
                .Select(r => new RelicEntry(
                    r.Id,
                    CardDatabaseService.GetName(r.Id, japanese: false),
                    CardDatabaseService.GetName(r.Id, japanese: true)))
                .ToList();

            RefreshCombinedOverview();
            RefreshCharacterOverview();
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

        void BtnCharacterOverview_Click(object? sender, EventArgs e)
        {
            if (_characterOverview is null || _characterOverview.IsDisposed || !_characterOverview.Visible)
            {
                if (_characterOverview is null || _characterOverview.IsDisposed)
                {
                    _characterOverview = new DeckOverviewForm();
                    _characterOverview.EnableCharacterMode();
                    ApplySubWindowSettings(_characterOverview, _characterOverviewSettings, new Point(Right + 4, Top));
                    _characterOverview.FormClosed += (_, _) =>
                    {
                        _characterOverviewSettings = BoundsToSub(_characterOverview.Bounds);
                        UpdateCharacterOverviewButton(false);
                    };
                }
                _characterOverview.Show(this);
                UpdateCharacterOverviewButton(true);
                RefreshCharacterOverview();
            }
            else
            {
                _characterOverview.Hide();
                UpdateCharacterOverviewButton(false);
            }
        }

        void UpdateCharacterOverviewButton(bool visible)
        {
            btnCharacterOverview.Text = visible ? "● キャラクター概観" : "○ キャラクター概観";
            btnCharacterOverview.ForeColor = visible ? Color.DarkMagenta : SystemColors.ControlText;
        }

        void RefreshCharacterOverview()
        {
            if (_characterOverview is null || _characterOverview.IsDisposed || !_characterOverview.Visible) return;
            if (_lastDeckCards is null) return;
            // 先に現在ランのキャラを通知 → デッキ更新で分類・統計が確定する。
            _characterOverview.SetCurrentCharacter(_lastRunData?.Players?.FirstOrDefault()?.CharacterId);
            _characterOverview.UpdateDeck(_lastDeckCards);
            _characterOverview.UpdateRelics(_lastRelics ?? []);
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

        // ---- ライブキャプチャ（旧 LiveCaptureForm から統合） ----

        void WireCaptureEvents()
        {
            _cbAuto.CheckedChanged += (_, _) =>
            {
                if (_cbAuto.Checked) _loop.Start();
                else _loop.Stop();
            };

            // 押下中はボタンを無効化＋「キャプチャ中…」表示で活動を可視化し、二重起動も防ぐ。
            // 重い初期化（WGC 初回フレーム待ち等）が残っても無反応に見えないようにする。
            _btnCapture.Click += async (_, _) =>
            {
                _btnCapture.Enabled = false;
                var prev = _btnCapture.Text;
                _btnCapture.Text = "キャプチャ中…";
                _status.Text = "認識中…";
                try { await Task.Run(_loop.CaptureOnce); }
                finally { _btnCapture.Text = prev; _btnCapture.Enabled = true; }
            };
            _btnLinks.Click += (_, _) => EditLinkTemplates();

            // 検出結果リスト（カード／ショップ）左クリックで情報ページリンクを開く。
            _list.MouseClick += OnListItemClick;
            _ocrList.MouseClick += OnListItemClick;
        }

        void BtnCaptureSource_Click(object? sender, EventArgs e)
        {
            _useGdi = !_useGdi;
            _loop.SetFrameSource(_useGdi ? new GdiFrameSource() : new WgcFrameSource());
            UpdateCaptureSourceButton();
        }

        void UpdateCaptureSourceButton() =>
            btnCaptureSource.Text = _useGdi ? "取得: GDI" : "取得: WGC";

        static void SetSplitterDistance(SplitContainer sc, int distance)
        {
            int extent = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
            int min = sc.Panel1MinSize;
            int max = extent - sc.Panel2MinSize - sc.SplitterWidth;
            if (max < min) return;
            try { sc.SplitterDistance = Math.Clamp(distance, min, max); } catch { }
        }

        void EditLinkTemplates()
        {
            using var dlg = new UrlTemplateSettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _templates = UrlTemplateService.Load();
        }

        void OnListItemClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sender is not ListView lv) return;
            if (lv.HitTest(e.Location).Item is not { Tag: LinkTarget target }) return;

            var links = SiteLinkService.BuildLinks(_templates, target.Kind, target.Id);
            if (links.Count == 0) return;
            if (links.Count == 1) { OpenUrl(links[0].Url); return; }

            // 複数テンプレートはクリック位置のメニューで選択。
            _linkMenu.Items.Clear();
            foreach (var link in links)
            {
                var url = link.Url;
                var item = new ToolStripMenuItem($"{link.Label}: {url}");
                item.Click += (_, _) => OpenUrl(url);
                _linkMenu.Items.Add(item);
            }
            _linkMenu.Show(lv, e.Location);
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

            // ショップ・エンシェントレリック選択はどちらもレリック/ポーション行（result.Shop.Items）を持つ。
            bool hasShopItems = result.Shop is { Items.Count: > 0 };
            var signature = string.Join("|",
                result.Cards.Select(c => $"{c.CardId}:{c.Confidence:F2}"))
                + "##" + string.Join("|", result.TextSpans.Select(s => s.Text))
                + "##SHOP:" + (hasShopItems
                    ? result.Screen + "|" + string.Join("|", result.Shop!.Items.Select(i =>
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

            // ショップ／エンシェントレリック選択ならレリック行、そうでなければ OCR テキスト行を描画（更新経路は1本）。
            _ocrHeaderLabel.Text = result.Screen == ScreenRecognizer.ScreenType.AncientSelect
                ? "エンシェントレリック候補"
                : "ショップ候補（レリック／ポーション）";
            _ocrList.BeginUpdate();
            _ocrList.Items.Clear();
            if (hasShopItems)
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
    }
}
