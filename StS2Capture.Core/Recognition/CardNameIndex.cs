using System.Text;
using StS2Shared.Services;

namespace StS2Capture.Recognition;

/// <summary>
/// 正規化したカード名（EN/JP）→ CardId の索引。OCR 結果のファジー照合に使う、
/// カード特定の中核。CardDatabaseService から全カード ID と名前を取得して構築する。
/// </summary>
public sealed class CardNameIndex
{
    public readonly record struct Match(string CardId, string CanonicalName, int Distance, double Confidence);

    // 正規化名 → CardId。EN/JP 双方を登録。
    readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    // ファジー照合用：(正規化名, CardId) の一覧（長さでバケット化）。
    readonly Dictionary<int, List<(string Norm, string CardId)>> _byLen = new();

    public static CardNameIndex Build()
    {
        var idx = new CardNameIndex();
        foreach (var id in CardDatabaseService.GetAllCardIds())
        {
            idx.Add(CardDatabaseService.GetName(id, japanese: false), id);
            idx.Add(CardDatabaseService.GetName(id, japanese: true), id);
        }
        return idx;
    }

    void Add(string name, string cardId)
    {
        var norm = Normalize(name);
        if (norm.Length < 3) return; // 短すぎる名前は誤検出源なので除外
        _exact[norm] = cardId;
        if (!_byLen.TryGetValue(norm.Length, out var list))
            _byLen[norm.Length] = list = new();
        list.Add((norm, cardId));
    }

    /// <summary>
    /// テキストに最も近いカードを返す（しきい値超過なら null）。
    /// </summary>
    public Match? FindBest(string text)
    {
        var norm = Normalize(text);
        if (norm.Length < 3) return null;

        if (_exact.TryGetValue(norm, out var exactId))
            return new Match(exactId, norm, 0, 1.0);

        // 近い長さのバケットのみ走査（編集距離の上限）。
        int maxDist = norm.Length <= 5 ? 1 : (int)Math.Floor(norm.Length * 0.2);
        if (maxDist <= 0) return null;

        Match? best = null;
        for (int len = norm.Length - maxDist; len <= norm.Length + maxDist; len++)
        {
            if (len < 3 || !_byLen.TryGetValue(len, out var list)) continue;
            foreach (var (cand, cardId) in list)
            {
                int d = Levenshtein(norm, cand, maxDist);
                if (d <= maxDist && (best is null || d < best.Value.Distance))
                {
                    double conf = 1.0 - (double)d / Math.Max(norm.Length, cand.Length);
                    best = new Match(cardId, cand, d, conf);
                    if (d == 0) return best;
                }
            }
        }
        return best;
    }

    /// <summary>小文字化し、英数字・かな・漢字以外（空白・記号）を除去する。</summary>
    static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>上限付き Levenshtein 距離（上限超過なら maxDist+1 を返す）。</summary>
    static int Levenshtein(string a, string b, int max)
    {
        int n = a.Length, m = b.Length;
        if (Math.Abs(n - m) > max) return max + 1;

        var prev = new int[m + 1];
        var cur = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;

        for (int i = 1; i <= n; i++)
        {
            cur[0] = i;
            int rowMin = cur[0];
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                if (cur[j] < rowMin) rowMin = cur[j];
            }
            if (rowMin > max) return max + 1; // 早期打ち切り
            (prev, cur) = (cur, prev);
        }
        return prev[m];
    }
}
