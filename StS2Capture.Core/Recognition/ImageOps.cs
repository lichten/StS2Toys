using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StS2Capture.Recognition;

/// <summary>
/// タイトル領域 OCR 用の画像前処理ユーティリティ。
/// 色付きバナー背景・縁取りに弱い OCR を助けるため、領域を拡大→グレースケール→
/// Otsu 二値化して「黒文字／白背景」に正規化する。彩度行スキャンはタイトル帯検出に使う。
/// </summary>
public static class ImageOps
{
    /// <summary>領域を切り出し、scale 倍に拡大して Otsu 二値化した 24bpp 画像を返す。</summary>
    public static Bitmap CropUpscaleBinarize(Bitmap src, Rectangle region, int scale)
    {
        region = Rectangle.Intersect(region, new Rectangle(0, 0, src.Width, src.Height));
        if (region.Width <= 0 || region.Height <= 0)
            return new Bitmap(1, 1, PixelFormat.Format24bppRgb);

        int w = Math.Max(1, region.Width * scale);
        int h = Math.Max(1, region.Height * scale);

        // 拡大（バイキュービック）してグレースケール配列へ。
        using var scaled = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(src, new Rectangle(0, 0, w, h), region, GraphicsUnit.Pixel);
        }

        var gray = ToGray(scaled, out int n);
        int threshold = OtsuThreshold(gray);

        // 文字は少数派ピクセル。多数派（背景）を白にする。
        int below = 0;
        for (int i = 0; i < n; i++) if (gray[i] < threshold) below++;
        bool textIsDark = below <= n - below; // 暗い側が少数なら文字＝暗

        var outBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var od = outBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = od.Stride;
            var row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool dark = gray[y * w + x] < threshold;
                    // textIsDark のとき暗ピクセル＝文字＝黒、その他白。
                    bool ink = textIsDark ? dark : !dark;
                    byte v = ink ? (byte)0 : (byte)255;
                    int p = x * 3;
                    row[p] = row[p + 1] = row[p + 2] = v;
                }
                Marshal.Copy(row, 0, od.Scan0 + y * stride, stride);
            }
        }
        finally { outBmp.UnlockBits(od); }
        return outBmp;
    }

    /// <summary>area 内の各行の平均彩度（0..1）を返す。タイトル帯（色バナー）検出用。</summary>
    public static double[] RowSaturation(Bitmap src, Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, src.Width, src.Height));
        var result = new double[Math.Max(0, area.Height)];
        if (area.Width <= 0 || area.Height <= 0) return result;

        var data = src.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var row = new byte[stride];
            for (int y = 0; y < area.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                double sum = 0;
                for (int x = 0; x < area.Width; x++)
                {
                    int p = x * 4;
                    byte b = row[p], gg = row[p + 1], r = row[p + 2];
                    int max = Math.Max(r, Math.Max(gg, b));
                    int min = Math.Min(r, Math.Min(gg, b));
                    sum += max == 0 ? 0 : (double)(max - min) / max;
                }
                result[y] = sum / area.Width;
            }
        }
        finally { src.UnlockBits(data); }
        return result;
    }

    /// <summary>
    /// カードのフレーム色のマスクを作る（長さ w*h・行優先）。枠色の判定はキャラ別の
    /// <see cref="FrameColorProfile"/> に委譲する。Defect の実測青（B 突出）や、未実測キャラ向けの
    /// 色相非依存な彩度リングなどを差し替えられる。背景・絵の除外は後段の形状フィルタと協働する。
    /// </summary>
    public static bool[] BuildFrameMask(Bitmap src, FrameColorProfile profile)
    {
        int w = src.Width, h = src.Height;
        var mask = new bool[w * h];
        var data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                for (int x = 0; x < w; x++)
                {
                    int p = x * 4;
                    int b = row[p], g = row[p + 1], r = row[p + 2];
                    mask[y * w + x] = profile.Matches(r, g, b);
                }
            }
        }
        finally { src.UnlockBits(data); }
        return mask;
    }

    /// <summary>マスクを白黒 24bpp 画像に可視化する（調整用デバッグ保存）。</summary>
    public static Bitmap MaskToBitmap(bool[] mask, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var od = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = od.Stride;
            var row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte v = mask[y * w + x] ? (byte)255 : (byte)0;
                    int p = x * 3;
                    row[p] = row[p + 1] = row[p + 2] = v;
                }
                Marshal.Copy(row, 0, od.Scan0 + y * stride, stride);
            }
        }
        finally { bmp.UnlockBits(od); }
        return bmp;
    }

    /// <summary>area 内の各行の平均輝度（0..255）を返す。カード矩形の上下端検出用。</summary>
    public static double[] RowBrightness(Bitmap src, Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, src.Width, src.Height));
        var result = new double[Math.Max(0, area.Height)];
        if (area.Width <= 0 || area.Height <= 0) return result;

        var data = src.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var row = new byte[stride];
            for (int y = 0; y < area.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                long sum = 0;
                for (int x = 0; x < area.Width; x++)
                {
                    int p = x * 4;
                    sum += (row[p + 2] * 30 + row[p + 1] * 59 + row[p] * 11) / 100;
                }
                result[y] = (double)sum / area.Width;
            }
        }
        finally { src.UnlockBits(data); }
        return result;
    }

    static byte[] ToGray(Bitmap bmp, out int count)
    {
        int w = bmp.Width, h = bmp.Height;
        count = w * h;
        var gray = new byte[count];
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                for (int x = 0; x < w; x++)
                {
                    int p = x * 4;
                    gray[y * w + x] = (byte)((row[p + 2] * 30 + row[p + 1] * 59 + row[p] * 11) / 100);
                }
            }
        }
        finally { bmp.UnlockBits(data); }
        return gray;
    }

    static int OtsuThreshold(byte[] gray)
    {
        Span<int> hist = stackalloc int[256];
        foreach (var v in gray) hist[v]++;
        int total = gray.Length;

        double sum = 0;
        for (int t = 0; t < 256; t++) sum += t * hist[t];

        double sumB = 0;
        int wB = 0;
        double maxVar = -1;
        int threshold = 127;
        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            int wF = total - wB;
            if (wF == 0) break;
            sumB += t * hist[t];
            double mB = sumB / wB;
            double mF = (sum - sumB) / wF;
            double between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > maxVar) { maxVar = between; threshold = t; }
        }
        return threshold;
    }
}
