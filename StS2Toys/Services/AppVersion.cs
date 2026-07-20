using System.Reflection;

namespace StS2Toys.Services;

/// <summary>
/// 自身（StS2Toys）のバージョン表示文字列。csproj の <c>&lt;Version&gt;</c>、
/// リリースビルドでは release.yml の <c>-p:Version=</c>（git タグ由来）が元になる。
/// </summary>
static class AppVersion
{
    /// <summary>表示用のバージョン（例 "0.1.0"）。取得できなければ "?"。</summary>
    public static string Display { get; } = Resolve();

    static string Resolve()
    {
        var asm = Assembly.GetEntryAssembly();

        // AssemblyInformationalVersion は SourceLink により "0.1.0+<40桁SHA>" になるため、
        // '+' 以降（ビルドメタデータ）を落とす。SHA をそのまま出すと利用者には読めない。
        var info = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return asm?.GetName().Version?.ToString(3) ?? "?";
    }
}
