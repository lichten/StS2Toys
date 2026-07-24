using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StS2Shared.Services;
using StS2Shared.Spine;

// モンスター Spine アニメーションの GIF/PNG 生成（旧 StS2SiteBuilder の GenerateMonsterGifs を移設）。
// 出力先 tools/extracted/images/monsters/ は site3（slaythespire2.lichtenlab.com）が
// read-only マウントで直接参照するため、クリーン展開のたびに再生成が必要。
static class MonsterGifGenerator
{
    /// <summary>
    /// 全モンスター ID の idle アニメーションを {id}.gif / {id}.png として outDir へ描画する。
    /// mtime キャッシュ（入力 .tscn / skel.import / 静的 .ctex より出力が新しければスキップ）あり。
    /// GIF を持つ ID の集合を返す。
    /// </summary>
    public static HashSet<string> Generate(string toolsRoot, string outDir, Action<string> log)
    {
        Directory.CreateDirectory(outDir);
        var disk = new DiskAssetSource(toolsRoot);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int generated = 0, skipped = 0, failed = 0;

        // モンスター ID ごとに creature_visuals/{id}.tscn を解決して描画する。
        // tscn が無い ID は animations/monsters/{id}/ のフォルダにフォールバック。
        var ids = MonsterDatabaseService.GetAllMonsters().Select(m => m.DirName)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var id in ids)
        {
            var gifPath = Path.Combine(outDir, $"{id}.gif");
            var pngPath = Path.Combine(outDir, $"{id}.png");
            // creature_visuals/{id}.tscn → 無ければ animations/monsters/{id}/ にフォールバック（共有ロジック）
            CreatureVisual? cv = MonsterResolver.Resolve(disk, id);

            if (cv is null || cv.Kind == CreatureVisualKind.Invisible)
                continue; // 画像なし（? 表示）

            // キャッシュ判定: 入力（tscn / skel.import / 静的 ctex）より出力が新しければスキップ
            var stamp = new[]
            {
                disk.GetLastWriteTimeUtc($"scenes/creature_visuals/{id}.tscn"),
                disk.GetLastWriteTimeUtc(cv.SkelImport),
                disk.GetLastWriteTimeUtc(cv.StaticCtexPath),
            }.Max();
            bool spineCached = File.Exists(gifPath) && File.Exists(pngPath) &&
                               File.GetLastWriteTimeUtc(gifPath) >= stamp;
            bool staticCached = File.Exists(pngPath) &&
                                File.GetLastWriteTimeUtc(pngPath) >= stamp;
            if ((cv.Kind == CreatureVisualKind.Spine && spineCached) ||
                (cv.Kind == CreatureVisualKind.Static && staticCached))
            {
                if (File.Exists(gifPath)) result.Add(id);
                skipped++;
                continue;
            }

            const int w = 192, h = 192;
            try
            {
                if (cv.Kind == CreatureVisualKind.Static)
                {
                    var ctex = disk.Read(cv.StaticCtexPath!)!;
                    using var src = SpineLoader.LoadCtexAsSKBitmap(ctex);
                    using var fitted = SpineImaging.FitBitmap(src, w, h);
                    SpineImaging.SavePng(fitted, pngPath, w, h);
                    generated++;
                    continue;
                }

                // Spine
                if (cv.SkelImport is null || cv.AtlasImport is null) { failed++; continue; }

                MonsterData? monsterData = null;
                try
                {
                    monsterData = SpineLoader.LoadFromImports(cv.SkelImport!, cv.AtlasImport!, disk);

                    // アニメ選択: tscn 指定 → idle 系 → 先頭。無ければ静止 setup pose。（共有ロジック）
                    string? animName = SpineImaging.PickAnimationName(cv, monsterData);

                    float duration = 1f;
                    int frameCount = 1;
                    if (animName is not null)
                    {
                        var anim = monsterData.SkeletonData.FindAnimation(animName)!;
                        duration = anim.Duration > 0 ? anim.Duration : 1f;
                        frameCount = Math.Min(50, Math.Max(2, (int)(duration * 10)));
                    }

                    using var gifImage = new Image<Rgba32>(w, h);
                    gifImage.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;

                    for (int i = 0; i < frameCount; i++)
                    {
                        float time = (frameCount <= 1) ? 0f : duration * i / (frameCount - 1);
                        using var bmp = SpineRenderer.Render(monsterData, animName, time, w, h, cv.Skin, cv.Tint);
                        var pixels = bmp.Pixels;

                        if (i == 0)
                        {
                            for (int pi = 0; pi < pixels.Length; pi++)
                            {
                                var p = pixels[pi];
                                gifImage.Frames.RootFrame[pi % w, pi / w] = new Rgba32(p.Red, p.Green, p.Blue, p.Alpha);
                            }
                        }
                        else
                        {
                            using var frameImg = new Image<Rgba32>(w, h);
                            frameImg.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;
                            for (int pi = 0; pi < pixels.Length; pi++)
                            {
                                var p = pixels[pi];
                                frameImg.Frames.RootFrame[pi % w, pi / w] = new Rgba32(p.Red, p.Green, p.Blue, p.Alpha);
                            }
                            gifImage.Frames.AddFrame(frameImg.Frames.RootFrame);
                        }
                    }

                    gifImage.Metadata.GetGifMetadata().RepeatCount = 0;
                    gifImage.SaveAsGif(gifPath);
                    using var pngFrame = gifImage.Frames.CloneFrame(0);
                    pngFrame.SaveAsPng(pngPath);

                    result.Add(id);
                    generated++;
                }
                finally
                {
                    monsterData?.Texture.Dispose();
                }
            }
            catch (Exception ex)
            {
                log($"GIF 生成エラー: {id}: {ex.Message}");
                failed++;
            }
        }

        log($"モンスター GIF: {generated} 件生成 / {skipped} 件スキップ / {failed} 件エラー");
        return result;
    }
}
