namespace StS2Capture.Capture;

/// <summary>
/// ゲームウィンドウのフレームを取得するキャプチャ手段の抽象。
/// WGC（GPU 描画対応・主）と GDI（フォールバック）を差し替え可能にする。
/// </summary>
public interface IFrameSource : IDisposable
{
    /// <summary>表示名（UI 表示・ログ用）。</summary>
    string Name { get; }

    /// <summary>
    /// 対象ウィンドウを捕捉して 1 フレームを取得する。失敗時は null。
    /// 返す Bitmap の所有権は呼び出し側（使用後 Dispose する）。
    /// </summary>
    Bitmap? CaptureFrame(IntPtr hwnd);
}
