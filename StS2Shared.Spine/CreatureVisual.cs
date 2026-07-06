using System.Globalization;
using System.Text.RegularExpressions;

namespace StS2Shared.Spine;

public enum CreatureVisualKind { Spine, Static, Invisible }

/// <summary>
/// scenes/creature_visuals/{id}.tscn から解決したモンスターの描画指定。
/// Spine = リグ＋スキン＋アニメ＋ティント、Static = 静的テクスチャ、Invisible = 非表示（画像なし）。
/// パス（SkelImport / AtlasImport / StaticCtexPath）は論理パス（res:// 無し・スラッシュ区切り）。
/// </summary>
public record CreatureVisual(
    CreatureVisualKind Kind,
    string? SkelImport = null,
    string? AtlasImport = null,
    string? Skin = null,
    string? Animation = null,
    (float R, float G, float B, float A)? Tint = null,
    string? StaticCtexPath = null);

/// <summary>モンスター ID から描画指定 <see cref="CreatureVisual"/> を解決する。</summary>
public static class MonsterResolver
{
    /// <summary>
    /// creature_visuals/{id}.tscn を解決。無ければ animations/monsters/{id}/ の
    /// *.skel.import / *.atlas.import にフォールバック。いずれも無ければ null。
    /// </summary>
    public static CreatureVisual? Resolve(IAssetSource src, string id)
    {
        var tscn = $"scenes/creature_visuals/{id}.tscn";
        if (src.Exists(tscn))
        {
            var cv = CreatureVisualParser.Parse(src, tscn);
            if (cv is not null) return cv;
        }

        var folder = $"animations/monsters/{id}";
        var skel  = src.List(folder, ".skel.import").FirstOrDefault();
        var atlas = src.List(folder, ".atlas.import").FirstOrDefault();
        if (skel is not null && atlas is not null)
            return new CreatureVisual(CreatureVisualKind.Spine, skel, atlas);

        return null;
    }
}

public static class CreatureVisualParser
{
    static readonly Regex ExtResRegex = new(
        @"\[ext_resource\s+type=""(?<type>[^""]+)""[^\]]*?path=""res://(?<path>[^""]+)""[^\]]*?id=""(?<id>[^""]+)""\]",
        RegexOptions.Compiled);

    /// <summary>tscn（論理パス）を解析。Spine/Static/Invisible いずれかを返す。判別不能なら null。</summary>
    public static CreatureVisual? Parse(IAssetSource src, string tscnResPath)
    {
        var bytes = src.Read(tscnResPath);
        if (bytes is null) return null;
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        var ext = new Dictionary<string, (string Type, string Path)>(StringComparer.Ordinal);
        foreach (Match m in ExtResRegex.Matches(text))
            ext[m.Groups["id"].Value] = (m.Groups["type"].Value, m.Groups["path"].Value);

        // 1) Spine: skeleton_data_res を持つノード
        foreach (var block in SplitNodeBlocks(text))
        {
            var sm = Regex.Match(block, @"skeleton_data_res\s*=\s*ExtResource\(""([^""]+)""\)");
            if (!sm.Success || !ext.TryGetValue(sm.Groups[1].Value, out var tres)) continue;

            if (!ResolveTres(src, tres.Path, out var skelImport, out var atlasImport))
                return null;

            var skin = Match1(block, @"preview_skin\s*=\s*""([^""]*)""");
            var anim = Match1(block, @"preview_animation\s*=\s*""([^""]*)""");

            (float, float, float, float)? tint = null;
            var mod = Regex.Match(block,
                @"modulate\s*=\s*Color\(\s*([-0-9.eE]+)\s*,\s*([-0-9.eE]+)\s*,\s*([-0-9.eE]+)\s*,\s*([-0-9.eE]+)\s*\)");
            if (mod.Success)
                tint = (F(mod, 1), F(mod, 2), F(mod, 3), F(mod, 4));

            return new CreatureVisual(CreatureVisualKind.Spine, skelImport, atlasImport,
                string.IsNullOrEmpty(skin) ? null : skin,
                string.IsNullOrEmpty(anim) ? null : anim, tint);
        }

        // 2) Static / Invisible: Sprite2D の Visuals ノード
        foreach (var block in SplitNodeBlocks(text))
        {
            if (!block.Contains("type=\"Sprite2D\"")) continue;

            var tm = Regex.Match(block, @"texture\s*=\s*ExtResource\(""([^""]+)""\)");
            if (tm.Success && ext.TryGetValue(tm.Groups[1].Value, out var tex)
                && tex.Path.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            {
                var importPath = tex.Path + ".import";
                if (src.Exists(importPath))
                    return new CreatureVisual(CreatureVisualKind.Static,
                        StaticCtexPath: SpineLoader.ResolveImportPath(src, importPath));
            }

            if (Regex.IsMatch(block, @"\bvisible\s*=\s*false\b"))
                return new CreatureVisual(CreatureVisualKind.Invisible);
        }

        return null;
    }

    /// <summary>_skel_data.tres（論理パス）から atlas(.atlas) と skeleton(.skel) を引き、対応する .import の論理パスを返す。</summary>
    static bool ResolveTres(IAssetSource src, string tresPath, out string skelImport, out string atlasImport)
    {
        skelImport = atlasImport = "";
        var bytes = src.Read(tresPath);
        if (bytes is null) return false;

        string? atlas = null, skel = null;
        foreach (Match m in ExtResRegex.Matches(System.Text.Encoding.UTF8.GetString(bytes)))
        {
            var type = m.Groups["type"].Value;
            if (type == "SpineAtlasResource") atlas = m.Groups["path"].Value;
            else if (type == "SpineSkeletonFileResource") skel = m.Groups["path"].Value;
        }
        if (atlas is null || skel is null) return false;

        atlasImport = atlas + ".import";
        skelImport  = skel + ".import";
        return src.Exists(atlasImport) && src.Exists(skelImport);
    }

    static IEnumerable<string> SplitNodeBlocks(string text)
    {
        var idx = text.IndexOf("[node ", StringComparison.Ordinal);
        if (idx < 0) yield break;
        foreach (var part in Regex.Split(text[idx..], @"(?=\[node )"))
            if (part.StartsWith("[node ", StringComparison.Ordinal))
                yield return part;
    }

    static string Match1(string s, string pattern)
    {
        var m = Regex.Match(s, pattern);
        return m.Success ? m.Groups[1].Value : "";
    }

    static float F(Match m, int i) => float.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
}
