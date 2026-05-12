using StS2Shared.Services;

namespace StS2Toys;

public partial class CardDetailForm : Form
{
    public CardDetailForm()
    {
        InitializeComponent();
        btnClose.Click += (_, _) => Close();
    }

    public void UpdateCard(string id, bool isRelic, string? enchantmentId = null, int enchantmentAmount = 0)
    {
        var en = CardDatabaseService.GetName(id, japanese: false);
        var ja = CardDatabaseService.GetName(id, japanese: true);
        lblTitle.Text = $"{en}  /  {ja}";
        Text = isRelic ? $"レリック: {ja}" : $"カード: {ja}";

        var (descEn, descJa) = CardDatabaseService.GetDescription(id);
        var stats = isRelic ? null : CardDatabaseService.GetCardStats(id);
        rtbDescEn.Text = DescriptionFormatter.Resolve(descEn, stats);
        rtbDescJa.Text = DescriptionFormatter.Resolve(descJa, stats);

        // エンチャント（カードのみ対象。レリックには付かない）
        bool hasEnchant = !isRelic && !string.IsNullOrEmpty(enchantmentId);
        if (hasEnchant)
        {
            var enName = CardDatabaseService.FormatEnchantmentLabel(enchantmentId!, enchantmentAmount, japanese: false);
            var jaName = CardDatabaseService.FormatEnchantmentLabel(enchantmentId!, enchantmentAmount, japanese: true);
            var enDesc = CardDatabaseService.GetEnchantmentDescription(enchantmentId!, enchantmentAmount, japanese: false);
            var jaDesc = CardDatabaseService.GetEnchantmentDescription(enchantmentId!, enchantmentAmount, japanese: true);
            rtbEnchant.Text = $"{enName} / {jaName}" +
                (string.IsNullOrEmpty(enDesc) ? "" : $"\n{enDesc}") +
                (string.IsNullOrEmpty(jaDesc) ? "" : $"\n{jaDesc}");
            lblEnchant.Visible = true;
            rtbEnchant.Visible = true;
            tableLayout.RowStyles[5].SizeType = SizeType.AutoSize;
            tableLayout.RowStyles[5].Height   = 0f;
            tableLayout.RowStyles[6].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[6].Height   = 72f;
        }
        else
        {
            rtbEnchant.Text    = "";
            lblEnchant.Visible = false;
            rtbEnchant.Visible = false;
            tableLayout.RowStyles[5].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[5].Height   = 0f;
            tableLayout.RowStyles[6].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[6].Height   = 0f;
        }

        // フレーバーテキスト（レリックのみ）
        var flavor = CardDatabaseService.GetFlavor(id);
        if (flavor is { } f)
        {
            lblFlavor.Visible = true;
            rtbFlavor.Visible = true;
            rtbFlavor.Text    = $"{f.En}\n\n{f.Ja}";

            tableLayout.RowStyles[2].Height   = 33.3f;
            tableLayout.RowStyles[4].Height   = 33.3f;
            tableLayout.RowStyles[7].SizeType = SizeType.AutoSize;
            tableLayout.RowStyles[7].Height   = 0f;
            tableLayout.RowStyles[8].SizeType = SizeType.Percent;
            tableLayout.RowStyles[8].Height   = 33.4f;
        }
        else
        {
            lblFlavor.Visible = false;
            rtbFlavor.Visible = false;
            rtbFlavor.Text    = "";

            tableLayout.RowStyles[2].Height   = 50f;
            tableLayout.RowStyles[4].Height   = 50f;
            tableLayout.RowStyles[7].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[7].Height   = 0f;
            tableLayout.RowStyles[8].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[8].Height   = 0f;
        }
    }
}
