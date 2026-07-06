namespace StS2Shared.Spine;

/// <summary>
/// モンスター 1 体の先頭フレームを PNG にレンダリングする（配布セットアップ用の軽量 API）。
/// GIF は生成しない＝StS2Toys の表示（.png のみ参照）に必要十分で高速。
/// </summary>
public static class MonsterPngRenderer
{
    /// <summary>
    /// <paramref name="monsterId"/>（dir 名）の先頭フレームを size×size の PNG バイト列で返す。
    /// 画像なし（Invisible）・解決不能なら null。描画時の例外は呼び出し側で扱う。
    /// </summary>
    public static byte[]? TryRenderFirstFramePng(IAssetSource src, string monsterId, int size = 192)
    {
        var cv = MonsterResolver.Resolve(src, monsterId);
        if (cv is null || cv.Kind == CreatureVisualKind.Invisible) return null;

        if (cv.Kind == CreatureVisualKind.Static)
        {
            if (cv.StaticCtexPath is null) return null;
            var ctex = src.Read(cv.StaticCtexPath);
            if (ctex is null) return null;
            using var s = SpineLoader.LoadCtexAsSKBitmap(ctex);
            using var fitted = SpineImaging.FitBitmap(s, size, size);
            return SpineImaging.EncodePng(fitted, size, size);
        }

        // Spine
        if (cv.SkelImport is null || cv.AtlasImport is null) return null;
        MonsterData? data = null;
        try
        {
            data = SpineLoader.LoadFromImports(cv.SkelImport, cv.AtlasImport, src);
            var anim = SpineImaging.PickAnimationName(cv, data);
            using var bmp = SpineRenderer.Render(data, anim, 0f, size, size, cv.Skin, cv.Tint);
            return SpineImaging.EncodePng(bmp, size, size);
        }
        finally
        {
            data?.Texture.Dispose();
        }
    }
}
