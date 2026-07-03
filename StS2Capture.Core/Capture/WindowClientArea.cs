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

            // DPI 整合: WGC/GDI フレームは**物理ピクセル**だが、Win32 の GetWindowRect/GetClientRect/
            // ClientToScreen は呼び出し元プロセスの DPI awareness 空間（本アプリは SystemAware のため、
            // ゲームウィンドウがシステム DPI と異なるモニタにあると**仮想化された論理座標**）を返す。
            // 両者をそのまま混ぜると client 矩形が物理フレーム内でずれ、固定相対バンドが名前/アイコンに
            // 乗らず認識に失敗する。ウィンドウ矩形（論理）とフレーム幅（物理）の比で client 矩形を
            // 物理ピクセルへ換算して整合させる（スケール 1.0＝無縮小時は実質ノーオペ）。
            int winW = wr.Right - wr.Left, winH = wr.Bottom - wr.Top;
            double sx = winW >= 8 ? (double)frameWidth / winW : 1.0;
            double sy = winH >= 8 ? (double)frameHeight / winH : 1.0;

            var rect = new Rectangle(
                (int)Math.Round(offX * sx), (int)Math.Round(offY * sy),
                (int)Math.Round(cw * sx), (int)Math.Round(ch * sy));
            rect.Intersect(full);
            return rect.Width < 8 || rect.Height < 8 ? full : rect;
        }
        catch { return full; }
    }
}
