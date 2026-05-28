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
        _statusStrip          = new StatusStrip();
        _statusLabel          = new ToolStripStatusLabel();
        _tabControl           = new TabControl();
        _tabPreview           = new TabPage();
        _tabBuild             = new TabPage();
        _tabHistory           = new TabPage();
        _historyToolbar       = new FlowLayoutPanel();
        _refreshHistoryButton = new Button();
        _generateRunButton    = new Button();
        _historyList          = new ListView();
        _tabArticles              = new TabPage();
        _articleSplit             = new SplitContainer();
        _articleToolbar           = new FlowLayoutPanel();
        _newArticleButton         = new Button();
        _deleteArticleButton      = new Button();
        _articleList              = new ListBox();
        _articleFieldPanel        = new Panel();
        _articleFieldTable        = new TableLayoutPanel();
        _articleTitleLabel        = new Label();
        _articleTitleBox          = new TextBox();
        _articleDateLabel         = new Label();
        _articleDatePanel         = new Panel();
        _articleDateBox           = new TextBox();
        _todayButton              = new Button();
        _articleDescLabel         = new Label();
        _articleDescBox           = new TextBox();
        _articleSlugLabel         = new Label();
        _articleSlugBox           = new TextBox();
        _articleBodyLabel         = new Label();
        _articleBodyBox           = new TextBox();
        _articleActionBar         = new FlowLayoutPanel();
        _saveArticleButton        = new Button();
        _savePreviewArticleButton = new Button();
        _revertArticleButton      = new Button();
        _previewSplit         = new SplitContainer();
        _webView2             = new Microsoft.Web.WebView2.WinForms.WebView2();
        _reviewPanel          = new Panel();
        _reviewLabel          = new Label();
        _reviewEditor         = new TextBox();
        _reviewButtons        = new FlowLayoutPanel();
        _saveReviewButton     = new Button();
        _revertReviewButton   = new Button();
        _changelogPanel       = new Panel();
        _changelogLabel       = new Label();
        _changelogEditor      = new TextBox();
        _changelogAddButton   = new Button();
        _buildToolbar         = new FlowLayoutPanel();
        _buildButton          = new Button();
        _openDistButton       = new Button();
        _logBox               = new TextBox();
        _statusStrip.SuspendLayout();
        _tabControl.SuspendLayout();
        _tabPreview.SuspendLayout();
        _tabBuild.SuspendLayout();
        _tabHistory.SuspendLayout();
        _historyToolbar.SuspendLayout();
        _tabArticles.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_articleSplit).BeginInit();
        _articleSplit.SuspendLayout();
        _articleToolbar.SuspendLayout();
        _articleFieldPanel.SuspendLayout();
        _articleFieldTable.SuspendLayout();
        _articleDatePanel.SuspendLayout();
        _articleActionBar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_previewSplit).BeginInit();
        _previewSplit.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_webView2).BeginInit();
        _reviewPanel.SuspendLayout();
        _reviewButtons.SuspendLayout();
        _changelogPanel.SuspendLayout();
        _buildToolbar.SuspendLayout();
        SuspendLayout();
        //
        // _statusStrip
        //
        _statusStrip.ImageScalingSize = new Size(24, 24);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Padding = new Padding(1, 0, 20, 0);
        _statusStrip.Size = new Size(1286, 32);
        _statusStrip.TabIndex = 0;
        //
        // _statusLabel
        //
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(0, 25);
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
        _tabControl.Name = "_tabControl";
        _tabControl.SelectedIndex = 0;
        _tabControl.TabIndex = 1;
        //
        // _tabPreview
        //
        _tabPreview.Controls.Add(_previewSplit);
        _tabPreview.Dock = DockStyle.Fill;
        _tabPreview.Name = "_tabPreview";
        _tabPreview.Padding = new Padding(0);
        _tabPreview.TabIndex = 0;
        _tabPreview.Text = "プレビュー";
        //
        // _tabBuild
        //
        _tabBuild.Controls.Add(_logBox);
        _tabBuild.Controls.Add(_buildToolbar);
        _tabBuild.Dock = DockStyle.Fill;
        _tabBuild.Name = "_tabBuild";
        _tabBuild.Padding = new Padding(0);
        _tabBuild.TabIndex = 1;
        _tabBuild.Text = "ビルド";
        //
        // _tabHistory
        //
        _tabHistory.Controls.Add(_historyList);
        _tabHistory.Controls.Add(_historyToolbar);
        _tabHistory.Dock = DockStyle.Fill;
        _tabHistory.Name = "_tabHistory";
        _tabHistory.Padding = new Padding(0);
        _tabHistory.TabIndex = 2;
        _tabHistory.Text = "ラン履歴";
        //
        // _historyToolbar
        //
        _historyToolbar.Controls.Add(_refreshHistoryButton);
        _historyToolbar.Controls.Add(_generateRunButton);
        _historyToolbar.Dock = DockStyle.Top;
        _historyToolbar.Name = "_historyToolbar";
        _historyToolbar.Padding = new Padding(8, 6, 8, 6);
        _historyToolbar.Size = new Size(520, 52);
        _historyToolbar.TabIndex = 0;
        //
        // _refreshHistoryButton
        //
        _refreshHistoryButton.AutoSize = true;
        _refreshHistoryButton.Margin = new Padding(0, 0, 8, 0);
        _refreshHistoryButton.Name = "_refreshHistoryButton";
        _refreshHistoryButton.Padding = new Padding(12, 4, 12, 4);
        _refreshHistoryButton.TabIndex = 0;
        _refreshHistoryButton.Text = "更新";
        //
        // _generateRunButton
        //
        _generateRunButton.AutoSize = true;
        _generateRunButton.Enabled = false;
        _generateRunButton.Name = "_generateRunButton";
        _generateRunButton.Padding = new Padding(12, 4, 12, 4);
        _generateRunButton.TabIndex = 1;
        _generateRunButton.Text = "HTMLを生成";
        //
        // _historyList
        //
        _historyList.Dock = DockStyle.Fill;
        _historyList.FullRowSelect = true;
        _historyList.GridLines = true;
        _historyList.HideSelection = false;
        _historyList.MultiSelect = false;
        _historyList.Name = "_historyList";
        _historyList.TabIndex = 1;
        _historyList.UseCompatibleStateImageBehavior = false;
        _historyList.View = View.Details;
        _historyList.Columns.AddRange(new ColumnHeader[]
        {
            new ColumnHeader { Text = "日付",   Width = 170 },
            new ColumnHeader { Text = "キャラ", Width = 110 },
            new ColumnHeader { Text = "結果",   Width = 70  },
            new ColumnHeader { Text = "A#",     Width = 45  },
            new ColumnHeader { Text = "時間",   Width = 75  },
        });
        //
        // _previewSplit  (horizontal: left=webview, right=review panel, Panel2 collapsed)
        //
        _previewSplit.Dock = DockStyle.Fill;
        _previewSplit.Name = "_previewSplit";
        _previewSplit.Orientation = Orientation.Vertical;
        _previewSplit.SplitterDistance = 900;
        _previewSplit.Panel2Collapsed = true;
        _previewSplit.TabIndex = 0;
        _previewSplit.Panel1.Controls.Add(_webView2);
        _previewSplit.Panel2.Controls.Add(_reviewPanel);
        _previewSplit.Panel2.Controls.Add(_changelogPanel);
        //
        // _webView2
        //
        _webView2.AllowExternalDrop = true;
        _webView2.CreationProperties = null;
        _webView2.DefaultBackgroundColor = Color.White;
        _webView2.Dock = DockStyle.Fill;
        _webView2.Name = "_webView2";
        _webView2.TabIndex = 0;
        _webView2.ZoomFactor = 1D;
        //
        // _reviewPanel
        //
        _reviewPanel.Controls.Add(_reviewEditor);
        _reviewPanel.Controls.Add(_reviewLabel);
        _reviewPanel.Controls.Add(_reviewButtons);
        _reviewPanel.Dock = DockStyle.Fill;
        _reviewPanel.Name = "_reviewPanel";
        _reviewPanel.Padding = new Padding(8);
        _reviewPanel.TabIndex = 0;
        //
        // _reviewLabel
        //
        _reviewLabel.Dock = DockStyle.Top;
        _reviewLabel.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold);
        _reviewLabel.Name = "_reviewLabel";
        _reviewLabel.Padding = new Padding(0, 0, 0, 4);
        _reviewLabel.Size = new Size(0, 28);
        _reviewLabel.TabIndex = 0;
        _reviewLabel.Text = "レビュー編集";
        //
        // _reviewButtons
        //
        _reviewButtons.Controls.Add(_saveReviewButton);
        _reviewButtons.Controls.Add(_revertReviewButton);
        _reviewButtons.Dock = DockStyle.Bottom;
        _reviewButtons.Name = "_reviewButtons";
        _reviewButtons.Padding = new Padding(0, 4, 0, 0);
        _reviewButtons.Size = new Size(0, 48);
        _reviewButtons.TabIndex = 2;
        //
        // _saveReviewButton
        //
        _saveReviewButton.AutoSize = true;
        _saveReviewButton.Margin = new Padding(0, 0, 8, 0);
        _saveReviewButton.Name = "_saveReviewButton";
        _saveReviewButton.Padding = new Padding(12, 4, 12, 4);
        _saveReviewButton.TabIndex = 0;
        _saveReviewButton.Text = "保存";
        //
        // _revertReviewButton
        //
        _revertReviewButton.AutoSize = true;
        _revertReviewButton.Name = "_revertReviewButton";
        _revertReviewButton.Padding = new Padding(12, 4, 12, 4);
        _revertReviewButton.TabIndex = 1;
        _revertReviewButton.Text = "元に戻す";
        //
        // _reviewEditor
        //
        _reviewEditor.BackColor = Color.FromArgb(30, 30, 30);
        _reviewEditor.Dock = DockStyle.Fill;
        _reviewEditor.Font = new Font("Consolas", 9F);
        _reviewEditor.ForeColor = Color.FromArgb(220, 220, 220);
        _reviewEditor.Multiline = true;
        _reviewEditor.Name = "_reviewEditor";
        _reviewEditor.ScrollBars = ScrollBars.Vertical;
        _reviewEditor.TabIndex = 1;
        //
        // _changelogPanel
        //
        _changelogPanel.Controls.Add(_changelogEditor);
        _changelogPanel.Controls.Add(_changelogLabel);
        _changelogPanel.Controls.Add(_changelogAddButton);
        _changelogPanel.Dock = DockStyle.Fill;
        _changelogPanel.Name = "_changelogPanel";
        _changelogPanel.Padding = new Padding(8);
        _changelogPanel.Visible = false;
        _changelogPanel.TabIndex = 1;
        //
        // _changelogLabel
        //
        _changelogLabel.Dock = DockStyle.Top;
        _changelogLabel.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold);
        _changelogLabel.Name = "_changelogLabel";
        _changelogLabel.Padding = new Padding(0, 0, 0, 4);
        _changelogLabel.Size = new Size(0, 28);
        _changelogLabel.TabIndex = 0;
        _changelogLabel.Text = "手動エントリを追加";
        //
        // _changelogAddButton
        //
        _changelogAddButton.Dock = DockStyle.Bottom;
        _changelogAddButton.Name = "_changelogAddButton";
        _changelogAddButton.Padding = new Padding(12, 4, 12, 4);
        _changelogAddButton.Size = new Size(0, 36);
        _changelogAddButton.TabIndex = 2;
        _changelogAddButton.Text = "追加";
        //
        // _changelogEditor
        //
        _changelogEditor.BackColor = Color.FromArgb(30, 30, 30);
        _changelogEditor.Dock = DockStyle.Fill;
        _changelogEditor.Font = new Font("Yu Gothic UI", 10F);
        _changelogEditor.ForeColor = Color.FromArgb(220, 220, 220);
        _changelogEditor.Multiline = true;
        _changelogEditor.Name = "_changelogEditor";
        _changelogEditor.ScrollBars = ScrollBars.Vertical;
        _changelogEditor.TabIndex = 1;
        //
        // _buildToolbar
        //
        _buildToolbar.Controls.Add(_buildButton);
        _buildToolbar.Controls.Add(_openDistButton);
        _buildToolbar.Dock = DockStyle.Top;
        _buildToolbar.Name = "_buildToolbar";
        _buildToolbar.Padding = new Padding(11, 10, 11, 10);
        _buildToolbar.Size = new Size(1286, 73);
        _buildToolbar.TabIndex = 0;
        //
        // _buildButton
        //
        _buildButton.AutoSize = true;
        _buildButton.Margin = new Padding(4, 5, 4, 5);
        _buildButton.Name = "_buildButton";
        _buildButton.Padding = new Padding(17, 7, 17, 7);
        _buildButton.TabIndex = 0;
        _buildButton.Text = "サイト生成";
        //
        // _openDistButton
        //
        _openDistButton.AutoSize = true;
        _openDistButton.Margin = new Padding(4, 5, 4, 5);
        _openDistButton.Name = "_openDistButton";
        _openDistButton.Padding = new Padding(17, 7, 17, 7);
        _openDistButton.TabIndex = 1;
        _openDistButton.Text = "dist を開く";
        //
        // _logBox
        //
        _logBox.BackColor = Color.FromArgb(30, 30, 30);
        _logBox.Dock = DockStyle.Fill;
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.ForeColor = Color.FromArgb(220, 220, 220);
        _logBox.Multiline = true;
        _logBox.Name = "_logBox";
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.TabIndex = 1;
        //
        // _tabArticles
        //
        _tabArticles.Controls.Add(_articleSplit);
        _tabArticles.Dock = DockStyle.Fill;
        _tabArticles.Name = "_tabArticles";
        _tabArticles.Padding = new Padding(0);
        _tabArticles.TabIndex = 3;
        _tabArticles.Text = "記事";
        //
        // _articleSplit
        //
        _articleSplit.Dock = DockStyle.Fill;
        _articleSplit.Name = "_articleSplit";
        _articleSplit.Orientation = Orientation.Vertical;
        _articleSplit.SplitterDistance = 230;
        _articleSplit.TabIndex = 0;
        _articleSplit.Panel1.Controls.Add(_articleList);
        _articleSplit.Panel1.Controls.Add(_articleToolbar);
        _articleSplit.Panel2.Controls.Add(_articleBodyBox);
        _articleSplit.Panel2.Controls.Add(_articleFieldPanel);
        _articleSplit.Panel2.Controls.Add(_articleActionBar);
        //
        // _articleToolbar
        //
        _articleToolbar.Controls.Add(_newArticleButton);
        _articleToolbar.Controls.Add(_deleteArticleButton);
        _articleToolbar.Dock = DockStyle.Top;
        _articleToolbar.Name = "_articleToolbar";
        _articleToolbar.Padding = new Padding(8, 6, 8, 6);
        _articleToolbar.Size = new Size(230, 44);
        _articleToolbar.TabIndex = 0;
        //
        // _newArticleButton
        //
        _newArticleButton.AutoSize = true;
        _newArticleButton.Margin = new Padding(0, 0, 8, 0);
        _newArticleButton.Name = "_newArticleButton";
        _newArticleButton.Padding = new Padding(12, 4, 12, 4);
        _newArticleButton.TabIndex = 0;
        _newArticleButton.Text = "新規";
        //
        // _deleteArticleButton
        //
        _deleteArticleButton.AutoSize = true;
        _deleteArticleButton.Enabled = false;
        _deleteArticleButton.Name = "_deleteArticleButton";
        _deleteArticleButton.Padding = new Padding(12, 4, 12, 4);
        _deleteArticleButton.TabIndex = 1;
        _deleteArticleButton.Text = "削除";
        //
        // _articleList
        //
        _articleList.Dock = DockStyle.Fill;
        _articleList.Name = "_articleList";
        _articleList.TabIndex = 1;
        //
        // _articleFieldPanel
        //
        _articleFieldPanel.Controls.Add(_articleFieldTable);
        _articleFieldPanel.Controls.Add(_articleBodyLabel);
        _articleFieldPanel.Dock = DockStyle.Top;
        _articleFieldPanel.Name = "_articleFieldPanel";
        _articleFieldPanel.Padding = new Padding(8, 6, 8, 2);
        _articleFieldPanel.Size = new Size(0, 162);
        _articleFieldPanel.TabIndex = 1;
        //
        // _articleBodyLabel
        //
        _articleBodyLabel.Dock = DockStyle.Bottom;
        _articleBodyLabel.Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold);
        _articleBodyLabel.ForeColor = Color.FromArgb(110, 110, 110);
        _articleBodyLabel.Name = "_articleBodyLabel";
        _articleBodyLabel.Padding = new Padding(0, 4, 0, 2);
        _articleBodyLabel.Size = new Size(0, 22);
        _articleBodyLabel.TabIndex = 1;
        _articleBodyLabel.Text = "Markdown本文:";
        //
        // _articleFieldTable
        //
        _articleFieldTable.ColumnCount = 2;
        _articleFieldTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68F));
        _articleFieldTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _articleFieldTable.Controls.Add(_articleTitleLabel, 0, 0);
        _articleFieldTable.Controls.Add(_articleTitleBox,   1, 0);
        _articleFieldTable.Controls.Add(_articleDateLabel,  0, 1);
        _articleFieldTable.Controls.Add(_articleDatePanel,  1, 1);
        _articleFieldTable.Controls.Add(_articleDescLabel,  0, 2);
        _articleFieldTable.Controls.Add(_articleDescBox,    1, 2);
        _articleFieldTable.Controls.Add(_articleSlugLabel,  0, 3);
        _articleFieldTable.Controls.Add(_articleSlugBox,    1, 3);
        _articleFieldTable.Dock = DockStyle.Fill;
        _articleFieldTable.Name = "_articleFieldTable";
        _articleFieldTable.RowCount = 4;
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _articleFieldTable.TabIndex = 0;
        //
        // _articleTitleLabel
        //
        _articleTitleLabel.Dock = DockStyle.Fill;
        _articleTitleLabel.Name = "_articleTitleLabel";
        _articleTitleLabel.TabIndex = 0;
        _articleTitleLabel.Text = "タイトル";
        _articleTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _articleTitleBox
        //
        _articleTitleBox.Dock = DockStyle.Fill;
        _articleTitleBox.Name = "_articleTitleBox";
        _articleTitleBox.TabIndex = 1;
        //
        // _articleDateLabel
        //
        _articleDateLabel.Dock = DockStyle.Fill;
        _articleDateLabel.Name = "_articleDateLabel";
        _articleDateLabel.TabIndex = 0;
        _articleDateLabel.Text = "日付";
        _articleDateLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _articleDatePanel
        //
        _articleDatePanel.Controls.Add(_articleDateBox);
        _articleDatePanel.Controls.Add(_todayButton);
        _articleDatePanel.Dock = DockStyle.Fill;
        _articleDatePanel.Name = "_articleDatePanel";
        _articleDatePanel.TabIndex = 1;
        //
        // _articleDateBox
        //
        _articleDateBox.Dock = DockStyle.Fill;
        _articleDateBox.Name = "_articleDateBox";
        _articleDateBox.TabIndex = 0;
        //
        // _todayButton
        //
        _todayButton.Dock = DockStyle.Right;
        _todayButton.Name = "_todayButton";
        _todayButton.Size = new Size(54, 0);
        _todayButton.TabIndex = 1;
        _todayButton.Text = "今日";
        //
        // _articleDescLabel
        //
        _articleDescLabel.Dock = DockStyle.Fill;
        _articleDescLabel.Name = "_articleDescLabel";
        _articleDescLabel.TabIndex = 0;
        _articleDescLabel.Text = "説明";
        _articleDescLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _articleDescBox
        //
        _articleDescBox.Dock = DockStyle.Fill;
        _articleDescBox.Name = "_articleDescBox";
        _articleDescBox.TabIndex = 1;
        //
        // _articleSlugLabel
        //
        _articleSlugLabel.Dock = DockStyle.Fill;
        _articleSlugLabel.Name = "_articleSlugLabel";
        _articleSlugLabel.TabIndex = 0;
        _articleSlugLabel.Text = "スラッグ";
        _articleSlugLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _articleSlugBox
        //
        _articleSlugBox.Dock = DockStyle.Fill;
        _articleSlugBox.Name = "_articleSlugBox";
        _articleSlugBox.ReadOnly = true;
        _articleSlugBox.TabIndex = 1;
        //
        // _articleBodyBox
        //
        _articleBodyBox.BackColor = Color.FromArgb(30, 30, 30);
        _articleBodyBox.Dock = DockStyle.Fill;
        _articleBodyBox.Font = new Font("Consolas", 9.5F);
        _articleBodyBox.ForeColor = Color.FromArgb(220, 220, 220);
        _articleBodyBox.Multiline = true;
        _articleBodyBox.Name = "_articleBodyBox";
        _articleBodyBox.ScrollBars = ScrollBars.Both;
        _articleBodyBox.TabIndex = 2;
        _articleBodyBox.WordWrap = false;
        //
        // _articleActionBar
        //
        _articleActionBar.Controls.Add(_saveArticleButton);
        _articleActionBar.Controls.Add(_savePreviewArticleButton);
        _articleActionBar.Controls.Add(_revertArticleButton);
        _articleActionBar.Dock = DockStyle.Bottom;
        _articleActionBar.Name = "_articleActionBar";
        _articleActionBar.Padding = new Padding(8, 6, 8, 6);
        _articleActionBar.Size = new Size(0, 44);
        _articleActionBar.TabIndex = 3;
        //
        // _saveArticleButton
        //
        _saveArticleButton.AutoSize = true;
        _saveArticleButton.Enabled = false;
        _saveArticleButton.Margin = new Padding(0, 0, 8, 0);
        _saveArticleButton.Name = "_saveArticleButton";
        _saveArticleButton.Padding = new Padding(12, 4, 12, 4);
        _saveArticleButton.TabIndex = 0;
        _saveArticleButton.Text = "保存";
        //
        // _savePreviewArticleButton
        //
        _savePreviewArticleButton.AutoSize = true;
        _savePreviewArticleButton.Enabled = false;
        _savePreviewArticleButton.Margin = new Padding(0, 0, 8, 0);
        _savePreviewArticleButton.Name = "_savePreviewArticleButton";
        _savePreviewArticleButton.Padding = new Padding(12, 4, 12, 4);
        _savePreviewArticleButton.TabIndex = 1;
        _savePreviewArticleButton.Text = "保存してプレビュー";
        //
        // _revertArticleButton
        //
        _revertArticleButton.AutoSize = true;
        _revertArticleButton.Enabled = false;
        _revertArticleButton.Name = "_revertArticleButton";
        _revertArticleButton.Padding = new Padding(12, 4, 12, 4);
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
        _tabBuild.ResumeLayout(false);
        _tabHistory.ResumeLayout(false);
        _historyToolbar.ResumeLayout(false);
        _historyToolbar.PerformLayout();
        _tabArticles.ResumeLayout(false);
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
        ((System.ComponentModel.ISupportInitialize)_previewSplit).EndInit();
        _previewSplit.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_webView2).EndInit();
        _reviewPanel.ResumeLayout(false);
        _reviewPanel.PerformLayout();
        _reviewButtons.ResumeLayout(false);
        _reviewButtons.PerformLayout();
        _changelogPanel.ResumeLayout(false);
        _changelogPanel.PerformLayout();
        _buildToolbar.ResumeLayout(false);
        _buildToolbar.PerformLayout();
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
    private TextBox              _logBox;
    private TabPage              _tabHistory;
    private FlowLayoutPanel      _historyToolbar;
    private Button               _refreshHistoryButton;
    private Button               _generateRunButton;
    private ListView             _historyList;
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
