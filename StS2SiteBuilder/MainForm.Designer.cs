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
        _historySplit         = new SplitContainer();
        _historyToolbar       = new FlowLayoutPanel();
        _refreshHistoryButton = new Button();
        _generateRunButton    = new Button();
        _historyList          = new ListView();
        _historyWebView2      = new Microsoft.Web.WebView2.WinForms.WebView2();
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
        ((System.ComponentModel.ISupportInitialize)_historySplit).BeginInit();
        _historySplit.SuspendLayout();
        _historyToolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_historyWebView2).BeginInit();
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
        _tabHistory.Controls.Add(_historySplit);
        _tabHistory.Dock = DockStyle.Fill;
        _tabHistory.Name = "_tabHistory";
        _tabHistory.Padding = new Padding(0);
        _tabHistory.TabIndex = 2;
        _tabHistory.Text = "ラン履歴";
        //
        // _historySplit
        //
        _historySplit.Dock = DockStyle.Fill;
        _historySplit.Name = "_historySplit";
        _historySplit.Orientation = Orientation.Vertical;
        _historySplit.SplitterDistance = 520;
        _historySplit.TabIndex = 0;
        _historySplit.Panel1.Controls.Add(_historyList);
        _historySplit.Panel1.Controls.Add(_historyToolbar);
        _historySplit.Panel2.Controls.Add(_historyWebView2);
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
        // _historyWebView2
        //
        _historyWebView2.AllowExternalDrop = true;
        _historyWebView2.CreationProperties = null;
        _historyWebView2.DefaultBackgroundColor = Color.White;
        _historyWebView2.Dock = DockStyle.Fill;
        _historyWebView2.Name = "_historyWebView2";
        _historyWebView2.TabIndex = 0;
        _historyWebView2.ZoomFactor = 1D;
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
        ((System.ComponentModel.ISupportInitialize)_historySplit).EndInit();
        _historySplit.ResumeLayout(false);
        _historyToolbar.ResumeLayout(false);
        _historyToolbar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_historyWebView2).EndInit();
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
    private SplitContainer       _historySplit;
    private FlowLayoutPanel      _historyToolbar;
    private Button               _refreshHistoryButton;
    private Button               _generateRunButton;
    private ListView             _historyList;
    private Microsoft.Web.WebView2.WinForms.WebView2 _historyWebView2;
}
