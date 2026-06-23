using System.Diagnostics;

namespace StS2Capture.Capture;

/// <summary>
/// 起動中の Slay the Spire 2 のトップレベルウィンドウ HWND を探す。
/// プロセス名（SlayTheSpire2）優先、見つからなければウィンドウタイトルで照合。
/// </summary>
public static class GameWindowLocator
{
    // ゲームの実行ファイル名（拡張子なし）の候補。
    static readonly string[] ProcessNameHints = { "SlayTheSpire2", "SlayTheSpire", "Slay the Spire 2" };
    const string TitleHint = "Slay the Spire 2";

    public readonly record struct GameWindow(IntPtr Handle, string ProcessName, string Title);

    /// <summary>見つかればウィンドウ情報、無ければ null。</summary>
    public static GameWindow? Find()
    {
        // 1) プロセス名で照合（MainWindowHandle が有効なもの）
        foreach (var p in SafeGetProcesses())
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                if (ProcessNameHints.Any(h => p.ProcessName.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    return new GameWindow(p.MainWindowHandle, p.ProcessName, p.MainWindowTitle);
            }
            catch { /* プロセスが終了した等は無視 */ }
        }

        // 2) ウィンドウタイトルで照合
        foreach (var p in SafeGetProcesses())
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                    p.MainWindowTitle.Contains(TitleHint, StringComparison.OrdinalIgnoreCase))
                    return new GameWindow(p.MainWindowHandle, p.ProcessName, p.MainWindowTitle);
            }
            catch { }
        }

        return null;
    }

    static IEnumerable<Process> SafeGetProcesses()
    {
        try { return Process.GetProcesses(); }
        catch { return Array.Empty<Process>(); }
    }
}
