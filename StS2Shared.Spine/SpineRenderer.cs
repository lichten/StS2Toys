using SkiaSharp;
using Spine;

namespace StS2Shared.Spine;

public static class SpineRenderer
{
    public static SKBitmap Render(MonsterData data, string? animationName, float time, int width, int height,
        string? skin = null, (float R, float G, float B, float A)? tint = null)
    {
        var skeleton = new Skeleton(data.SkeletonData);
        if (!string.IsNullOrEmpty(skin) && data.SkeletonData.FindSkin(skin) != null)
            skeleton.SetSkin(skin);
        skeleton.SetToSetupPose();

        if (!string.IsNullOrEmpty(animationName) && data.SkeletonData.FindAnimation(animationName) != null)
        {
            var stateData = new AnimationStateData(data.SkeletonData);
            var state = new AnimationState(stateData);
            state.SetAnimation(0, animationName, true);
            state.Update(time);
            state.Apply(skeleton);
        }
        skeleton.UpdateWorldTransform(Skeleton.Physics.Update);

        var bounds = ComputeBounds(skeleton);

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(30, 30, 35));

        float bw = bounds.Width + 40, bh = bounds.Height + 40;
        float scale = (bw > 0 && bh > 0) ? Math.Min(width / bw, height / bh) : 1f;

        canvas.Save();
        canvas.Translate(width / 2f, height / 2f);
        canvas.Scale(scale, -scale);
        canvas.Translate(-bounds.MidX, -bounds.MidY);

        using var shader = SKShader.CreateBitmap(
            data.Texture, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        var (tr, tg, tb, ta) = tint ?? (1f, 1f, 1f, 1f);
        DrawSkeleton(canvas, skeleton, shader, data.Texture.Width, data.Texture.Height, tr, tg, tb, ta);

        canvas.Restore();
        return bitmap;
    }

    static void DrawSkeleton(SKCanvas canvas, Skeleton skeleton, SKShader shader, int texW, int texH,
        float tr, float tg, float tb, float ta)
    {
        using var paint = new SKPaint { IsAntialias = true };
        var regionVerts = new float[8];
        var drawOrder = skeleton.DrawOrder;

        for (int i = 0, n = drawOrder.Count; i < n; i++)
        {
            var slot = drawOrder.Items[i];
            if (slot.Attachment == null || slot.A == 0) continue;

            var blend = slot.Data.BlendMode switch
            {
                BlendMode.Additive => SKBlendMode.Plus,
                BlendMode.Multiply => SKBlendMode.Multiply,
                _ => SKBlendMode.SrcOver
            };

            if (slot.Attachment is RegionAttachment region)
            {
                region.ComputeWorldVertices(slot, regionVerts, 0, 2);
                DrawQuad(canvas, paint, shader, regionVerts, region.UVs,
                    texW, texH, slot.R * tr, slot.G * tg, slot.B * tb, slot.A * ta,
                    region.R, region.G, region.B, region.A, blend);
            }
            else if (slot.Attachment is MeshAttachment mesh)
            {
                var mv = new float[mesh.WorldVerticesLength];
                mesh.ComputeWorldVertices(slot, 0, mesh.WorldVerticesLength, mv, 0, 2);
                DrawMesh(canvas, paint, shader, mv, mesh.UVs, mesh.Triangles,
                    texW, texH, slot.R * tr, slot.G * tg, slot.B * tb, slot.A * ta,
                    mesh.R, mesh.G, mesh.B, mesh.A, blend);
            }
        }
    }

    static void DrawQuad(SKCanvas canvas, SKPaint paint, SKShader shader,
        float[] verts, float[] uvs, int texW, int texH,
        float sr, float sg, float sb, float sa,
        float ar, float ag, float ab, float aa, SKBlendMode blend)
    {
        var positions = new SKPoint[4];
        var texCoords = new SKPoint[4];
        var colors = new SKColor[4];
        var color = ToSKColor(sr * ar, sg * ag, sb * ab, sa * aa);
        for (int j = 0; j < 4; j++)
        {
            positions[j] = new SKPoint(verts[j * 2], verts[j * 2 + 1]);
            texCoords[j] = new SKPoint(uvs[j * 2] * texW, uvs[j * 2 + 1] * texH);
            colors[j] = color;
        }
        ushort[] indices = [0, 1, 2, 2, 3, 0];

        paint.Shader = shader;
        paint.BlendMode = blend;
        var skVerts = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, texCoords, colors, indices);
        // Modulate: 頂点カラーでテクスチャを乗算（ティント・アルファ反映）
        canvas.DrawVertices(skVerts, SKBlendMode.Modulate, paint);
    }

    static void DrawMesh(SKCanvas canvas, SKPaint paint, SKShader shader,
        float[] verts, float[] uvs, int[] triangles, int texW, int texH,
        float sr, float sg, float sb, float sa,
        float ar, float ag, float ab, float aa, SKBlendMode blend)
    {
        int numVerts = verts.Length / 2;
        var positions = new SKPoint[numVerts];
        var texCoords = new SKPoint[numVerts];
        var colors = new SKColor[numVerts];
        var color = ToSKColor(sr * ar, sg * ag, sb * ab, sa * aa);
        for (int j = 0; j < numVerts; j++)
        {
            positions[j] = new SKPoint(verts[j * 2], verts[j * 2 + 1]);
            texCoords[j] = new SKPoint(uvs[j * 2] * texW, uvs[j * 2 + 1] * texH);
            colors[j] = color;
        }
        var indices = Array.ConvertAll(triangles, t => (ushort)t);

        paint.Shader = shader;
        paint.BlendMode = blend;
        var skVerts = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, texCoords, colors, indices);
        canvas.DrawVertices(skVerts, SKBlendMode.Modulate, paint);
    }

    static SKRect ComputeBounds(Skeleton skeleton)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        var verts = new float[8];
        var drawOrder = skeleton.DrawOrder;

        for (int i = 0, n = drawOrder.Count; i < n; i++)
        {
            var slot = drawOrder.Items[i];
            if (slot.Attachment is RegionAttachment region)
            {
                region.ComputeWorldVertices(slot, verts, 0, 2);
                for (int j = 0; j < 4; j++) AccumBounds(verts[j * 2], verts[j * 2 + 1], ref minX, ref minY, ref maxX, ref maxY);
            }
            else if (slot.Attachment is MeshAttachment mesh)
            {
                var mv = new float[mesh.WorldVerticesLength];
                mesh.ComputeWorldVertices(slot, 0, mesh.WorldVerticesLength, mv, 0, 2);
                for (int j = 0; j < mv.Length / 2; j++) AccumBounds(mv[j * 2], mv[j * 2 + 1], ref minX, ref minY, ref maxX, ref maxY);
            }
        }

        return minX == float.MaxValue
            ? new SKRect(-100, -100, 100, 100)
            : new SKRect(minX, minY, maxX, maxY);
    }

    static void AccumBounds(float x, float y, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
    }

    static SKColor ToSKColor(float r, float g, float b, float a) =>
        new((byte)(Math.Clamp(r, 0, 1) * 255),
            (byte)(Math.Clamp(g, 0, 1) * 255),
            (byte)(Math.Clamp(b, 0, 1) * 255),
            (byte)(Math.Clamp(a, 0, 1) * 255));
}
