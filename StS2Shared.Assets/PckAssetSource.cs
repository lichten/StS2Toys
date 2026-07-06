using StS2Shared.Spine;

namespace StS2Shared.Assets;

/// <summary>
/// <see cref="PckReader"/> を <see cref="IAssetSource"/> として公開し、Spine 生成コードが
/// ユーザーの <c>.pck</c> から直接（中間ファイルを展開せず）読めるようにする。
/// パスは <c>res://</c> 無しのスラッシュ区切り論理パス（<see cref="PckReader"/> が内部で正規化する）。
/// </summary>
public sealed class PckAssetSource : IAssetSource
{
    readonly PckReader _pck;

    public PckAssetSource(PckReader pck) => _pck = pck;

    public bool Exists(string resPath) => _pck.Find(resPath) is not null;

    public byte[]? Read(string resPath) => _pck.TryRead(resPath, out var bytes) ? bytes : null;

    public IEnumerable<string> List(string resDir, string? suffix)
    {
        // 直下（サブフォルダを除く）のみを返す。EnumerateUnder は接頭辞一致なので階層で絞る。
        var prefix = resDir.TrimEnd('/') + "/";
        return _pck.EnumerateUnder(prefix)
            .Select(e => e.ResPath)
            .Where(p => !p.AsSpan(prefix.Length).Contains('/') &&
                        (suffix is null || p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }
}
