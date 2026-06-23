using System.Runtime.InteropServices;

namespace StS2Capture.Capture;

/// <summary>
/// 取得済みウィンドウフレーム（= ウィンドウ全体。WGC/GDI とも GetWindowRect 相当を捕捉）の中での
/// 「クライアント領域」矩形を Win32 で求める。ウィンドウモードのタイトルバー／枠を除外し、
/// 全画面では領域＝フレーム全体になる。固定相対座標をこのクライアント領域基準にすることで
/// 解像度可変・タイトルバー有無に依存しない座標が得られる。
/// </summary>
public static class WindowClientArea
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);

    /// <summary>
    /// フレーム内のクライアント領域矩形。取得失敗・不整合時はフレーム全体を返す。
    /// </summary>
    public static Rectangle Resolve(IntPtr hwnd, int frameWidth, int frameHeight)
    {
        var full = new Rectangle(0, 0, frameWidth, frameHeight);
        try
        {
            if (!GetWindowRect(hwnd, out var wr) || !GetClientRect(hwnd, out var cr)) return full;
            var origin = new POINT();
            if (!ClientToScreen(hwnd, ref origin)) return full;

            int offX = origin.X - wr.Left, offY = origin.Y - wr.Top;
            int cw = cr.Right - cr.Left, ch = cr.Bottom - cr.Top;
            if (cw < 8 || ch < 8) return full;

            var rect = new Rectangle(offX, offY, cw, ch);
            rect.Intersect(full);
            return rect.Width < 8 || rect.Height < 8 ? full : rect;
        }
        catch { return full; }
    }
}
