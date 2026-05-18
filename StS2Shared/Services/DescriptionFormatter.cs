using System.Text.RegularExpressions;

namespace StS2Shared.Services;

public static class DescriptionFormatter
{
    static readonly Regex TagRegex      = new(@"\[/?[a-zA-Z]+\]", RegexOptions.Compiled);
    static readonly Regex TemplateRegex = new(@"\{([^}]+)\}",     RegexOptions.Compiled);
    static readonly Regex EnergyArgRegex = new(@"energyIcons\((\d+)\)", RegexOptions.Compiled);
    static readonly Regex AmountTemplate =
        new(@"\{Amount[^}]*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 引数なし版: card_stats.json が未整備の変数は [VarName] で表示（案C）
    public static string Clean(string raw, bool japanese = false) => Resolve(raw, null, japanese);

    // card_stats.json の値を渡すと実際の数値で置換する（案A）
    public static string Resolve(string raw, IReadOnlyDictionary<string, int>? stats, bool japanese = false, bool upgraded = false)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        raw = StripInCombat(raw);
        var s = TagRegex.Replace(raw, "");
        s = TemplateRegex.Replace(s, m =>
        {
            var r = ResolveTemplate(m.Groups[1].Value, stats, japanese, upgraded);
            if (IsEnergyWord(r, japanese))
            {
                if (m.Index > 0 && s[m.Index - 1] != ' ') r = " " + r;
                var end = m.Index + m.Length;
                if (end < s.Length && s[end] != ' ') r += " ";
            }
            return r;
        });
        return s.Trim();
    }

    static bool IsEnergyWord(string text, bool japanese) =>
        japanese ? text.EndsWith("エナジー") : text.EndsWith("Energy");

    // エンチャント説明文用（Amount テンプレートのみ数値置換、残りは Clean）
    public static string CleanWithAmount(string raw, int amount, bool japanese = false)
    {
        var replaced = AmountTemplate.Replace(raw, amount.ToString());
        return Clean(replaced, japanese);
    }

    // {InCombat:...|} ブロックをネスト深度で正確に除去
    static string StripInCombat(string s)
    {
        int start = s.IndexOf("{InCombat:", StringComparison.Ordinal);
        if (start < 0) return s;
        int pos = start + 1, depth = 1;
        while (pos < s.Length && depth > 0)
        {
            if (s[pos] == '{') depth++;
            else if (s[pos] == '}') depth--;
            pos++;
        }
        return s[..start] + StripInCombat(s[pos..]);
    }

    static string ResolveTemplate(string content, IReadOnlyDictionary<string, int>? stats, bool japanese, bool upgraded = false)
    {
        int colonIdx = content.IndexOf(':');
        var varName  = colonIdx >= 0 ? content[..colonIdx] : content;
        var rest     = colonIdx >= 0 ? content[(colonIdx + 1)..] : "";

        // {InCombat:...} — 除去済みのはずだが念のため
        if (varName == "InCombat") return "";

        // {IfUpgraded:show:UpgradedText|BaseText}
        if (varName == "IfUpgraded" && rest.StartsWith("show:", StringComparison.Ordinal))
        {
            var afterShow = rest[5..];
            int pipe = afterShow.IndexOf('|');
            if (upgraded) return pipe >= 0 ? afterShow[..pipe] : afterShow;
            return pipe >= 0 ? afterShow[(pipe + 1)..] : afterShow;
        }

        // {VarName:plural:singular|plural} → singular（値が不明なので単数形を返す）
        if (rest.StartsWith("plural:", StringComparison.Ordinal))
        {
            var afterPlural = rest[7..];
            // stats に値があれば正しい形を返す
            if (TryFindStat(stats, varName, out int count))
                return count == 1 ? afterPlural[..afterPlural.IndexOf('|')] : afterPlural[(afterPlural.IndexOf('|') + 1)..];
            int pipe = afterPlural.IndexOf('|');
            return pipe >= 0 ? afterPlural[..pipe] : afterPlural;
        }

        // {prefix:energyIcons(N)} → "Energy" / "エナジー"（N=1）or "N Energy" / "Nエナジー"（N>1）
        // N をそのまま返すと "0{...energyIcons(1)}" が "01" になるため
        var energyMatch = EnergyArgRegex.Match(rest);
        if (energyMatch.Success)
        {
            var n = int.Parse(energyMatch.Groups[1].Value);
            if (japanese) return n <= 1 ? "エナジー" : $"{n}エナジー";
            return n <= 1 ? "Energy" : $"{n} Energy";
        }

        // {VarName:diff()} など — stats を参照
        var (baseVal, upgVal) = FindStatPair(stats, varName);
        if (upgraded && upgVal.HasValue) return $"{upgVal}";
        if (baseVal.HasValue)
            return upgVal.HasValue ? $"{baseVal}({upgVal})" : $"{baseVal}";

        // 案C: 値不明の変数名を [VarName] で表示
        return $"[{varName}]";
    }

    // case-insensitive + 先頭アンダースコア除去でフィールド名を検索
    // "Power" サフィックスも除去してリトライ（例: DexterityPower → Dexterity）
    static bool TryFindStat(IReadOnlyDictionary<string, int>? stats, string varName, out int value)
    {
        value = 0;
        if (stats == null) return false;
        if (TryFindStatExact(stats, varName, out value)) return true;
        if (varName.EndsWith("Power", StringComparison.OrdinalIgnoreCase))
            return TryFindStatExact(stats, varName[..^5], out value);
        return false;
    }

    static bool TryFindStatExact(IReadOnlyDictionary<string, int> stats, string varName, out int value)
    {
        if (stats.TryGetValue(varName, out value)) return true;
        foreach (var (k, v) in stats)
        {
            if (string.Equals(k.TrimStart('_'), varName, StringComparison.OrdinalIgnoreCase))
            { value = v; return true; }
        }
        value = 0;
        return false;
    }

    // base + upgraded の両値を取得（diff() 表示用）
    // "Power" サフィックスも除去してリトライ（例: DexterityPower → Dexterity）
    static (int? Base, int? Upgraded) FindStatPair(IReadOnlyDictionary<string, int>? stats, string varName)
    {
        if (stats == null) return (null, null);
        var result = FindStatPairExact(stats, varName);
        if (result.Base.HasValue) return result;
        if (varName.EndsWith("Power", StringComparison.OrdinalIgnoreCase))
            return FindStatPairExact(stats, varName[..^5]);
        return result;
    }

    static (int? Base, int? Upgraded) FindStatPairExact(IReadOnlyDictionary<string, int> stats, string varName)
    {
        int? baseVal = null, upgVal = null;
        foreach (var (k, v) in stats)
        {
            var norm = k.TrimStart('_');
            if (string.Equals(norm, varName, StringComparison.OrdinalIgnoreCase))
                baseVal = v;
            else if (string.Equals(norm, "upgraded" + varName, StringComparison.OrdinalIgnoreCase))
                upgVal = v;
        }
        return (baseVal, upgVal);
    }
}
