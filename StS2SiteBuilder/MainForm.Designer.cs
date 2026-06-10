partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        _tabControl = new TabControl();
        _tabPreview = new TabPage();
        _previewSplit = new SplitContainer();
        _webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
        _reviewPanel = new Panel();
        _reviewEditor = new TextBox();
        _reviewLabel = new Label();
        _reviewButtons = new FlowLayoutPanel();
        _saveReviewButton = new Button();
        _revertReviewButton = new Button();
        _changelogPanel = new Panel();
        _changelogEditor = new TextBox();
        _changelogLabel = new Label();
        _changelogAddButton = new Button();
        _tabBuild = new TabPage();
        _logBox = new TextBox();
        _buildToolbar = new FlowLayoutPanel();
        _buildButton = new Button();
        _openDistButton = new Button();
        _changeDistButton = new Button();
        _tabHistory = new TabPage();
        _historyPanel = new Panel();
        _historyToolbar = new FlowLayoutPanel();
        _refreshHistoryButton = new Button();
        _tabArticles = new TabPage();
        _articleSplit = new SplitContainer();
        _articleList = new ListBox();
        _articleToolbar = new FlowLayoutPanel();
        _newArticleButton = new Button();
        _deleteArticleButton = new Button();
        _articleBodyBox = new TextBox();
        _articleFieldPanel = new Panel();
        _articleFieldTable = new TableLayoutPanel();
        _articleTitleLabel = new Label();
        _articleTitleBox = new TextBox();
        _articleDateLabel = new Label();
        _articleDatePanel = new Panel();
        _articleDateBox = new TextBox();
        _todayButton = new Button();
        _articleDescLabel = new Label();
        _articleDescBox = new TextBox();
        _articleSlugLabel = new Label();
        _articleSlugBox = new TextBox();
        _articleBodyLabel = new Label();
        _articleActionBar = new FlowLayoutPanel();
        _saveArticleButton = new Button();
        _savePreviewArticleButton = new Button();
        _revertArticleButton = new Button();
        _statusStrip.SuspendLayout();
        _tabControl.SuspendLayout();
        _tabPreview.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_previewSplit).BeginInit();
        _previewSplit.Panel1.SuspendLayout();
        _previewSplit.Panel2.SuspendLayout();
        _previewSplit.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_webView2).BeginInit();
        _reviewPanel.SuspendLayout();
        _reviewButtons.SuspendLayout();
        _changelogPanel.SuspendLayout();
        _tabBuild.SuspendLayout();
        _buildToolbar.SuspendLayout();
        _tabHistory.SuspendLayout();
        _historyToolbar.SuspendLayout();
        _tabArticles.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_articleSplit).BeginInit();
        _articleSplit.Panel1.SuspendLayout();
        _articleSplit.Panel2.SuspendLayout();
        _articleSplit.SuspendLayout();
        _articleToolbar.SuspendLayout();
        _articleFieldPanel.SuspendLayout();
        _articleFieldTable.SuspendLayout();
        _articleDatePanel.SuspendLayout();
        _articleActionBar.SuspendLayout();
        SuspendLayout();
        // 
        // _statusStrip
        // 
        _statusStrip.ImageScalingSize = new Size(24, 24);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
        _statusStrip.Location = new Point(0, 968);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Padding = new Padding(1, 0, 20, 0);
        _statusStrip.Size = new Size(1286, 32);
        _statusStrip.TabIndex = 0;
        // 
        // _statusLabel
        // 
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(1265, 25);
        _statusLabel.Spring = true;
        _statusLabel.Text = "準備完了";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _tabControl
        // 
        _tabControl.Controls.Add(_tabPreview);
        _tabControl.Controls.Add(_tabBuild);
        _tabControl.Controls.Add(_tabHistory);
        _tabControl.Controls.Add(_tabArticles);
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Location = new Point(0, 0);
        _tabControl.Name = "_tabControl";
        _tabControl.SelectedIndex = 0;
        _tabControl.Size = new Size(1286, 968);
        _tabControl.TabIndex = 1;
        // 
        // _tabPreview
        // 
        _tabPreview.Controls.Add(_previewSplit);
        _tabPreview.Dock = DockStyle.Fill;
        _tabPreview.Location = new Point(4, 34);
        _tabPreview.Name = "_tabPreview";
        _tabPreview.Size = new Size(1278, 930);
        _tabPreview.TabIndex = 0;
        _tabPreview.Text = "プレビュー";
        // 
        // _previewSplit
        // 
        _previewSplit.Dock = DockStyle.Fill;
        _previewSplit.Location = new Point(0, 0);
        _previewSplit.Name = "_previewSplit";
        // 
        // _previewSplit.Panel1
        // 
        _previewSplit.Panel1.Controls.Add(_webView2);
        // 
        // _previewSplit.Panel2
        // 
        _previewSplit.Panel2.Controls.Add(_reviewPanel);
        _previewSplit.Panel2.Controls.Add(_changelogPanel);
        _previewSplit.Panel2Collapsed = true;
        _previewSplit.Size = new Size(1278, 930);
        _previewSplit.SplitterDistance = 121;
        _previewSplit.TabIndex = 0;
        // 
        // _webView2
        // 
        _webView2.AllowExternalDrop = true;
        _webView2.CreationProperties = null;
        _webView2.DefaultBackgroundColor = Color.White;
        _webView2.Dock = DockStyle.Fill;
        _webView2.Location = new Point(0, 0);
        _webView2.Name = "_webView2";
        _webView2.Size = new Size(1278, 930);
        _webView2.TabIndex = 0;
        _webView2.ZoomFactor = 1D;
        // 
        // _reviewPanel
        // 
        _reviewPanel.Controls.Add(_reviewEditor);
        _reviewPanel.Controls.Add(_reviewLabel);
        _reviewPanel.Controls.Add(_reviewButtons);
        _reviewPanel.Dock = DockStyle.Fill;
        _reviewPanel.Location = new Point(0, 0);
        _reviewPanel.Name = "_reviewPanel";
        _reviewPanel.Padding = new Padding(8);
        _reviewPanel.Size = new Size(25, 100);
        _reviewPanel.TabIndex = 0;
        // 
        // _reviewEditor
        // 
        _reviewEditor.BackColor = Color.FromArgb(30, 30, 30);
        _reviewEditor.Dock = DockStyle.Fill;
        _reviewEditor.Font = new Font("Consolas", 9F);
        _reviewEditor.ForeColor = Color.FromArgb(220, 220, 220);
        _reviewEditor.Location = new Point(8, 36);
        _reviewEditor.Multiline = true;
        _reviewEditor.Name = "_reviewEditor";
        _reviewEditor.ScrollBars = ScrollBars.Vertical;
        _reviewEditor.Size = new Size(9, 8);
        _reviewEditor.TabIndex = 1;
        // 
        // _reviewLabel
        // 
        _reviewLabel.Dock = DockStyle.Top;
        _reviewLabel.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold);
        _reviewLabel.Location = new Point(8, 8);
        _reviewLabel.Name = "_reviewLabel";
        _reviewLabel.Padding = new Padding(0, 0, 0, 4);
        _reviewLabel.Size = new Size(9, 28);
        _reviewLabel.TabIndex = 0;
        _reviewLabel.Text = "レビュー編集";
        // 
        // _reviewButtons
        // 
        _reviewButtons.Controls.Add(_saveReviewButton);
        _reviewButtons.Controls.Add(_revertReviewButton);
        _reviewButtons.Dock = DockStyle.Bottom;
        _reviewButtons.Location = new Point(8, 44);
        _reviewButtons.Name = "_reviewButtons";
        _reviewButtons.Padding = new Padding(0, 4, 0, 0);
        _reviewButtons.Size = new Size(9, 48);
        _reviewButtons.TabIndex = 2;
        // 
        // _saveReviewButton
        // 
        _saveReviewButton.AutoSize = true;
        _saveReviewButton.Location = new Point(0, 4);
        _saveReviewButton.Margin = new Padding(0, 0, 8, 0);
        _saveReviewButton.Name = "_saveReviewButton";
        _saveReviewButton.Padding = new Padding(12, 4, 12, 4);
        _saveReviewButton.Size = new Size(82, 43);
        _saveReviewButton.TabIndex = 0;
        _saveReviewButton.Text = "保存";
        // 
        // _revertReviewButton
        // 
        _revertReviewButton.AutoSize = true;
        _revertReviewButton.Location = new Point(3, 50);
        _revertReviewButton.Name = "_revertReviewButton";
        _revertReviewButton.Padding = new Padding(12, 4, 12, 4);
        _revertReviewButton.Size = new Size(111, 43);
        _revertReviewButton.TabIndex = 1;
        _revertReviewButton.Text = "元に戻す";
        // 
        // _changelogPanel
        // 
        _changelogPanel.Controls.Add(_changelogEditor);
        _changelogPanel.Controls.Add(_changelogLabel);
        _changelogPanel.Controls.Add(_changelogAddButton);
        _changelogPanel.Dock = DockStyle.Fill;
        _changelogPanel.Location = new Point(0, 0);
        _changelogPanel.Name = "_changelogPanel";
        _changelogPanel.Padding = new Padding(8);
        _changelogPanel.Size = new Size(25, 100);
        _changelogPanel.TabIndex = 1;
        _changelogPanel.Visible = false;
        // 
        // _changelogEditor
        // 
        _changelogEditor.BackColor = Color.FromArgb(30, 30, 30);
        _changelogEditor.Dock = DockStyle.Fill;
        _changelogEditor.Font = new Font("Yu Gothic UI", 10F);
        _changelogEditor.ForeColor = Color.FromArgb(220, 220, 220);
        _changelogEditor.Location = new Point(8, 36);
        _changelogEditor.Multiline = true;
        _changelogEditor.Name = "_changelogEditor";
        _changelogEditor.ScrollBars = ScrollBars.Vertical;
        _changelogEditor.Size = new Size(9, 20);
        _changelogEditor.TabIndex = 1;
        // 
        // _changelogLabel
        // 
        _changelogLabel.Dock = DockStyle.Top;
        _changelogLabel.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold);
        _changelogLabel.Location = new Point(8, 8);
        _changelogLabel.Name = "_changelogLabel";
        _changelogLabel.Padding = new Padding(0, 0, 0, 4);
        _changelogLabel.Size = new Size(9, 28);
        _changelogLabel.TabIndex = 0;
        _changelogLabel.Text = "手動エントリを追加";
        // 
        // _changelogAddButton
        // 
        _changelogAddButton.Dock = DockStyle.Bottom;
        _changelogAddButton.Location = new Point(8, 56);
        _changelogAddButton.Name = "_changelogAddButton";
        _changelogAddButton.Padding = new Padding(12, 4, 12, 4);
        _changelogAddButton.Size = new Size(9, 36);
        _changelogAddButton.TabIndex = 2;
        _changelogAddButton.Text = "追加";
        // 
        // _tabBuild
        // 
        _tabBuild.Controls.Add(_logBox);
        _tabBuild.Controls.Add(_buildToolbar);
        _tabBuild.Dock = DockStyle.Fill;
        _tabBuild.Location = new Point(4, 34);
        _tabBuild.Name = "_tabBuild";
        _tabBuild.Size = new Size(1278, 930);
        _tabBuild.TabIndex = 1;
        _tabBuild.Text = "ビルド";
        // 
        // _logBox
        // 
        _logBox.BackColor = Color.FromArgb(30, 30, 30);
        _logBox.Dock = DockStyle.Fill;
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.ForeColor = Color.FromArgb(220, 220, 220);
        _logBox.Location = new Point(0, 73);
        _logBox.Multiline = true;
        _logBox.Name = "_logBox";
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Size = new Size(1278, 857);
        _logBox.TabIndex = 1;
        // 
        // _buildToolbar
        // 
        _buildToolbar.Controls.Add(_buildButton);
        _buildToolbar.Controls.Add(_openDistButton);
        _buildToolbar.Controls.Add(_changeDistButton);
        _buildToolbar.Dock = DockStyle.Top;
        _buildToolbar.Location = new Point(0, 0);
        _buildToolbar.Name = "_buildToolbar";
        _buildToolbar.Padding = new Padding(11, 10, 11, 10);
        _buildToolbar.Size = new Size(1278, 73);
        _buildToolbar.TabIndex = 0;
        // 
        // _buildButton
        // 
        _buildButton.AutoSize = true;
        _buildButton.Location = new Point(15, 15);
        _buildButton.Margin = new Padding(4, 5, 4, 5);
        _buildButton.Name = "_buildButton";
        _buildButton.Padding = new Padding(17, 7, 17, 7);
        _buildButton.Size = new Size(131, 49);
        _buildButton.TabIndex = 0;
        _buildButton.Text = "サイト生成";
        // 
        // _openDistButton
        // 
        _openDistButton.AutoSize = true;
        _openDistButton.Location = new Point(154, 15);
        _openDistButton.Margin = new Padding(4, 5, 4, 5);
        _openDistButton.Name = "_openDistButton";
        _openDistButton.Padding = new Padding(17, 7, 17, 7);
        _openDistButton.Size = new Size(133, 49);
        _openDistButton.TabIndex = 1;
        _openDistButton.Text = "dist を開く";
        // 
        // _changeDistButton
        // 
        _changeDistButton.AutoSize = true;
        _changeDistButton.Location = new Point(295, 15);
        _changeDistButton.Margin = new Padding(4, 5, 4, 5);
        _changeDistButton.Name = "_changeDistButton";
        _changeDistButton.Padding = new Padding(17, 7, 17, 7);
        _changeDistButton.Size = new Size(172, 49);
        _changeDistButton.TabIndex = 2;
        _changeDistButton.Text = "出力先を変更...";
        // 
        // _tabHistory
        // 
        _tabHistory.Controls.Add(_historyPanel);
        _tabHistory.Controls.Add(_historyToolbar);
        _tabHistory.Dock = DockStyle.Fill;
        _tabHistory.Location = new Point(4, 34);
        _tabHistory.Name = "_tabHistory";
        _tabHistory.Size = new Size(192, 62);
        _tabHistory.TabIndex = 2;
        _tabHistory.Text = "ラン履歴";
        //
        // _historyPanel
        //
        _historyPanel.AutoScroll = true;
        _historyPanel.Dock = DockStyle.Fill;
        _historyPanel.Name = "_historyPanel";
        _historyPanel.TabIndex = 1;
        //
        // _historyToolbar
        //
        _historyToolbar.Controls.Add(_refreshHistoryButton);
        _historyToolbar.Dock = DockStyle.Top;
        _historyToolbar.Location = new Point(0, 0);
        _historyToolbar.Name = "_historyToolbar";
        _historyToolbar.Padding = new Padding(8, 6, 8, 6);
        _historyToolbar.Size = new Size(192, 52);
        _historyToolbar.TabIndex = 0;
        // 
        // _refreshHistoryButton
        // 
        _refreshHistoryButton.AutoSize = true;
        _refreshHistoryButton.Location = new Point(8, 6);
        _refreshHistoryButton.Margin = new Padding(0, 0, 8, 0);
        _refreshHistoryButton.Name = "_refreshHistoryButton";
        _refreshHistoryButton.Padding = new Padding(12, 4, 12, 4);
        _refreshHistoryButton.Size = new Size(82, 43);
        _refreshHistoryButton.TabIndex = 0;
        _refreshHistoryButton.Text = "更新";
        //
        // _tabArticles
        // 
        _tabArticles.Controls.Add(_articleSplit);
        _tabArticles.Dock = DockStyle.Fill;
        _tabArticles.Location = new Point(4, 34);
        _tabArticles.Name = "_tabArticles";
        _tabArticles.Size = new Size(192, 62);
        _tabArticles.TabIndex = 3;
        _tabArticles.Text = "記事";
        // 
        // _articleSplit
        // 
        _articleSplit.Dock = DockStyle.Fill;
        _articleSplit.Location = new Point(0, 0);
        _articleSplit.Name = "_articleSplit";
        // 
        // _articleSplit.Panel1
        // 
        _articleSplit.Panel1.Controls.Add(_articleList);
        _articleSplit.Panel1.Controls.Add(_articleToolbar);
        // 
        // _articleSplit.Panel2
        // 
        _articleSplit.Panel2.Controls.Add(_articleBodyBox);
        _articleSplit.Panel2.Controls.Add(_articleFieldPanel);
        _articleSplit.Panel2.Controls.Add(_articleActionBar);
        _articleSplit.Size = new Size(192, 62);
        _articleSplit.SplitterDistance = 154;
        _articleSplit.TabIndex = 0;
        // 
        // _articleList
        // 
        _articleList.Dock = DockStyle.Fill;
        _articleList.Location = new Point(0, 44);
        _articleList.Name = "_articleList";
        _articleList.Size = new Size(154, 18);
        _articleList.TabIndex = 1;
        // 
        // _articleToolbar
        // 
        _articleToolbar.Controls.Add(_newArticleButton);
        _articleToolbar.Controls.Add(_deleteArticleButton);
        _articleToolbar.Dock = DockStyle.Top;
        _articleToolbar.Location = new Point(0, 0);
        _articleToolbar.Name = "_articleToolbar";
        _articleToolbar.Padding = new Padding(8, 6, 8, 6);
        _articleToolbar.Size = new Size(154, 44);
        _articleToolbar.TabIndex = 0;
        // 
        // _newArticleButton
        // 
        _newArticleButton.AutoSize = true;
        _newArticleButton.Location = new Point(8, 6);
        _newArticleButton.Margin = new Padding(0, 0, 8, 0);
        _newArticleButton.Name = "_newArticleButton";
        _newArticleButton.Padding = new Padding(12, 4, 12, 4);
        _newArticleButton.Size = new Size(82, 43);
        _newArticleButton.TabIndex = 0;
        _newArticleButton.Text = "新規";
        // 
        // _deleteArticleButton
        // 
        _deleteArticleButton.AutoSize = true;
        _deleteArticleButton.Enabled = false;
        _deleteArticleButton.Location = new Point(11, 52);
        _deleteArticleButton.Name = "_deleteArticleButton";
        _deleteArticleButton.Padding = new Padding(12, 4, 12, 4);
        _deleteArticleButton.Size = new Size(82, 43);
        _deleteArticleButton.TabIndex = 1;
        _deleteArticleButton.Text = "削除";
        // 
        // _articleBodyBox
        // 
        _articleBodyBox.BackColor = Color.FromArgb(30, 30, 30);
        _articleBodyBox.Dock = DockStyle.Fill;
        _articleBodyBox.Font = new Font("Consolas", 9.5F);
        _articleBodyBox.ForeColor = Color.FromArgb(220, 220, 220);
        _articleBodyBox.Location = new Point(0, 162);
        _articleBodyBox.Multiline = true;
        _articleBodyBox.Name = "_articleBodyBox";
        _articleBodyBox.ScrollBars = ScrollBars.Both;
        _articleBodyBox.Size = new Size(34, 0);
        _articleBodyBox.TabIndex = 2;
        _articleBodyBox.WordWrap = false;
        // 
        // _articleFieldPanel
        // 
        _articleFieldPanel.Controls.Add(_articleFieldTable);
        _articleFieldPanel.Controls.Add(_articleBodyLabel);
        _articleFieldPanel.Dock = DockStyle.Top;
        _articleFieldPanel.Location = new Point(0, 0);
        _articleFieldPanel.Name = "_articleFieldPanel";
        _articleFieldPanel.Padding = new Padding(8, 6, 8, 2);
        _articleFieldPanel.Size = new Size(34, 162);
        _articleFieldPanel.TabIndex = 1;
        // 
        // _articleFieldTable
        // 
        _articleFieldTable.ColumnCount = 2;
        _articleFieldTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68F));
        _articleFieldTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _articleFieldTable.Controls.Add(_articleTitleLabel, 0, 0);
        _articleFieldTable.Controls.Add(_articleTitleBox, 1, 0);
        _articleFieldTable.Controls.Add(_articleDateLabel, 0, 1);
        _articleFieldTable.Controls.Add(_articleDatePanel, 1, 1);
        _articleFieldTable.Controls.Add(_articleDescLabel, 0, 2);
        _articleFieldTable.Controls.Add(_articleDescBox, 1, 2);
        _articleFieldTable.Controls.Add(_articleSlugLabel, 0, 3);
        _articleFieldTable.Controls.Add(_articleSlugBox, 1, 3);
        _articleFieldTable.Dock = DockStyle.Fill;
        _articleFieldTable.Location = new Point(8, 6);
        _articleFieldTable.Name = "_articleFieldTable";
        _articleFieldTable.RowCount = 4;
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.Size = new Size(18, 132);
        _articleFieldTable.TabIndex = 0;
        // 
        // _articleTitleLabel
        // 
        _articleTitleLabel.Dock = DockStyle.Fill;
        _articleTitleLabel.Location = new Point(3, 0);
        _articleTitleLabel.Name = "_articleTitleLabel";
        _articleTitleLabel.Size = new Size(62, 32);
        _articleTitleLabel.TabIndex = 0;
        _articleTitleLabel.Text = "タイトル";
        _articleTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _articleTitleBox
        // 
        _articleTitleBox.Dock = DockStyle.Fill;
        _articleTitleBox.Location = new Point(71, 3);
        _articleTitleBox.Name = "_articleTitleBox";
        _articleTitleBox.Size = new Size(1, 31);
        _articleTitleBox.TabIndex = 1;
        // 
        // _articleDateLabel
        // 
        _articleDateLabel.Dock = DockStyle.Fill;
        _articleDateLabel.Location = new Point(3, 32);
        _articleDateLabel.Name = "_articleDateLabel";
        _articleDateLabel.Size = new Size(62, 32);
        _articleDateLabel.TabIndex = 0;
        _articleDateLabel.Text = "日付";
        _articleDateLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _articleDatePanel
        // 
        _articleDatePanel.Controls.Add(_articleDateBox);
        _articleDatePanel.Controls.Add(_todayButton);
        _articleDatePanel.Dock = DockStyle.Fill;
        _articleDatePanel.Location = new Point(71, 35);
        _articleDatePanel.Name = "_articleDatePanel";
        _articleDatePanel.Size = new Size(1, 26);
        _articleDatePanel.TabIndex = 1;
        // 
        // _articleDateBox
        // 
        _articleDateBox.Dock = DockStyle.Fill;
        _articleDateBox.Location = new Point(0, 0);
        _articleDateBox.Name = "_articleDateBox";
        _articleDateBox.Size = new Size(0, 31);
        _articleDateBox.TabIndex = 0;
        // 
        // _todayButton
        // 
        _todayButton.Dock = DockStyle.Right;
        _todayButton.Location = new Point(-53, 0);
        _todayButton.Name = "_todayButton";
        _todayButton.Size = new Size(54, 26);
        _todayButton.TabIndex = 1;
        _todayButton.Text = "今日";
        // 
        // _articleDescLabel
        // 
        _articleDescLabel.Dock = DockStyle.Fill;
        _articleDescLabel.Location = new Point(3, 64);
        _articleDescLabel.Name = "_articleDescLabel";
        _articleDescLabel.Size = new Size(62, 32);
        _articleDescLabel.TabIndex = 0;
        _articleDescLabel.Text = "説明";
        _articleDescLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _articleDescBox
        // 
        _articleDescBox.Dock = DockStyle.Fill;
        _articleDescBox.Location = new Point(71, 67);
        _articleDescBox.Name = "_articleDescBox";
        _articleDescBox.Size = new Size(1, 31);
        _articleDescBox.TabIndex = 1;
        // 
        // _articleSlugLabel
        // 
        _articleSlugLabel.Dock = DockStyle.Fill;
        _articleSlugLabel.Location = new Point(3, 96);
        _articleSlugLabel.Name = "_articleSlugLabel";
        _articleSlugLabel.Size = new Size(62, 36);
        _articleSlugLabel.TabIndex = 0;
        _articleSlugLabel.Text = "スラッグ";
        _articleSlugLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _articleSlugBox
        // 
        _articleSlugBox.Dock = DockStyle.Fill;
        _articleSlugBox.Location = new Point(71, 99);
        _articleSlugBox.Name = "_articleSlugBox";
        _articleSlugBox.ReadOnly = true;
        _articleSlugBox.Size = new Size(1, 31);
        _articleSlugBox.TabIndex = 1;
        // 
        // _articleBodyLabel
        // 
        _articleBodyLabel.Dock = DockStyle.Bottom;
        _articleBodyLabel.Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold);
        _articleBodyLabel.ForeColor = Color.FromArgb(110, 110, 110);
        _articleBodyLabel.Location = new Point(8, 138);
        _articleBodyLabel.Name = "_articleBodyLabel";
        _articleBodyLabel.Padding = new Padding(0, 4, 0, 2);
        _articleBodyLabel.Size = new Size(18, 22);
        _articleBodyLabel.TabIndex = 1;
        _articleBodyLabel.Text = "Markdown本文:";
        // 
        // _articleActionBar
        // 
        _articleActionBar.Controls.Add(_saveArticleButton);
        _articleActionBar.Controls.Add(_savePreviewArticleButton);
        _articleActionBar.Controls.Add(_revertArticleButton);
        _articleActionBar.Dock = DockStyle.Bottom;
        _articleActionBar.Location = new Point(0, 18);
        _articleActionBar.Name = "_articleActionBar";
        _articleActionBar.Padding = new Padding(8, 6, 8, 6);
        _articleActionBar.Size = new Size(34, 44);
        _articleActionBar.TabIndex = 3;
        // 
        // _saveArticleButton
        // 
        _saveArticleButton.AutoSize = true;
        _saveArticleButton.Enabled = false;
        _saveArticleButton.Location = new Point(8, 6);
        _saveArticleButton.Margin = new Padding(0, 0, 8, 0);
        _saveArticleButton.Name = "_saveArticleButton";
        _saveArticleButton.Padding = new Padding(12, 4, 12, 4);
        _saveArticleButton.Size = new Size(82, 43);
        _saveArticleButton.TabIndex = 0;
        _saveArticleButton.Text = "保存";
        // 
        // _savePreviewArticleButton
        // 
        _savePreviewArticleButton.AutoSize = true;
        _savePreviewArticleButton.Enabled = false;
        _savePreviewArticleButton.Location = new Point(8, 49);
        _savePreviewArticleButton.Margin = new Padding(0, 0, 8, 0);
        _savePreviewArticleButton.Name = "_savePreviewArticleButton";
        _savePreviewArticleButton.Padding = new Padding(12, 4, 12, 4);
        _savePreviewArticleButton.Size = new Size(170, 43);
        _savePreviewArticleButton.TabIndex = 1;
        _savePreviewArticleButton.Text = "保存してプレビュー";
        // 
        // _revertArticleButton
        // 
        _revertArticleButton.AutoSize = true;
        _revertArticleButton.Enabled = false;
        _revertArticleButton.Location = new Point(11, 95);
        _revertArticleButton.Name = "_revertArticleButton";
        _revertArticleButton.Padding = new Padding(12, 4, 12, 4);
        _revertArticleButton.Size = new Size(111, 43);
        _revertArticleButton.TabIndex = 2;
        _revertArticleButton.Text = "元に戻す";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1286, 1000);
        Controls.Add(_tabControl);
        Controls.Add(_statusStrip);
        Margin = new Padding(4, 5, 4, 5);
        MinimumSize = new Size(848, 629);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "StS2 Site Builder";
        Load += MainForm_Load;
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        _tabControl.ResumeLayout(false);
        _tabPreview.ResumeLayout(false);
        _previewSplit.Panel1.ResumeLayout(false);
        _previewSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_previewSplit).EndInit();
        _previewSplit.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_webView2).EndInit();
        _reviewPanel.ResumeLayout(false);
        _reviewPanel.PerformLayout();
        _reviewButtons.ResumeLayout(false);
        _reviewButtons.PerformLayout();
        _changelogPanel.ResumeLayout(false);
        _changelogPanel.PerformLayout();
        _tabBuild.ResumeLayout(false);
        _tabBuild.PerformLayout();
        _buildToolbar.ResumeLayout(false);
        _buildToolbar.PerformLayout();
        _tabHistory.ResumeLayout(false);
        _historyToolbar.ResumeLayout(false);
        _historyToolbar.PerformLayout();
        _tabArticles.ResumeLayout(false);
        _articleSplit.Panel1.ResumeLayout(false);
        _articleSplit.Panel2.ResumeLayout(false);
        _articleSplit.Panel2.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_articleSplit).EndInit();
        _articleSplit.ResumeLayout(false);
        _articleToolbar.ResumeLayout(false);
        _articleToolbar.PerformLayout();
        _articleFieldPanel.ResumeLayout(false);
        _articleFieldTable.ResumeLayout(false);
        _articleFieldTable.PerformLayout();
        _articleDatePanel.ResumeLayout(false);
        _articleDatePanel.PerformLayout();
        _articleActionBar.ResumeLayout(false);
        _articleActionBar.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private StatusStrip          _statusStrip;
    private ToolStripStatusLabel _statusLabel;
    private TabControl           _tabControl;
    private TabPage              _tabPreview;
    private TabPage              _tabBuild;
    private SplitContainer       _previewSplit;
    private Microsoft.Web.WebView2.WinForms.WebView2 _webView2;
    private Panel                _reviewPanel;
    private Label                _reviewLabel;
    private TextBox              _reviewEditor;
    private FlowLayoutPanel      _reviewButtons;
    private Button               _saveReviewButton;
    private Button               _revertReviewButton;
    private Panel                _changelogPanel;
    private Label                _changelogLabel;
    private TextBox              _changelogEditor;
    private Button               _changelogAddButton;
    private FlowLayoutPanel      _buildToolbar;
    private Button               _buildButton;
    private Button               _openDistButton;
    private Button               _changeDistButton;
    private TextBox              _logBox;
    private TabPage              _tabHistory;
    private FlowLayoutPanel      _historyToolbar;
    private Button               _refreshHistoryButton;
    private Panel                _historyPanel;
    private TabPage              _tabArticles;
    private SplitContainer       _articleSplit;
    private FlowLayoutPanel      _articleToolbar;
    private Button               _newArticleButton;
    private Button               _deleteArticleButton;
    private ListBox              _articleList;
    private Panel                _articleFieldPanel;
    private TableLayoutPanel     _articleFieldTable;
    private Label                _articleTitleLabel;
    private TextBox              _articleTitleBox;
    private Label                _articleDateLabel;
    private Panel                _articleDatePanel;
    private TextBox              _articleDateBox;
    private Button               _todayButton;
    private Label                _articleDescLabel;
    private TextBox              _articleDescBox;
    private Label                _articleSlugLabel;
    private TextBox              _articleSlugBox;
    private Label                _articleBodyLabel;
    private TextBox              _articleBodyBox;
    private FlowLayoutPanel      _articleActionBar;
    private Button               _saveArticleButton;
    private Button               _savePreviewArticleButton;
    private Button               _revertArticleButton;
}
