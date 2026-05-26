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

        _tabControl.SelectedIndexChanged += (_, _) =>
        {
            if (_tabControl.SelectedTab == _tabHistory) RefreshHistoryList();
        };
        _refreshHistoryButton.Click += (_, _) => RefreshHistoryList();
        _generateRunButton.Click    += GenerateRunButton_Click;
        _historyList.SelectedIndexChanged += HistoryList_SelectedIndexChanged;
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

        await _historyWebView2.EnsureCoreWebView2Async(null);
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

    // ── ラン履歴タブ ────────────────────────────────────────────────────────────

    private static readonly CharData[] _baseChars = SiteBuilderCore.GetBaseChars();

    private void RefreshHistoryList()
    {
        _historyList.Items.Clear();
        var dir = RunHistoryService.GetHistoryDir();
        if (!Directory.Exists(dir))
        {
            _statusLabel.Text = "history フォルダが見つかりません";
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

    private void HistoryList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_historyList.SelectedItems.Count == 0) return;
        var summary  = (RunSummary)_historyList.SelectedItems[0].Tag!;
        var distDir  = SiteBuilderCore.GetDistDir();
        var existing = Path.Combine(distDir, "run", $"{summary.StartTime}.html");
        _generateRunButton.Enabled = true;
        _generateRunButton.Text    = File.Exists(existing) ? "HTMLを再生成" : "HTMLを生成";
        if (File.Exists(existing) && _historyWebView2.CoreWebView2 != null)
            _historyWebView2.CoreWebView2.Navigate(new Uri(existing).AbsoluteUri);
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
            if (_historyWebView2.CoreWebView2 != null)
                _historyWebView2.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"エラー: {ex.Message}";
            _generateRunButton.Enabled = true;
        }
    }
}
