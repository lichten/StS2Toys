using StS2Toys.Services;
using StS2Shared.Services;

namespace StS2Toys;

public partial class CardImageViewerForm : Form
{
    private static string? _portraitsDir;

    public CardImageViewerForm()
    {
        InitializeComponent();
    }

    public void ShowCard(string cardId, string? typeHint = null)
    {
        var oldImage = pictureBox.Image;

        var imagePath = FindCardImage(cardId, typeHint);
        Image? newImage;
        if (imagePath is not null)
        {
            newImage = Image.FromFile(imagePath);
        }
        else
        {
            // フォールバック: カードアトラスから取得（コピーして独立した所有権を確保）
            var atlasBmp = Services.CardAtlasService.GetCardBitmap(cardId);
            newImage = atlasBmp is not null ? new Bitmap(atlasBmp) : null;
        }

        pictureBox.Image = newImage;
        oldImage?.Dispose();
        pictureBox.Invalidate();
    }

    void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        if (pictureBox.Image == null)
            TextRenderer.DrawText(e.Graphics, "画像なし", pictureBox.Font,
                pictureBox.ClientRectangle, SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    internal static string? FindCardImage(string cardId, string? typeHint = null)
    {
        // サブディレクトリ/type 付きファイル名の対応は StS2Shared の CardImageService に一元化。
        var dir = GetPortraitsDir();
        if (dir is null) return null;
        var path = CardImageService.GetSourcePath(dir, cardId);
        return path is not null && File.Exists(path) ? path : null;
    }

    static string? GetPortraitsDir()
    {
        if (_portraitsDir != null) return _portraitsDir;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "extracted", "images", "card_portraits_png");
            if (Directory.Exists(candidate))
                return _portraitsDir = candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
