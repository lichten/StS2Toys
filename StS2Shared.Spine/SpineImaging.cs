using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace StS2Shared.Spine;

/// <summary>SkiaSharp のビットマップを PNG 化する共通ヘルパ（SiteBuilder / 配布セットアップ共用）。</summary>
public static class SpineImaging
{
    /// <summary>静的テクスチャを w×h の背景(30,30,35)中央にアスペクト維持で配置する。</summary>
    public static SKBitmap FitBitmap(SKBitmap src, int w, int h)
    {
        var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(30, 30, 35));
        float scale = Math.Min((w - 8f) / src.Width, (h - 8f) / src.Height);
        float dw = src.Width * scale, dh = src.Height * scale;
        var dst = new SKRect((w - dw) / 2, (h - dh) / 2, (w + dw) / 2, (h + dh) / 2);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(src, dst, paint);
        return bitmap;
    }

    /// <summary>SKBitmap を Rgba32 の ImageSharp 画像へ変換する。</summary>
    static Image<Rgba32> ToImage(SKBitmap bmp, int w, int h)
    {
        var img = new Image<Rgba32>(w, h);
        var pixels = bmp.Pixels;
        for (int pi = 0; pi < pixels.Length; pi++)
        {
            var p = pixels[pi];
            img[pi % w, pi / w] = new Rgba32(p.Red, p.Green, p.Blue, p.Alpha);
        }
        return img;
    }

    /// <summary>SKBitmap を PNG ファイルへ書き出す。</summary>
    public static void SavePng(SKBitmap bmp, string pngPath, int w, int h)
    {
        using var img = ToImage(bmp, w, h);
        img.SaveAsPng(pngPath);
    }

    /// <summary>SKBitmap を PNG バイト列へエンコードする。</summary>
    public static byte[] EncodePng(SKBitmap bmp, int w, int h)
    {
        using var img = ToImage(bmp, w, h);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>tscn 指定アニメ → idle 系 → 先頭 の優先でアニメ名を選ぶ。無ければ null（静止 setup pose）。</summary>
    public static string? PickAnimationName(CreatureVisual cv, MonsterData data)
    {
        string? animName = null;
        if (cv.Animation is { } a && a != "-- Empty --" &&
            data.SkeletonData.FindAnimation(a) != null)
            animName = a;
        animName ??= data.Animations.FirstOrDefault(
                         x => x.Contains("idle", StringComparison.OrdinalIgnoreCase))
                     ?? data.Animations.FirstOrDefault();
        return animName;
    }
}
