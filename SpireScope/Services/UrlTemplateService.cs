using System.Text.Json;
using StS2Shared.Services;

namespace SpireScope.Services;

/// <summary>
/// 情報ページ URL テンプレートの永続化（ライブキャプチャの検出結果リンク用）。
/// ウィンドウ設定とは独立した url_templates.json に保存し、互いに上書きしないようにする。
/// </summary>
static class UrlTemplateService
{
    static readonly string Path_ = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpireScope", "url_templates.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<UrlTemplate> Load()
    {
        if (!File.Exists(Path_)) return Defaults();
        try
        {
            using var stream = File.OpenRead(Path_);
            return JsonSerializer.Deserialize<List<UrlTemplate>>(stream, Options) ?? Defaults();
        }
        catch
        {
            return Defaults();
        }
    }

    public static void Save(List<UrlTemplate> templates)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_)!);
        using var stream = File.Create(Path_);
        JsonSerializer.Serialize(stream, templates, Options);
    }

    /// <summary>初期テンプレート（外部 Wiki のみ。旧自前サイトのテンプレートはサイト廃止に伴い撤去）。</summary>
    public static List<UrlTemplate> Defaults() => new()
    {
        new("Wiki", "any", "https://wikiwiki.jp/sts2/{jp}"),
    };
}
