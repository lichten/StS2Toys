namespace StS2Capture.Recognition;

/// <summary>
/// フレーム画像から表示中のカードを特定する認識器。
/// OCR 実装とテンプレートマッチング実装を差し替えて比較できるようにする。
/// </summary>
public interface ICardRecognizer
{
    /// <summary>表示名（"OCR" / "Template"）。</summary>
    string Name { get; }

    /// <summary>
    /// カード矩形検出に使う枠色プロファイル（キャラ別）。CaptureLoop が現在キャラに応じて設定する。
    /// 内部の <see cref="CardRegionDetector"/> へ転送する。
    /// </summary>
    FrameColorProfile FrameProfile { get; set; }

    /// <summary>
    /// フレームから検出したカード（重複排除済み）と、診断用の OCR テキスト行を返す。
    /// </summary>
    RecognitionResult Recognize(Bitmap frame);
}
