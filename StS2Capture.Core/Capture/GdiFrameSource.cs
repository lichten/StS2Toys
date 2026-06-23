using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StS2Capture.Capture;

/// <summary>
/// GDI ベースのキャプチャ（フォールバック）。
/// まず PrintWindow(PW_RENDERFULLCONTENT) を試し、黒画面なら画面合成領域の
/// コピー（CopyFromScreen）に切り替える。ウィンドウモードでは概ね機能するが、
/// 排他フルスクリーンや一部 GPU 描画では黒くなり得る（その場合は WGC を使う）。
/// </summary>
public sealed class GdiFrameSource : IFrameSource
{
    public string Name => "GDI (PrintWindow/Screen)";

    const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll")]
    static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    public Bitmap? CaptureFrame(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return null;
        if (!GetWindowRect(hwnd, out var r)) return null;

        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0) return null;

        // 1) PrintWindow
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        try
        {
            using (var g = Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                try
                {
                    if (PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT) && !IsMostlyBlack(bmp))
                    {
                        g.ReleaseHdc(hdc);
                        return bmp;
                    }
                }
                finally { /* hdc 解放は下で */ }
                g.ReleaseHdc(hdc);
            }

            // 2) 画面合成領域のコピーにフォールバック
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }
        catch
        {
            bmp.Dispose();
            return null;
        }
    }

    /// <summary>サンプリングして大半が黒なら true（PrintWindow 失敗の簡易判定）。</summary>
    static bool IsMostlyBlack(Bitmap bmp)
    {
        const int step = 37; // 適当な素数間隔でサンプル
        int sampled = 0, black = 0;
        for (int y = 0; y < bmp.Height; y += step)
            for (int x = 0; x < bmp.Width; x += step)
            {
                var c = bmp.GetPixel(x, y);
                sampled++;
                if (c.R < 8 && c.G < 8 && c.B < 8) black++;
            }
        return sampled > 0 && black >= sampled * 0.98;
    }

    public void Dispose() { }
}
