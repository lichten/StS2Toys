using StS2Toys.Services;

namespace StS2Toys;

public partial class CardImageViewerForm : Form
{
    private static string? _portraitsDir;

    public CardImageViewerForm()
    {
        InitializeComponent();
    }

    public void ShowCard(string cardId)
    {
        var oldImage = pictureBox.Image;
        var imagePath = FindCardImage(cardId);
        pictureBox.Image = imagePath != null ? Image.FromFile(imagePath) : null;
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

    static string? FindCardImage(string cardId)
    {
        var dir = GetPortraitsDir();
        if (dir is null) return null;

        var raw = cardId.Contains('.') ? cardId[(cardId.LastIndexOf('.') + 1)..] : cardId;
        var filename = raw.ToLowerInvariant() + ".png";

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
