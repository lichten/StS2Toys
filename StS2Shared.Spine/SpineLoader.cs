using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SkiaSharp;
using Spine;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISImage  = SixLabors.ImageSharp.Image;
using ISRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace StS2Shared.Spine;

public record MonsterData(SkeletonData SkeletonData, Atlas Atlas, SKBitmap Texture, string[] Animations);

/// <summary>
/// Spine のスケルトン・アトラス・テクスチャを <see cref="IAssetSource"/> 経由でロードする
/// （ディスク＝SiteBuilder / .pck 直読み＝配布セットアップ の双方で共用）。パスは論理パス（res:// 無し）。
/// </summary>
public static class SpineLoader
{
    static readonly Regex ImportPathRegex = new(@"path=""res://(.+?)""", RegexOptions.Compiled);

    /// <summary>
    /// 明示的な .skel.import / .atlas.import（論理パス）を指定してロードする
    /// （creature_visuals の .tscn → .tres で解決した特定リグ用）。
    /// テクスチャは atlas_data 先頭行のページ名から同フォルダの .png.import を引く。
    /// </summary>
    public static MonsterData LoadFromImports(string skelImport, string atlasImport, IAssetSource src)
    {
        var spskelPath  = ResolveImportPath(src, skelImport);
        var spatlasPath = ResolveImportPath(src, atlasImport);

        var spatlasBytes = src.Read(spatlasPath)
            ?? throw new FileNotFoundException($"spatlas not found: {spatlasPath}");
        using var spatlasDoc = JsonDocument.Parse(spatlasBytes);
        var atlasText     = spatlasDoc.RootElement.GetProperty("atlas_data").GetString()!;

        // atlas_data 先頭の非空行 = ページ画像名（例 "bowlbug.png"）
        var atlasDir = ResDir(atlasImport);
        var pageName = atlasText.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        string? pngImport = null;
        if (pageName is not null)
        {
            var candidate = atlasDir + "/" + pageName + ".import";
            if (src.Exists(candidate)) pngImport = candidate;
        }
        pngImport ??= src.List(atlasDir, ".png.import").FirstOrDefault()
            ?? throw new FileNotFoundException($"*.png.import not found in {atlasDir}");
        var ctexPath  = ResolveImportPath(src, pngImport);
        var ctexBytes = src.Read(ctexPath) ?? throw new FileNotFoundException($"ctex not found: {ctexPath}");
        var texture   = LoadCtexAsSKBitmap(ctexBytes);

        var textureLoader = new SkiaTextureLoader(texture);
        var atlas = new Atlas(new StringReader(atlasText), "", textureLoader);

        var attachmentLoader = new AtlasAttachmentLoader(atlas);
        var binary           = new SkeletonBinary(attachmentLoader);
        var skelBytes = src.Read(spskelPath) ?? throw new FileNotFoundException($"skel not found: {spskelPath}");
        using var stream = new MemoryStream(skelBytes);
        var skeletonData = binary.ReadSkeletonData(stream);

        var animations = skeletonData.Animations.Select(a => a.Name).ToArray();
        return new MonsterData(skeletonData, atlas, texture, animations);
    }

    /// <summary>.import ファイル（論理パス）を読み、参照先実体の論理パス（<c>path="res://…"</c> の中身）を返す。</summary>
    public static string ResolveImportPath(IAssetSource src, string importFile)
    {
        var bytes = src.Read(importFile) ?? throw new FileNotFoundException($".import not found: {importFile}");
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        var m = ImportPathRegex.Match(content);
        if (!m.Success)
            throw new InvalidDataException($"path= not found in {importFile}");
        return m.Groups[1].Value.Replace('\\', '/');
    }

    /// <summary>論理パスの親ディレクトリ（スラッシュ区切り）。</summary>
    static string ResDir(string resPath)
    {
        var i = resPath.LastIndexOf('/');
        return i < 0 ? "" : resPath[..i];
    }

    public static SKBitmap LoadCtexAsSKBitmap(byte[] data)
    {
        if (System.Text.Encoding.ASCII.GetString(data, 0, 4) != "GST2")
            throw new InvalidDataException("Not a GST2 ctex");

        var width      = (int)BitConverter.ToUInt32(data, 8);
        var height     = (int)BitConverter.ToUInt32(data, 12);
        var dataFormat = BitConverter.ToUInt32(data, 36);

        const int hdr = 52;
        if (dataFormat == 2)
        {
            var size = (int)BitConverter.ToUInt32(data, hdr);
            using var ms = new System.IO.MemoryStream(data, hdr + 4, size);
            using var isImg = ISImage.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(ms);
            return ToSKBitmap(isImg);
        }
        else
        {
            var bc7Data  = new ReadOnlyMemory<byte>(data, hdr, data.Length - hdr);
            var decoder  = new BcDecoder();
            var pixels   = decoder.DecodeRaw(bc7Data.ToArray(), width, height, CompressionFormat.Bc7);
            var bytes    = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
            var bitmap   = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                bitmap.InstallPixels(bitmap.Info, handle.AddrOfPinnedObject(), bitmap.RowBytes);
                var safe = bitmap.Copy();
                bitmap.Dispose();
                return safe;
            }
            finally
            {
                handle.Free();
            }
        }
    }

    static SKBitmap ToSKBitmap(ISRgba32 img)
    {
        var bitmap = new SKBitmap(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    bitmap.SetPixel(x, y, new SKColor(p.R, p.G, p.B, p.A));
                }
            }
        });
        return bitmap;
    }
}

class SkiaTextureLoader : TextureLoader
{
    readonly SKBitmap _bitmap;
    public SkiaTextureLoader(SKBitmap bitmap) => _bitmap = bitmap;

    public void Load(AtlasPage page, string path)
    {
        page.rendererObject = _bitmap;
        page.width  = _bitmap.Width;
        page.height = _bitmap.Height;
    }

    public void Unload(object texture) { }
}
