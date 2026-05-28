using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

public partial class MainForm : Form
{
    private string _currentFilePath = "";
    private string _savedReviewContent = "";
    private bool   _isDirty;

    public MainForm()
    {
        InitializeComponent();
        _buildButton.Click += BuildButton_Click;
        _openDistButton.Click += (_, _) =>
        {
            var d = SiteBuilderCore.GetDistDir();
            if (Directory.Exists(d)) Process.Start("explorer.exe", d);
        };
        _saveReviewButton.Click += SaveReview_Click;
        _revertReviewButton.Click += (_, _) => RevertReview();
        _changelogAddButton.Click += ChangelogAddButton_Click;

        _tabHistory.Enter += (_, _) => RefreshHistoryList();
        _refreshHistoryButton.Click += (_, _) => RefreshHistoryList();
        _generateRunButton.Click    += GenerateRunButton_Click;
        _historyList.SelectedIndexChanged += HistoryList_SelectedIndexChanged;

        _tabArticles.Enter += (_, _) => RefreshArticleList();
        _newArticleButton.Click    += NewArticle_Click;
        _deleteArticleButton.Click += DeleteArticle_Click;
        _saveArticleButton.Click         += SaveArticle_Click;
        _savePreviewArticleButton.Click  += SavePreviewArticle_Click;
        _revertArticleButton.Click       += (_, _) => RevertArticle();
        _todayButton.Click += (_, _) => _articleDateBox.Text = DateTime.Today.ToString("yyyy-MM-dd");
        _articleList.SelectedIndexChanged += ArticleList_SelectedIndexChanged;
        _articleTitleBox.TextChanged += ArticleTitle_TextChanged;
        _articleBodyBox.TextChanged  += (_, _) => MarkArticleDirty();
        _articleDescBox.TextChanged  += (_, _) => MarkArticleDirty();
        _articleDateBox.TextChanged  += (_, _) => MarkArticleDirty();
    }

    private async void BuildButton_Click(object? sender, EventArgs e)
    {
        _buildButton.Enabled    = false;
        _openDistButton.Enabled = false;
        _logBox.Clear();
        _statusLabel.Text = "生成中...";
        try
        {
            var distDir = SiteBuilderCore.GetDistDir();
            await Task.Run(() => SiteBuilderCore.Build(distDir, AppendLog));
            _statusLabel.Text = "完了";
            _tabControl.SelectedTab = _tabPreview;
            if (_webView2.CoreWebView2 != null && !string.IsNullOrEmpty(_currentFilePath))
                _webView2.CoreWebView2.Reload();
        }
        catch (Exception ex)
        {
            AppendLog($"エラー: {ex.Message}");
            _statusLabel.Text = "エラー";
        }
        finally
        {
            _buildButton.Enabled    = true;
            _openDistButton.Enabled = true;
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }
        _logBox.AppendText(message + Environment.NewLine);
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        await _webView2.EnsureCoreWebView2Async(null);
        _webView2.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
        var indexPath = Path.Combine(SiteBuilderCore.GetDistDir(), "index.html");
        _webView2.CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        var uri = _webView2.Source;
        if (uri == null || uri.Scheme != "file")
        {
            SetReviewPanel(null);
            return;
        }
        var filePath = uri.LocalPath;
        SetReviewPanel(filePath);
    }

    private void SetReviewPanel(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            _previewSplit.Panel2Collapsed = true;
            _currentFilePath = "";
            return;
        }

        var distDir       = Path.GetFullPath(SiteBuilderCore.GetDistDir());
        var changelogPath = Path.Combine(distDir, "changelog.html");
        if (string.Equals(Path.GetFullPath(filePath), changelogPath, StringComparison.OrdinalIgnoreCase))
        {
            _currentFilePath        = "";
            _reviewPanel.Visible    = false;
            _changelogPanel.Visible = true;
            _previewSplit.Panel2Collapsed = false;
            return;
        }

        var content = SiteBuilderCore.ExtractReviewPublic(filePath);
        if (content == null)
        {
            _previewSplit.Panel2Collapsed = true;
            _currentFilePath = "";
            return;
        }
        _changelogPanel.Visible = false;
        _reviewPanel.Visible    = true;
        _currentFilePath = filePath;
        _savedReviewContent = content;
        _isDirty = false;
        _reviewEditor.TextChanged -= ReviewEditor_TextChanged;
        _reviewEditor.Text = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _reviewEditor.TextChanged += ReviewEditor_TextChanged;
        _reviewLabel.Text = $"レビュー編集: {Path.GetFileName(filePath)}";
        _previewSplit.Panel2Collapsed = false;
        UpdateReviewButtons();
    }

    private void ChangelogAddButton_Click(object? sender, EventArgs e)
    {
        var text = _changelogEditor.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        SiteBuilderCore.AppendManualChangelogEntry(text);
        _changelogEditor.Clear();
        _statusLabel.Text = "更新履歴に追加しました";
        _webView2.CoreWebView2?.Reload();
    }

    private void ReviewEditor_TextChanged(object? sender, EventArgs e) => MarkDirty();

    private void MarkDirty()
    {
        if (_isDirty) return;
        _isDirty = true;
        UpdateReviewButtons();
    }

    private void UpdateReviewButtons()
    {
        _saveReviewButton.Enabled   = _isDirty;
        _revertReviewButton.Enabled = _isDirty;
    }

    private void SaveReview_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        SiteBuilderCore.SaveReview(_currentFilePath, _reviewEditor.Text.Replace("\r\n", "\n"));
        SiteBuilderCore.AppendChangelogEntry(_currentFilePath);
        _savedReviewContent = _reviewEditor.Text;
        _isDirty = false;
        UpdateReviewButtons();
        _statusLabel.Text = "保存しました";
        _webView2.CoreWebView2?.Reload();
    }

    private void RevertReview()
    {
        _reviewEditor.TextChanged -= ReviewEditor_TextChanged;
        _reviewEditor.Text = _savedReviewContent;
        _reviewEditor.TextChanged += ReviewEditor_TextChanged;
        _isDirty = false;
        UpdateReviewButtons();
    }

    // ── 記事タブ ────────────────────────────────────────────────────────────────

    private bool   _articleIsDirty;
    private bool   _articleIsNew;
    private bool   _articleSuppressDirty;
    private string _articleCurrentFormat = "markdown";
    private string _articleSavedTitle = "";
    private string _articleSavedDate  = "";
    private string _articleSavedDesc  = "";
    private string _articleSavedBody  = "";

    private static string TitleToSlug(string title) =>
        System.Text.RegularExpressions.Regex.Replace(title.ToLowerInvariant(), @"[^\p{L}\p{N}]+", "-").Trim('-');

    private void RefreshArticleList()
    {
        var selectedSlug = (_articleList.SelectedItem as ArticleItem)?.Slug;
        _articleList.SelectedIndexChanged -= ArticleList_SelectedIndexChanged;
        _articleList.Items.Clear();
        var dir = SiteBuilderCore.GetOrCreateArticlesDir();
        foreach (var f in Directory.GetFiles(dir, "*.html").OrderBy(f => f))
        {
            var meta = SiteBuilderCore.ParseArticlePublic(f);
            _articleList.Items.Add(new ArticleItem(Path.GetFileNameWithoutExtension(f), meta.Title));
        }
        _articleList.SelectedIndexChanged += ArticleList_SelectedIndexChanged;
        if (selectedSlug != null)
        {
            for (int i = 0; i < _articleList.Items.Count; i++)
                if (((ArticleItem)_articleList.Items[i]).Slug == selectedSlug)
                { _articleList.SelectedIndex = i; break; }
        }
        _statusLabel.Text = $"記事: {_articleList.Items.Count} 件";
    }

    private void ArticleList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_articleList.SelectedItem is not ArticleItem item) return;
        if (_articleIsDirty && MessageBox.Show(
                "変更を破棄して別の記事を開きますか?",
                "確認", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
        LoadArticle(item.Slug);
        _deleteArticleButton.Enabled = true;
    }

    private void LoadArticle(string slug)
    {
        var dir  = SiteBuilderCore.GetOrCreateArticlesDir();
        var path = Path.Combine(dir, slug + ".html");
        var meta = SiteBuilderCore.ParseArticlePublic(path);
        _articleIsNew = false;
        _articleCurrentFormat = meta.Format;
        _articleSlugBox.ReadOnly  = true;
        _articleSlugBox.BackColor = SystemColors.Control;
        SetArticleFields(meta.Title, meta.Date, meta.Desc, slug, meta.BodyHtml);
        SaveArticleSnapshot();
        _articleIsDirty = false;
        UpdateArticleButtons();
    }

    private void NewArticle_Click(object? sender, EventArgs e)
    {
        if (_articleIsDirty && MessageBox.Show(
                "変更を破棄して新規作成しますか?",
                "確認", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
        _articleList.SelectedIndexChanged -= ArticleList_SelectedIndexChanged;
        _articleList.ClearSelected();
        _articleList.SelectedIndexChanged += ArticleList_SelectedIndexChanged;
        _articleIsNew = true;
        _articleCurrentFormat = "markdown";
        _articleSlugBox.ReadOnly  = false;
        _articleSlugBox.BackColor = SystemColors.Window;
        SetArticleFields("", DateTime.Today.ToString("yyyy-MM-dd"), "", "", "");
        SaveArticleSnapshot();
        _articleIsDirty = false;
        _deleteArticleButton.Enabled = false;
        UpdateArticleButtons();
        _articleTitleBox.Focus();
    }

    private void SetArticleFields(string title, string date, string desc, string slug, string body)
    {
        _articleSuppressDirty = true;
        try
        {
            _articleTitleBox.Text = title;
            _articleDateBox.Text  = date;
            _articleDescBox.Text  = desc;
            _articleSlugBox.Text  = slug;
            _articleBodyBox.Text  = body.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
        finally
        {
            _articleSuppressDirty = false;
        }
    }

    private void SaveArticleSnapshot()
    {
        _articleSavedTitle = _articleTitleBox.Text;
        _articleSavedDate  = _articleDateBox.Text;
        _articleSavedDesc  = _articleDescBox.Text;
        _articleSavedBody  = _articleBodyBox.Text;
    }

    private void ArticleTitle_TextChanged(object? sender, EventArgs e)
    {
        if (_articleIsNew && !_articleSuppressDirty)
            _articleSlugBox.Text = TitleToSlug(_articleTitleBox.Text);
        MarkArticleDirty();
    }

    private void MarkArticleDirty()
    {
        if (_articleSuppressDirty || _articleIsDirty) return;
        _articleIsDirty = true;
        UpdateArticleButtons();
    }

    private void UpdateArticleButtons()
    {
        var hasSlug  = !string.IsNullOrWhiteSpace(_articleSlugBox.Text);
        var hasTitle = !string.IsNullOrWhiteSpace(_articleTitleBox.Text);
        var canSave  = _articleIsDirty && hasSlug && hasTitle;
        _saveArticleButton.Enabled        = canSave;
        _savePreviewArticleButton.Enabled = canSave;
        _revertArticleButton.Enabled      = _articleIsDirty;
    }

    private void RevertArticle()
    {
        SetArticleFields(_articleSavedTitle, _articleSavedDate, _articleSavedDesc,
                         _articleSlugBox.Text, _articleSavedBody);
        _articleIsDirty = false;
        UpdateArticleButtons();
    }

    private bool DoSaveArticle()
    {
        var slug  = _articleSlugBox.Text.Trim();
        var title = _articleTitleBox.Text.Trim();
        var date  = _articleDateBox.Text.Trim();
        var desc  = _articleDescBox.Text.Trim();
        var body  = _articleBodyBox.Text.Replace("\r\n", "\n");
        if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(title)) return false;
        var dir = SiteBuilderCore.GetOrCreateArticlesDir();
        SiteBuilderCore.SaveArticle(dir, slug, title, date, desc, body, _articleCurrentFormat);
        _articleIsNew            = false;
        _articleSlugBox.ReadOnly  = true;
        _articleSlugBox.BackColor = SystemColors.Control;
        SaveArticleSnapshot();
        _articleIsDirty = false;
        UpdateArticleButtons();
        RefreshArticleList();
        return true;
    }

    private async void SaveArticle_Click(object? sender, EventArgs e)
    {
        if (!DoSaveArticle()) return;
        await BuildArticlesOnlyAsync();
    }

    private async void SavePreviewArticle_Click(object? sender, EventArgs e)
    {
        if (!DoSaveArticle()) return;
        await BuildArticlesOnlyAsync();
        var distDir  = SiteBuilderCore.GetDistDir();
        var slug     = _articleSlugBox.Text.Trim();
        var htmlPath = Path.Combine(distDir, "articles", slug + ".html");
        _tabControl.SelectedTab = _tabPreview;
        if (_webView2.CoreWebView2 != null && File.Exists(htmlPath))
            _webView2.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
    }

    private async Task BuildArticlesOnlyAsync()
    {
        _saveArticleButton.Enabled        = false;
        _savePreviewArticleButton.Enabled = false;
        _statusLabel.Text = "記事ビルド中...";
        try
        {
            var distDir = SiteBuilderCore.GetDistDir();
            await Task.Run(() => SiteBuilderCore.BuildArticlesOnly(distDir, AppendLog));
            _statusLabel.Text = "保存しました";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"エラー: {ex.Message}";
        }
        finally
        {
            UpdateArticleButtons();
        }
    }

    private void DeleteArticle_Click(object? sender, EventArgs e)
    {
        if (_articleList.SelectedItem is not ArticleItem item) return;
        if (MessageBox.Show($"「{item.Title}」を削除しますか?",
                "削除確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        var dir = SiteBuilderCore.GetOrCreateArticlesDir();
        SiteBuilderCore.DeleteArticle(dir, item.Slug, SiteBuilderCore.GetDistDir());
        _articleIsDirty = false;
        SetArticleFields("", "", "", "", "");
        SaveArticleSnapshot();
        _deleteArticleButton.Enabled = false;
        UpdateArticleButtons();
        RefreshArticleList();
        _statusLabel.Text = "削除しました";
    }

    private sealed class ArticleItem(string slug, string title)
    {
        public string Slug  { get; } = slug;
        public string Title { get; } = title;
        public override string ToString() => string.IsNullOrEmpty(Title) ? Slug : Title;
    }

    // ── ラン履歴タブ ────────────────────────────────────────────────────────────

    private static readonly CharData[] _baseChars = SiteBuilderCore.GetBaseChars();

    private void RefreshHistoryList()
    {
        try
        {
            _historyList.Items.Clear();
            var dir = RunHistoryService.GetHistoryDir();
            if (!Directory.Exists(dir))
            {
                _statusLabel.Text = $"history フォルダが見つかりません: {dir}";
                return;
            }
            var distDir   = SiteBuilderCore.GetDistDir();
            var summaries = RunHistoryService.LoadSummaries(dir);
            foreach (var s in summaries)
            {
                var date       = DateTimeOffset.FromUnixTimeSeconds(s.StartTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
                var charJa     = _baseChars.FirstOrDefault(c => c.Id == s.CharacterId)?.JaName ?? s.CharacterId;
                var resultText = s.Win ? "勝利" : s.WasAbandoned ? "離脱" : "敗北";
                var time       = $"{s.RunTime / 60}:{s.RunTime % 60:D2}";
                var item       = new ListViewItem(date);
                item.SubItems.Add(charJa);
                item.SubItems.Add(resultText);
                item.SubItems.Add($"A{s.Ascension}");
                item.SubItems.Add(time);
                item.Tag = s;
                var existing = Path.Combine(distDir, "run", $"{s.StartTime}.html");
                item.ForeColor = File.Exists(existing) ? Color.Black : Color.Gray;
                _historyList.Items.Add(item);
            }
            _statusLabel.Text = $"ラン履歴: {summaries.Count} 件";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"[エラー] {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void HistoryList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_historyList.SelectedItems.Count == 0) return;
        var summary  = (RunSummary)_historyList.SelectedItems[0].Tag!;
        var distDir  = SiteBuilderCore.GetDistDir();
        var existing = Path.Combine(distDir, "run", $"{summary.StartTime}.html");
        _generateRunButton.Enabled = true;
        _generateRunButton.Text    = File.Exists(existing) ? "HTMLを再生成" : "HTMLを生成";
    }

    private async void GenerateRunButton_Click(object? sender, EventArgs e)
    {
        if (_historyList.SelectedItems.Count == 0) return;
        var summary = (RunSummary)_historyList.SelectedItems[0].Tag!;
        _generateRunButton.Enabled = false;
        _statusLabel.Text = "HTML生成中...";
        try
        {
            var run     = await Task.Run(() => RunHistoryService.Load(summary.FilePath));
            var distDir = SiteBuilderCore.GetDistDir();
            var htmlPath = await Task.Run(() => SiteBuilderCore.WriteRunPage(run, _baseChars, distDir));
            _statusLabel.Text = "生成完了";
            _generateRunButton.Text = "HTMLを再生成";
            _generateRunButton.Enabled = true;
            // update color in list
            if (_historyList.SelectedItems.Count > 0)
                _historyList.SelectedItems[0].ForeColor = Color.Black;
            _tabControl.SelectedTab = _tabPreview;
            if (_webView2.CoreWebView2 != null)
                _webView2.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"エラー: {ex.Message}";
            _generateRunButton.Enabled = true;
        }
    }
}
