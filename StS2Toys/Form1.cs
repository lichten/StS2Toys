using StS2Toys.Models;
using StS2Toys.Services;

namespace StS2Toys
{
    public partial class Form1 : Form
    {
        private FileSystemWatcher? _watcher;
        private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 500 };
        private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = 2000 };
        private CardImageViewerForm? _imageViewer;
        private CardDetailForm? _detailViewer;
        private SubWindowSettings? _imageViewerSettings;
        private SubWindowSettings? _cardDetailSettings;

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
            _imageViewer?.Close();
            _detailViewer?.Close();
        }

        void RestoreWindowSettings()
        {
            var app = WindowSettingsService.Load();
            _imageViewerSettings = app.ImageViewer;
            _cardDetailSettings = app.CardDetail;

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
                _imageViewerSettings = BoundsToSub(_imageViewer.Bounds);
            if (_detailViewer is { IsDisposed: false })
                _cardDetailSettings = BoundsToSub(_detailViewer.Bounds);

            var state = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var main = new WindowSettings(bounds.X, bounds.Y, bounds.Width, bounds.Height, state.ToString());
            WindowSettingsService.Save(new AppSettings(main, _imageViewerSettings, _cardDetailSettings));
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
                    listViewDeck.SelectedItems[0].Tag is string id)
                    _imageViewer.ShowCard(id);
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

                if (listViewDeck.SelectedItems.Count > 0 && listViewDeck.SelectedItems[0].Tag is string deckId)
                    _detailViewer.UpdateCard(deckId, isRelic: false);
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

        void ListViewDeck_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (listViewDeck.SelectedItems.Count == 0) return;
            if (listViewDeck.SelectedItems[0].Tag is not string id) return;

            if (_imageViewer is { IsDisposed: false } iv && iv.Visible)
                iv.ShowCard(id);
            if (_detailViewer is { IsDisposed: false } dv && dv.Visible)
                dv.UpdateCard(id, isRelic: false);
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

            int result = column == 3 && int.TryParse(sa, out int ia) && int.TryParse(sb, out int ib)
                ? ia.CompareTo(ib)
                : string.Compare(sa, sb, StringComparison.CurrentCulture);

            return ascending ? result : -result;
        }
    }
}
