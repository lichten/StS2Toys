using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StS2Capture.Recognition;

/// <summary>
/// HSV カラーヒストグラム（H12×S3×V3=108bin）による画像照合の共有ユーティリティ。
/// カード絵・レリック・ポーションの識別で共用する。透過 PNG は <paramref name="compositeBg"/> で
/// 背景色に合成してから集計できる（背景を共通定数化すると前景の差だけが効き、小アイコンでも
/// 識別が成立する。実測でレリック/ポーションが背景合成により全て正解1位になった）。
/// </summary>
public static class HsvHistogram
{
    public const int HB = 12, SB = 3, VB = 3, BINS = HB * SB * VB;
    public const int SampleN = 40;

    /// <summary>
    /// <paramref name="region"/> を SampleN×SampleN に縮小して正規化 HSV ヒストグラムを返す。
    /// <paramref name="compositeBg"/> 指定時は α 合成（透過部分を背景色に）してから集計する。
    /// <paramref name="sb"/>/<paramref name="vb"/> で S/V のビン数を変えられる（既定 3,3＝カードと同一。
    /// レリック等の小アイコンは 4,4 など細かくすると識別力が上がる）。
    /// </summary>
    public static float[] Compute(Bitmap src, Rectangle region, Color? compositeBg = null,
        int sb = SB, int vb = VB)
    {
        region = Rectangle.Intersect(region, new Rectangle(0, 0, src.Width, src.Height));
        var hist = new float[HB * sb * vb];
        if (region.Width <= 0 || region.Height <= 0) return hist;

        using var small = new Bitmap(SampleN, SampleN, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(src, new Rectangle(0, 0, SampleN, SampleN), region, GraphicsUnit.Pixel);
        }

        bool composite = compositeBg.HasValue;
        int br = compositeBg?.R ?? 0, bgC = compositeBg?.G ?? 0, bbC = compositeBg?.B ?? 0;

        var data = small.LockBits(new Rectangle(0, 0, SampleN, SampleN),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int count = 0;
        try
        {
            int stride = data.Stride;
            var row = new byte[stride];
            for (int y = 0; y < SampleN; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                for (int x = 0; x < SampleN; x++)
                {
                    int p = x * 4;
                    int b = row[p], gg = row[p + 1], r = row[p + 2], a = row[p + 3];
                    if (composite && a < 255)
                    {
                        double af = a / 255.0;
                        r = (int)(r * af + br * (1 - af));
                        gg = (int)(gg * af + bgC * (1 - af));
                        b = (int)(b * af + bbC * (1 - af));
                    }
                    ToHsv((byte)r, (byte)gg, (byte)b, out double h, out double s, out double v);
                    int hi = Math.Min(HB - 1, (int)(h * HB));
                    int si = Math.Min(sb - 1, (int)(s * sb));
                    int vi = Math.Min(vb - 1, (int)(v * vb));
                    hist[(hi * sb + si) * vb + vi] += 1f;
                    count++;
                }
            }
        }
        finally { small.UnlockBits(data); }

        if (count > 0) for (int i = 0; i < hist.Length; i++) hist[i] /= count;
        return hist;
    }

    /// <summary>
    /// α&gt;=16 のピクセルの外接矩形（透過余白を除いたアート部分）。透過 PNG をアートが枠いっぱいに
    /// 収まるよう切り出してから集計するために使う（ショップアイコンとの枠取りを揃える）。
    /// 透過が無い／全面不透明なら画像全体を返す。
    /// </summary>
    public static Rectangle AlphaBoundingBox(Bitmap src)
    {
        int w = src.Width, h = src.Height;
        var full = new Rectangle(0, 0, w, h);
        var data = src.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        try
        {
            int stride = data.Stride;
            var row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                for (int x = 0; x < w; x++)
                {
                    if (row[x * 4 + 3] < 16) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
        }
        finally { src.UnlockBits(data); }
        if (maxX < 0) return full;
        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    /// <summary>2 つの正規化ヒストグラムのカイ二乗距離（0 に近いほど類似）。</summary>
    public static double ChiSquare(float[] u, float[] v)
    {
        double s = 0;
        for (int i = 0; i < u.Length; i++)
        {
            double d = u[i] - v[i], den = u[i] + v[i];
            if (den > 0) s += d * d / den;
        }
        return s;
    }

    /// <summary>RGB(0-255) → HSV(0..1)。.NET の HSL とは別なので手計算する。</summary>
    public static void ToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf));
        double delta = max - min;
        v = max;
        s = max <= 0 ? 0 : delta / max;
        if (delta <= 0) { h = 0; return; }
        double hue;
        if (max == rf) hue = (gf - bf) / delta % 6;
        else if (max == gf) hue = (bf - rf) / delta + 2;
        else hue = (rf - gf) / delta + 4;
        hue /= 6;
        if (hue < 0) hue += 1;
        h = hue;
    }
}
