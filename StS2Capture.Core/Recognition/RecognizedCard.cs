namespace StS2Capture.Recognition;

/// <summary>
/// 1 フレームから認識されたカード 1 件。
/// </summary>
/// <param name="CardId">カード ID（例 "CARD.STRIKE"）。</param>
/// <param name="MatchedText">照合の元になった画面上テキスト（OCR の場合）。</param>
/// <param name="Region">フレーム内での検出領域。</param>
/// <param name="Confidence">0..1 の確信度。</param>
/// <param name="Recognizer">どの認識器が出したか（"OCR" / "Template"）。</param>
public readonly record struct RecognizedCard(
    string CardId,
    string MatchedText,
    Rectangle Region,
    double Confidence,
    string Recognizer);
