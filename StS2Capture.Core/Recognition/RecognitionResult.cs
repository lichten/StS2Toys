namespace StS2Capture.Recognition;

/// <summary>
/// OCR が読み取った 1 行分の診断情報。カード名に照合できた場合は
/// <see cref="MatchedCardId"/> 等を併記する（できなければ null）。
/// </summary>
/// <param name="Text">画面上で読み取った生テキスト。</param>
/// <param name="Region">フレーム（原寸）内での領域。</param>
/// <param name="MatchedCardId">照合できたカード ID（無ければ null）。</param>
/// <param name="Distance">照合の編集距離（無ければ null）。</param>
/// <param name="Confidence">照合の確信度 0..1（無ければ null）。</param>
/// <param name="Source">"frame"（全画面 OCR）／"title"（タイトル帯の局所 OCR）。</param>
public readonly record struct OcrTextSpan(
    string Text,
    Rectangle Region,
    string? MatchedCardId,
    int? Distance,
    double? Confidence,
    string Source);

/// <summary>
/// 認識器 1 回分の結果。特定できたカード・診断用 OCR テキスト行・
/// 検出カード矩形・タイトル帯候補矩形を持つ。
/// </summary>
public sealed record RecognitionResult(
    IReadOnlyList<RecognizedCard> Cards,
    IReadOnlyList<OcrTextSpan> TextSpans,
    IReadOnlyList<Rectangle> CardBoxes,
    IReadOnlyList<Rectangle> TitleBands)
{
    public static readonly RecognitionResult Empty = new(
        Array.Empty<RecognizedCard>(), Array.Empty<OcrTextSpan>(),
        Array.Empty<Rectangle>(), Array.Empty<Rectangle>());
}
