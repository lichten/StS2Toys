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
        _toolbar = new FlowLayoutPanel();
        _buildButton = new Button();
        _openDistButton = new Button();
        _logBox = new TextBox();
        _webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
        _statusStrip.SuspendLayout();
        _toolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_webView2).BeginInit();
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
        _statusStrip.TabIndex = 2;
        // 
        // _statusLabel
        // 
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(1265, 25);
        _statusLabel.Spring = true;
        _statusLabel.Text = "準備完了";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _toolbar
        // 
        _toolbar.Controls.Add(_buildButton);
        _toolbar.Controls.Add(_openDistButton);
        _toolbar.Dock = DockStyle.Top;
        _toolbar.Location = new Point(0, 0);
        _toolbar.Margin = new Padding(4, 5, 4, 5);
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(11, 10, 11, 10);
        _toolbar.Size = new Size(1286, 73);
        _toolbar.TabIndex = 1;
        // 
        // _buildButton
        // 
        _buildButton.AutoSize = true;
        _buildButton.Location = new Point(15, 15);
        _buildButton.Margin = new Padding(4, 5, 4, 5);
        _buildButton.Name = "_buildButton";
        _buildButton.Padding = new Padding(17, 7, 17, 7);
        _buildButton.Size = new Size(173, 72);
        _buildButton.TabIndex = 0;
        _buildButton.Text = "サイト生成";
        // 
        // _openDistButton
        // 
        _openDistButton.AutoSize = true;
        _openDistButton.Location = new Point(196, 15);
        _openDistButton.Margin = new Padding(4, 5, 4, 5);
        _openDistButton.Name = "_openDistButton";
        _openDistButton.Padding = new Padding(17, 7, 17, 7);
        _openDistButton.Size = new Size(176, 72);
        _openDistButton.TabIndex = 1;
        _openDistButton.Text = "dist を開く";
        // 
        // _logBox
        // 
        _logBox.BackColor = Color.FromArgb(30, 30, 30);
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.ForeColor = Color.FromArgb(220, 220, 220);
        _logBox.Location = new Point(0, 341);
        _logBox.Margin = new Padding(4, 5, 4, 5);
        _logBox.Multiline = true;
        _logBox.Name = "_logBox";
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Size = new Size(1193, 627);
        _logBox.TabIndex = 0;
        // 
        // _webView2
        // 
        _webView2.AllowExternalDrop = true;
        _webView2.CreationProperties = null;
        _webView2.DefaultBackgroundColor = Color.White;
        _webView2.Location = new Point(512, 129);
        _webView2.Name = "_webView2";
        _webView2.Size = new Size(413, 138);
        _webView2.TabIndex = 3;
        _webView2.ZoomFactor = 1D;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1286, 1000);
        Controls.Add(_webView2);
        Controls.Add(_logBox);
        Controls.Add(_toolbar);
        Controls.Add(_statusStrip);
        Margin = new Padding(4, 5, 4, 5);
        MinimumSize = new Size(848, 629);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "StS2 Site Builder";
        Load += MainForm_Load;
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        _toolbar.ResumeLayout(false);
        _toolbar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_webView2).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private FlowLayoutPanel      _toolbar;
    private Button               _buildButton;
    private Button               _openDistButton;
    private TextBox              _logBox;
    private StatusStrip          _statusStrip;
    private ToolStripStatusLabel _statusLabel;
    private Microsoft.Web.WebView2.WinForms.WebView2 _webView2;
}
