using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StS2Shared.Assets;

/// <summary>Slay the Spire 2 のインストールを検出した結果。</summary>
/// <param name="GameDir">ゲームフォルダ（<c>...\common\Slay the Spire 2</c>）。</param>
/// <param name="PckPath">ゲームアセットの <c>.pck</c>（<c>SlayTheSpire2.pck</c>）。</param>
/// <param name="Version">
/// <c>release_info.json</c> の <c>version</c>（例 "v0.108.0"）。読めなければ null。</param>
public sealed record Sts2Install(string GameDir, string PckPath, string? Version);

/// <summary>
/// Steam のインストール位置とライブラリを辿り、Slay the Spire 2 の <c>.pck</c> を自動検出する。
///
/// 検出順序：レジストリ <c>HKCU\Software\Valve\Steam</c> の <c>SteamPath</c>（Windows のみ）→ 既定
/// <c>C:\Program Files (x86)\Steam</c> → 各ドライブの <c>steamapps\libraryfolders.vdf</c> を解析して
/// 全ライブラリフォルダを列挙 → 各ライブラリの <c>steamapps\common\Slay the Spire 2\SlayTheSpire2.pck</c> を探す。
/// 見つからなければ null（利用側で手動フォルダ選択にフォールバックする想定）。
/// </summary>
public static class SteamLocator
{
    const string GameFolderName = "Slay the Spire 2";
    const string PckFileName = "SlayTheSpire2.pck";

    /// <summary>Slay the Spire 2 を自動検出する。見つからなければ null。</summary>
    public static Sts2Install? Locate()
    {
        foreach (var steamRoot in EnumerateSteamRoots())
        {
            foreach (var library in EnumerateLibraries(steamRoot))
            {
                var gameDir = Path.Combine(library, "steamapps", "common", GameFolderName);
                var pck = Path.Combine(gameDir, PckFileName);
                if (File.Exists(pck))
                    return new Sts2Install(gameDir, pck, ReadVersion(gameDir));
            }
        }
        return null;
    }

    /// <summary>
    /// 明示的に指定されたパス（<c>.pck</c> 直接指定、またはゲームフォルダ）から <see cref="Sts2Install"/> を構築する。
    /// 手動フォルダ選択のフォールバック用。妥当でなければ null。
    /// </summary>
    public static Sts2Install? FromPath(string pckOrGameDir)
    {
        if (File.Exists(pckOrGameDir) &&
            Path.GetFileName(pckOrGameDir).Equals(PckFileName, StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(pckOrGameDir)!;
            return new Sts2Install(dir, pckOrGameDir, ReadVersion(dir));
        }
        if (Directory.Exists(pckOrGameDir))
        {
            var pck = Path.Combine(pckOrGameDir, PckFileName);
            if (File.Exists(pck))
                return new Sts2Install(pckOrGameDir, pck, ReadVersion(pckOrGameDir));
        }
        return null;
    }

    /// <summary>ゲームフォルダの <c>release_info.json</c> から <c>version</c> を読む。読めなければ null。</summary>
    public static string? ReadVersion(string gameDir)
    {
        try
        {
            var path = Path.Combine(gameDir, "release_info.json");
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch { return null; }
    }

    static List<string> EnumerateSteamRoots()
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? p)
        {
            if (!string.IsNullOrEmpty(p) && seen.Add(p) && Directory.Exists(p))
                roots.Add(p);
        }

        if (OperatingSystem.IsWindows())
            Add(ReadSteamPathFromRegistry());

        Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));

        return roots;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    static string? ReadSteamPathFromRegistry()
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var sub in new[] { @"Software\Valve\Steam", @"Software\Wow6432Node\Valve\Steam" })
            {
                try
                {
                    using var key = hive.OpenSubKey(sub);
                    if (key?.GetValue("SteamPath") is string p && !string.IsNullOrEmpty(p))
                        return p;
                }
                catch { /* レジストリ読み取り不可は無視して次候補へ */ }
            }
        }
        return null;
    }

    static IEnumerable<string> EnumerateLibraries(string steamRoot)
    {
        // steamRoot 自身も 1 ライブラリ。
        yield return steamRoot;

        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        string text;
        try { text = File.ReadAllText(vdf); }
        catch { yield break; }

        // "path"		"D:\\SteamLibrary" 形式の全 path 値を拾う（エスケープされた \\ を復元）。
        foreach (Match m in Regex.Matches(text, @"""path""\s*""([^""]+)"""))
        {
            var path = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(path))
                yield return path;
        }
    }
}
