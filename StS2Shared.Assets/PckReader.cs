namespace StS2Shared.Assets;

/// <summary>PCK 内の 1 ファイルのインデックスエントリ。</summary>
/// <param name="ResPath">正規化した論理パス（<c>res://</c> 接頭辞を除いた例 "images/atlases/relic_atlas.tpsheet"）。</param>
/// <param name="Offset">ファイル本体の絶対オフセット（<c>file_base</c> 適用済み）。</param>
/// <param name="Size">ファイルサイズ（バイト）。</param>
/// <param name="Encrypted">このエントリが暗号化されているか。</param>
public sealed record PckEntry(string ResPath, long Offset, long Size, bool Encrypted);

/// <summary>
/// Godot エンジンの PCK（<c>.pck</c>）を読み、必要なファイルだけを選択的に抽出する自前リーダー。
///
/// フォーマットはエンジンソース（<c>core/io/file_access_pack.cpp</c>）に基づく。フォーマットバージョン 2 / 3 に対応：
/// <list type="bullet">
///   <item>ヘッダ: magic "GDPC" + format_version + (major,minor,patch) + pack_flags + file_base(u64)。</item>
///   <item>V3+ はヘッダ直後に dir_offset(u64) を持ち、ディレクトリはファイル末尾に置かれる。V2 はヘッダの
///     予約 16×u32 の直後にディレクトリが続く。</item>
///   <item>ディレクトリ: file_count(u32) と、各ファイルの path_len(u32)+path+offset(u64)+size(u64)+md5(16)+flags(u32)。</item>
///   <item><c>pack_flags</c> の <c>PACK_REL_FILEBASE</c>(1&lt;&lt;1) が立つ場合、各 offset は <c>file_base</c> 相対。</item>
/// </list>
/// ディレクトリ暗号化（<c>PACK_DIR_ENCRYPTED</c>）は非対応で例外を投げる。巨大な <c>.pck</c> を全読みせず、
/// ディレクトリのみメモリに載せ、<see cref="Read"/> 時に該当範囲だけをシークして読む。
/// </summary>
public sealed class PckReader : IDisposable
{
    const uint Magic = 0x43504447; // "GDPC" (little-endian の順で 'G','D','P','C')
    const uint PackDirEncrypted = 1 << 0;
    const uint PackRelFilebase  = 1 << 1;
    const uint FileFlagEncrypted = 1 << 0;

    readonly FileStream _stream;
    readonly BinaryReader _reader;
    readonly Dictionary<string, PckEntry> _index;

    /// <summary>PCK フォーマットバージョン（2 または 3）。</summary>
    public int FormatVersion { get; }

    /// <summary>Godot エンジンバージョン（major, minor, patch）。</summary>
    public (int Major, int Minor, int Patch) EngineVersion { get; }

    /// <summary>全インデックスエントリ（正規化済み <c>ResPath</c> でキー付け）。</summary>
    public IReadOnlyCollection<PckEntry> Index => _index.Values;

    public PckReader(string pckPath)
    {
        _stream = new FileStream(pckPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _reader = new BinaryReader(_stream);

        var magic = _reader.ReadUInt32();
        if (magic != Magic)
            throw new InvalidDataException(
                $"PCK マジックが不正です（GDPC ではありません）: 0x{magic:X8}。埋め込み PCK やパッチ形式は非対応です。");

        FormatVersion = _reader.ReadInt32();
        if (FormatVersion is not (2 or 3))
            throw new NotSupportedException(
                $"未対応の PCK フォーマットバージョン {FormatVersion} です（対応: 2, 3）。ゲーム更新でフォーマットが変わった可能性があります。");

        EngineVersion = (_reader.ReadInt32(), _reader.ReadInt32(), _reader.ReadInt32());

        uint packFlags = _reader.ReadUInt32();
        long fileBase  = _reader.ReadInt64();

        if ((packFlags & PackDirEncrypted) != 0)
            throw new NotSupportedException("ディレクトリが暗号化された PCK は非対応です。");

        bool relFilebase = (packFlags & PackRelFilebase) != 0;

        if (FormatVersion >= 3)
        {
            long dirOffset = _reader.ReadInt64();
            _stream.Seek(dirOffset, SeekOrigin.Begin);
        }
        else
        {
            // V2: 予約 16×u32 を読み飛ばすとディレクトリ。
            _stream.Seek(16 * sizeof(uint), SeekOrigin.Current);
        }

        _index = ReadDirectory(relFilebase ? fileBase : 0);
    }

    Dictionary<string, PckEntry> ReadDirectory(long baseOffset)
    {
        int fileCount = _reader.ReadInt32();
        var index = new Dictionary<string, PckEntry>(fileCount, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < fileCount; i++)
        {
            int pathLen = _reader.ReadInt32();          // 4 バイト境界へパディング済みの長さ
            var pathBytes = _reader.ReadBytes(pathLen);
            var resPath = System.Text.Encoding.UTF8.GetString(pathBytes).TrimEnd('\0');

            long offset = _reader.ReadInt64() + baseOffset;
            long size   = _reader.ReadInt64();
            _reader.ReadBytes(16);                       // md5（未使用）
            uint flags  = _reader.ReadUInt32();

            var normalized = Normalize(resPath);
            index[normalized] = new PckEntry(normalized, offset, size, (flags & FileFlagEncrypted) != 0);
        }

        return index;
    }

    /// <summary>正規化済み論理パス（<c>res://</c> 無し・スラッシュ区切り）でエントリを引く。無ければ null。</summary>
    public PckEntry? Find(string resPath) =>
        _index.TryGetValue(Normalize(resPath), out var e) ? e : null;

    /// <summary>指定パスのファイル本体を読む。存在しなければ例外。</summary>
    public byte[] Read(string resPath)
        => TryRead(resPath, out var bytes) ? bytes : throw new FileNotFoundException($"PCK 内に見つかりません: {resPath}");

    /// <summary>指定パスのファイル本体を読む。存在すれば true。</summary>
    public bool TryRead(string resPath, out byte[] bytes)
    {
        var entry = Find(resPath);
        if (entry is null)
        {
            bytes = [];
            return false;
        }
        if (entry.Encrypted)
            throw new NotSupportedException($"暗号化されたエントリは非対応です: {entry.ResPath}");

        _stream.Seek(entry.Offset, SeekOrigin.Begin);
        bytes = _reader.ReadBytes((int)entry.Size);
        return true;
    }

    /// <summary>指定の接頭辞（正規化済み論理パス）で始まる全エントリを列挙する。</summary>
    public IEnumerable<PckEntry> EnumerateUnder(string resPrefix)
    {
        var prefix = Normalize(resPrefix);
        return _index.Values.Where(e => e.ResPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    static string Normalize(string resPath)
    {
        var p = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath[6..] : resPath;
        return p.Replace('\\', '/').TrimStart('/');
    }

    public void Dispose()
    {
        _reader.Dispose();
        _stream.Dispose();
    }
}
