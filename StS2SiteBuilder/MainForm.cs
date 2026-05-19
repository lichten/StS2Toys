using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
        _buildButton.Click += BuildButton_Click;
        _openDistButton.Click += (_, _) =>
        {
            var d = SiteBuilderCore.GetDistDir();
            if (Directory.Exists(d)) Process.Start("explorer.exe", d);
        };
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
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }
        _logBox.AppendText(message + Environment.NewLine);
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        await _webView2.EnsureCoreWebView2Async(null);
        _webView2.CoreWebView2.Navigate("https://www.microsoft.com");
    }
}
