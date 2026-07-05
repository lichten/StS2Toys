using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace StS2Shared.Assets;

/// <summary>
/// Godot のインポート済みテクスチャ（<c>.ctex</c> = GST2 コンテナ）を RGBA 画素にデコードする単一実装。
///
/// 以前は同一フォーマットのデコーダが 3 箇所に重複していた（ctex-to-png・StS2Toys の RelicImageService・
/// StS2SiteBuilder の SpineLoader）。本クラスに集約し、各利用側は出力型（PNG 保存 / System.Drawing.Bitmap /
/// SkiaSharp）への薄い変換だけを持つ。
///
/// GST2 ヘッダレイアウト:
/// <code>
///   [0]  "GST2" magic (4 bytes)
///   [4]  unknown (4)
///   [8]  width  (uint32)
///   [12] height (uint32)
///   [16..35] unknown
///   [36] data_format: 0 = 生 BC データ, 2 = WebP
///   [40..47] unknown
///   [48] Image::Format enum (19 = FORMAT_DXT5/BC3, その他 = FORMAT_BPTC_RGBA/BC7)
///   [52] 生 BC データ  -OR-  uint32 data_size に続く WebP RIFF
/// </code>
/// </summary>
public static class CtexDecoder
{
    const int HeaderSize = 52;

    /// <summary>GST2 バイト列を <see cref="Image{Rgba32}"/> にデコードする。呼び出し側が Dispose すること。</summary>
    public static Image<Rgba32> Decode(byte[] data)
    {
        var magic = System.Text.Encoding.ASCII.GetString(data, 0, 4);
        if (magic != "GST2")
            throw new InvalidDataException($"Expected GST2 magic, got '{magic}'.");

        var width      = (int)BitConverter.ToUInt32(data, 8);
        var height     = (int)BitConverter.ToUInt32(data, 12);
        var dataFormat = BitConverter.ToUInt32(data, 36); // 0=BC raw, 2=WebP
        var imgFormat  = BitConverter.ToUInt32(data, 48);

        return dataFormat == 2
            ? LoadWebP(data, HeaderSize)
            : DecodeBc(data, HeaderSize, width, height, BcFormat(imgFormat));
    }

    /// <summary>
    /// GST2 バイト列を行優先の RGBA32 画素バイト列（<c>width*height*4</c>、R,G,B,A の順）にデコードする。
    /// System.Drawing.Bitmap 等へ直接展開したい利用側向け。
    /// </summary>
    public static byte[] DecodeRgba(byte[] data, out int width, out int height)
    {
        using var image = Decode(data);
        width = image.Width;
        height = image.Height;
        var buffer = new byte[width * height * 4];
        image.CopyPixelDataTo(buffer);
        return buffer;
    }

    /// <summary><c>.ctex</c> ファイルを PNG に変換して保存する。</summary>
    public static void ConvertToPng(string srcCtexPath, string outPngPath)
        => ConvertToPng(File.ReadAllBytes(srcCtexPath), outPngPath);

    /// <summary>GST2 バイト列（PCK から抽出した <c>.ctex</c> 本体など）を PNG に変換して保存する。</summary>
    public static void ConvertToPng(byte[] ctexBytes, string outPngPath)
    {
        using var image = Decode(ctexBytes);
        image.SaveAsPng(outPngPath);
    }

    /// <summary>
    /// <c>.ctex</c> ファイルを JPEG に変換して保存する（白背景で不透明化。<paramref name="maxWidth"/>&gt;0 で縮小）。
    /// </summary>
    public static void ConvertToJpeg(string srcCtexPath, string outJpegPath, int maxWidth, int quality)
    {
        using var image = Decode(File.ReadAllBytes(srcCtexPath));

        if (maxWidth > 0 && image.Width > maxWidth)
            image.Mutate(x => x.Resize(maxWidth, 0));

        image.Mutate(x => x.BackgroundColor(Color.White));
        image.SaveAsJpeg(outJpegPath, new JpegEncoder { Quality = quality });
    }

    /// <summary>
    /// Godot の <c>.png.import</c> から実体 <c>.ctex</c> の <c>res://</c> パスを取り出す。
    /// 単一フォーマット（<c>path=</c>）と複数フォーマット（<c>path.bptc=</c> 等）の両方に対応。無ければ null。
    /// </summary>
    public static string? ParseImportCtexPath(string importPath)
    {
        if (!File.Exists(importPath)) return null;
        return ParseImportCtexPathFromText(File.ReadAllText(importPath));
    }

    /// <summary><see cref="ParseImportCtexPath"/> の文字列版（<c>.import</c> の中身を直接渡す）。</summary>
    public static string? ParseImportCtexPathFromText(string importContent)
    {
        var m = Regex.Match(importContent, @"^path(?:\.\w+)?=""res://(.+?\.ctex)""", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    static CompressionFormat BcFormat(uint imgFormat) => imgFormat switch
    {
        19 => CompressionFormat.Bc3,  // FORMAT_DXT5
        _  => CompressionFormat.Bc7,  // FORMAT_BPTC_RGBA (default)
    };

    static Image<Rgba32> LoadWebP(byte[] data, int headerSize)
    {
        // [headerSize] uint32 webpSize, [headerSize+4] RIFF...WEBP bytes
        var webpSize   = (int)BitConverter.ToUInt32(data, headerSize);
        var webpOffset = headerSize + 4;
        using var ms   = new MemoryStream(data, webpOffset, webpSize);
        return Image.Load<Rgba32>(ms);
    }

    static Image<Rgba32> DecodeBc(byte[] data, int headerSize, int width, int height, CompressionFormat format)
    {
        var bcData    = new ReadOnlyMemory<byte>(data, headerSize, data.Length - headerSize);
        var decoder   = new BcDecoder();
        var pixels    = decoder.DecodeRaw(bcData.ToArray(), width, height, format);
        var rgbaBytes = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
        return Image.LoadPixelData<Rgba32>(rgbaBytes, width, height);
    }
}
