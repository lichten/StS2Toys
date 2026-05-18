using System.Diagnostics;

public class MainForm : Form
{
    private readonly Button      _buildButton;
    private readonly Button      _openDistButton;
    private readonly TextBox     _logBox;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly StatusStrip  _statusStrip;

    public MainForm()
    {
        Text            = "StS2 Site Builder";
        Width           = 800;
        Height          = 560;
        MinimumSize     = new Size(600, 400);
        StartPosition   = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock    = DockStyle.Top,
            Height  = 44,
            Padding = new Padding(8, 6, 8, 6),
        };

        _buildButton = new Button
        {
            Text        = "サイト生成",
            AutoSize    = true,
            Padding     = new Padding(12, 4, 12, 4),
        };
        _buildButton.Click += BuildButton_Click;

        _openDistButton = new Button
        {
            Text     = "dist を開く",
            AutoSize = true,
            Padding  = new Padding(12, 4, 12, 4),
        };
        _openDistButton.Click += (_, _) =>
        {
            var distDir = SiteBuilderCore.GetDistDir();
            if (Directory.Exists(distDir))
                Process.Start("explorer.exe", distDir);
        };

        toolbar.Controls.AddRange([_buildButton, _openDistButton]);

        _logBox = new TextBox
        {
            Dock        = DockStyle.Fill,
            Multiline   = true,
            ReadOnly    = true,
            ScrollBars  = ScrollBars.Vertical,
            Font        = new Font("Consolas", 9f),
            BackColor   = Color.FromArgb(30, 30, 30),
            ForeColor   = Color.FromArgb(220, 220, 220),
        };

        _statusLabel = new ToolStripStatusLabel("準備完了");
        _statusStrip = new StatusStrip();
        _statusStrip.Items.Add(_statusLabel);

        Controls.Add(_logBox);
        Controls.Add(toolbar);
        Controls.Add(_statusStrip);
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
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }
        _logBox.AppendText(message + Environment.NewLine);
    }
}
