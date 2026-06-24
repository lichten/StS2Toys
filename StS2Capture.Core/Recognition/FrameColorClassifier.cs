using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StS2Capture.Recognition;

/// <summary>
/// 固定スロットのカード外枠リング帯をサンプリングし、最も枠色がヒットするキャラ（正規化大文字、
/// 例 "DEFECT"）を推定する。固定矩形方式では矩形探索に枠色を使わず、識別の候補プールを
/// キャラで絞り込むためにのみ使う（<see cref="ScreenRecognizer"/>）。
/// colorless 枠は <see cref="FrameColorProfile.ColorlessGray"/> で拾い空文字 "" を返す。
/// </summary>
public static class FrameColorClassifier
{
    /// <summary>キャラ別実測プロファイル＋colorless（"" キー）。<see cref="FrameColorProfile"/> から構築。</summary>
    static readonly (string Key, FrameColorProfile Profile)[] Candidates =
        FrameColorProfile.MeasuredCharacters
            .Select(k => (k, FrameColorProfile.ForCharacter(k)))
            .Append(("", FrameColorProfile.ColorlessGray))
            .ToArray();

    /// <summary>最尤キャラのヒット率がこの値未満なら判定不能（null）とする。実機で較正。</summary>
    public static double MinHitRatio { get; set; } = 0.16;

    /// <summary>外枠リング帯の太さ（カード辺長に対する比）。外周のこの割合だけをサンプリングする。</summary>
    public static double BorderFrac { get; set; } = 0.07;

    /// <summary>
    /// <paramref name="cardRect"/>（フレーム座標のカード全体矩形）の外周リング帯を走査し、
    /// 各キャラプロファイルのヒット率最大のキャラキーを返す。閾値未満は null。
    /// </summary>
    public static string? Classify(Bitmap frame, Rectangle cardRect)
    {
        cardRect = Rectangle.Intersect(cardRect, new Rectangle(0, 0, frame.Width, frame.Height));
        if (cardRect.Width < 8 || cardRect.Height < 8) return null;

        int bx = Math.Max(1, (int)(cardRect.Width * BorderFrac));
        int by = Math.Max(1, (int)(cardRect.Height * BorderFrac));

        var hits = new int[Candidates.Length];
        int ring = 0;

        var data = frame.LockBits(cardRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int w = cardRect.Width, h = cardRect.Height;
            var row = new byte[stride];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, stride);
                bool yEdge = y < by || y >= h - by;
                for (int x = 0; x < w; x++)
                {
                    // 外周リング帯（上下 by / 左右 bx）のみ対象。内側のアートは無視する。
                    if (!yEdge && x >= bx && x < w - bx) continue;
                    int p = x * 4;
                    int b = row[p], g = row[p + 1], r = row[p + 2];
                    ring++;
                    for (int c = 0; c < Candidates.Length; c++)
                        if (Candidates[c].Profile.Matches(r, g, b)) hits[c]++;
                }
            }
        }
        finally { frame.UnlockBits(data); }

        if (ring == 0) return null;

        int best = -1; double bestRatio = 0;
        for (int c = 0; c < Candidates.Length; c++)
        {
            double ratio = (double)hits[c] / ring;
            if (ratio > bestRatio) { bestRatio = ratio; best = c; }
        }
        return bestRatio >= MinHitRatio && best >= 0 ? Candidates[best].Key : null;
    }
}
