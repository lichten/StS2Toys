using StS2Toys.Services;

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
        var dir = GetPortraitsDir();
        if (dir is null) return null;

        var raw = cardId.Contains('.') ? cardId[(cardId.LastIndexOf('.') + 1)..] : cardId;
        var baseName = raw.ToLowerInvariant();

        var found = SearchPortraitsDir(dir, baseName + ".png");
        if (found is not null) return found;

        // タイプ別ファイル名のフォールバック（例: mad_science_skill.png）
        var type = (typeHint ?? Services.CardDatabaseService.GetCardType(cardId)).ToLowerInvariant();
        if (!string.IsNullOrEmpty(type))
            found = SearchPortraitsDir(dir, baseName + "_" + type + ".png");

        return found;
    }

    static string? SearchPortraitsDir(string dir, string filename)
    {
        foreach (var subdir in Directory.GetDirectories(dir))
        {
            var path = Path.Combine(subdir, filename);
            if (File.Exists(path)) return path;
        }
        var rootPath = Path.Combine(dir, filename);
        return File.Exists(rootPath) ? rootPath : null;
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
