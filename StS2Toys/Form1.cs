using StS2Toys.Models;
using StS2Toys.Services;

namespace StS2Toys
{
    public partial class Form1 : Form
    {
        private FileSystemWatcher? _watcher;
        private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 500 };
        private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = 2000 };

        // デッキリストのソート状態
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        // カラムヘッダーのベーステキスト（種別カラムは index 2、枚数は index 3）
        private static readonly string[] DeckColumnTexts = ["カード名 (EN)", "カード名 (JP)", "種別", "枚数"];

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
                OpenFile(defaultPath);
        }

        void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveWindowSettings();
            StopWatching();
            _reloadTimer.Dispose();
            _flashTimer.Dispose();
        }

        void RestoreWindowSettings()
        {
            var settings = WindowSettingsService.Load();
            if (settings is null) return;

            var savedBounds = new Rectangle(settings.X, settings.Y, settings.Width, settings.Height);
            var isVisible = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(savedBounds));
            if (!isVisible) return;

            StartPosition = FormStartPosition.Manual;
            Bounds = savedBounds;

            if (settings.State == nameof(FormWindowState.Maximized))
                WindowState = FormWindowState.Maximized;
        }

        void SaveWindowSettings()
        {
            // 最小化中に終了した場合は Normal として扱う
            var state = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowSettingsService.Save(new WindowSettings(
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                state.ToString()));
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
            btnToggleAuto.Text = watching ? "● 監視中" : "○ 自動更新";
            btnToggleAuto.ForeColor = watching ? Color.DarkGreen : SystemColors.ControlText;
        }

        void DisplayData(RunSaveData data)
        {
            if (data.Players.Count == 0) return;
            var player = data.Players[0];

            var characterEn = CardDatabaseService.GetName(player.CharacterId, japanese: false);
            var characterJa = CardDatabaseService.GetName(player.CharacterId, japanese: true);
            lblInfo.Text =
                $"キャラクター: {characterJa} ({characterEn})　" +
                $"アセンション: {data.Ascension}　" +
                $"Act: {data.CurrentActIndex + 1}　　" +
                $"HP: {player.CurrentHp}/{player.MaxHp}　" +
                $"ゴールド: {player.Gold}　" +
                $"エネルギー: {player.MaxEnergy}";

            DisplayDeck(player);
            DisplayRelics(player);
        }

        void DisplayDeck(PlayerData player)
        {
            var grouped = player.Deck
                .GroupBy(c => c.Id)
                .OrderBy(g => CardDatabaseService.GetName(g.Key, japanese: true))
                .Select(g => (
                    Id:    g.Key,
                    En:    CardDatabaseService.GetName(g.Key, japanese: false),
                    Ja:    CardDatabaseService.GetName(g.Key, japanese: true),
                    Type:  CardDatabaseService.GetCardType(g.Key),
                    Count: g.Count()))
                .ToList();

            lblDeckTitle.Text = $"デッキ ({player.Deck.Count}枚)";

            listViewDeck.BeginUpdate();
            listViewDeck.Items.Clear();
            foreach (var (id, en, ja, type, count) in grouped)
            {
                var item = new ListViewItem(en);
                item.SubItems.Add(ja);
                item.SubItems.Add(LocalizeType(type));
                item.SubItems.Add(count.ToString());
                item.Tag = id;
                listViewDeck.Items.Add(item);
            }
            listViewDeck.EndUpdate();
        }

        void DisplayRelics(PlayerData player)
        {
            lblRelicsTitle.Text = $"レリック ({player.Relics.Count}個)";

            listViewRelics.BeginUpdate();
            listViewRelics.Items.Clear();
            foreach (var relic in player.Relics)
            {
                var item = new ListViewItem(CardDatabaseService.GetName(relic.Id, japanese: false));
                item.SubItems.Add(CardDatabaseService.GetName(relic.Id, japanese: true));
                item.Tag = relic.Id;
                listViewRelics.Items.Add(item);
            }
            listViewRelics.EndUpdate();
        }

        void ListViewDeck_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (_sortColumn == e.Column)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }

            for (int i = 0; i < listViewDeck.Columns.Count; i++)
                listViewDeck.Columns[i].Text = DeckColumnTexts[i] +
                    (i == _sortColumn ? (_sortAscending ? " ▲" : " ▼") : "");

            listViewDeck.ListViewItemSorter = new DeckItemComparer(_sortColumn, _sortAscending);
        }

        static string LocalizeType(string type) => type switch
        {
            "Attack" => "アタック",
            "Skill"  => "スキル",
            "Power"  => "パワー",
            "Status" => "状態",
            "Curse"  => "呪い",
            "Quest"  => "クエスト",
            _        => type
        };

        void ListViewDeck_ItemActivate(object? sender, EventArgs e)
        {
            if (listViewDeck.SelectedItems.Count == 0) return;
            if (listViewDeck.SelectedItems[0].Tag is not string id) return;
            using var dlg = new CardDetailForm(id, isRelic: false);
            dlg.ShowDialog(this);
        }

        void ListViewRelics_ItemActivate(object? sender, EventArgs e)
        {
            if (listViewRelics.SelectedItems.Count == 0) return;
            if (listViewRelics.SelectedItems[0].Tag is not string id) return;
            using var dlg = new CardDetailForm(id, isRelic: true);
            dlg.ShowDialog(this);
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

            int result = column == 3 && int.TryParse(sa, out int ia) && int.TryParse(sb, out int ib)
                ? ia.CompareTo(ib)
                : string.Compare(sa, sb, StringComparison.CurrentCulture);

            return ascending ? result : -result;
        }
    }
}
