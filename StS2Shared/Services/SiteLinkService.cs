namespace StS2Shared.Services;

/// <summary>
/// 情報ページ URL テンプレート。<see cref="Type"/> は対象種別
/// （card/relic/potion/monster/event/encounter/any）。<see cref="Template"/> は
/// トークン（{id}/{en}/{jp}/{idraw}/{idrawlower}/{cardclass}）を含む URL 文字列。
/// </summary>
public sealed record UrlTemplate(string Label, string Type, string Template);

/// <summary>
/// 検出エンティティ（種別＋ID）から、設定済み URL テンプレートを展開してリンクを作る。
/// トークン値は URL エンコードする（日本語名などをそのまま埋め込める）。
/// 名称・キャラ別ディレクトリ解決は StS2Shared の各サービス／SiteBuilder と同じ規則。
/// </summary>
public static class SiteLinkService
{
    public sealed record Link(string Label, string Url);

    static readonly HashSet<string> CardClasses =
        new(StringComparer.OrdinalIgnoreCase) { "ironclad", "silent", "defect", "necrobinder", "regent" };

    /// <summary>
    /// 指定エンティティに対し、種別が一致（または "any"）するテンプレートを展開したリンク一覧。
    /// </summary>
    public static IReadOnlyList<Link> BuildLinks(IEnumerable<UrlTemplate> templates, string kind, string id)
    {
        var t = ResolveTokens(kind, id);
        var links = new List<Link>();
        foreach (var tmpl in templates)
        {
            if (!string.IsNullOrWhiteSpace(tmpl.Type) &&
                !tmpl.Type.Equals("any", StringComparison.OrdinalIgnoreCase) &&
                !tmpl.Type.Equals(kind, StringComparison.OrdinalIgnoreCase))
                continue;
            var url = Expand(tmpl.Template, t);
            if (!string.IsNullOrWhiteSpace(url)) links.Add(new Link(tmpl.Label, url));
        }
        return links;
    }

    readonly record struct Tokens(
        string Id, string IdRaw, string IdRawLower, string En, string Jp, string CardClass);

    static Tokens ResolveTokens(string kind, string id)
    {
        string idRaw = id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;
        string en, jp;
        switch (kind?.ToLowerInvariant())
        {
            case "card": en = CardDatabaseService.GetName(id, false); jp = CardDatabaseService.GetName(id, true); break;
            case "relic": en = CardDatabaseService.GetRelicTitle(id, false); jp = CardDatabaseService.GetRelicTitle(id, true); break;
            case "event": en = CardDatabaseService.GetEventTitle(id, false); jp = CardDatabaseService.GetEventTitle(id, true); break;
            case "encounter": en = EncounterDatabaseService.GetEncounterName(id, false); jp = EncounterDatabaseService.GetEncounterName(id, true); break;
            case "potion": en = CardDatabaseService.GetPotionTitle(id, false); jp = CardDatabaseService.GetPotionTitle(id, true); break;
            default: en = idRaw; jp = idRaw; break; // monster は名称サービス未整備のため ID フォールバック
        }

        string cardClass = "";
        if (string.Equals(kind, "card", StringComparison.OrdinalIgnoreCase))
        {
            var c = CardDatabaseService.GetCardCharacter(id)?.ToLowerInvariant() ?? "";
            cardClass = CardClasses.Contains(c) ? c : "shared";
        }

        return new Tokens(id, idRaw, idRaw.ToLowerInvariant(), en, jp, cardClass);
    }

    static string Expand(string template, Tokens t)
    {
        // 長いトークンから置換（部分一致を避ける）。値は URL エンコード。
        return template
            .Replace("{idrawlower}", Enc(t.IdRawLower))
            .Replace("{idraw}", Enc(t.IdRaw))
            .Replace("{cardclass}", Enc(t.CardClass))
            .Replace("{id}", Enc(t.Id))
            .Replace("{en}", Enc(t.En))
            .Replace("{jp}", Enc(t.Jp));
    }

    static string Enc(string? s) => Uri.EscapeDataString(s ?? "");
}
