using StS2Toys.Services;

namespace StS2Toys;

public partial class CardDetailForm : Form
{
    public CardDetailForm()
    {
        InitializeComponent();
        btnClose.Click += (_, _) => Close();
    }

    public void UpdateCard(string id, bool isRelic)
    {
        var en = CardDatabaseService.GetName(id, japanese: false);
        var ja = CardDatabaseService.GetName(id, japanese: true);
        lblTitle.Text = $"{en}  /  {ja}";
        Text = isRelic ? $"レリック: {ja}" : $"カード: {ja}";

        var (descEn, descJa) = CardDatabaseService.GetDescription(id);
        rtbDescEn.Text = DescriptionFormatter.Clean(descEn);
        rtbDescJa.Text = DescriptionFormatter.Clean(descJa);

        var flavor = CardDatabaseService.GetFlavor(id);
        if (flavor is { } f)
        {
            lblFlavor.Visible = true;
            rtbFlavor.Visible = true;
            rtbFlavor.Text    = $"{f.En}\n\n{f.Ja}";

            tableLayout.RowStyles[2].Height = 33.3f;
            tableLayout.RowStyles[4].Height = 33.3f;
            tableLayout.RowStyles[5].SizeType = SizeType.AutoSize;
            tableLayout.RowStyles[5].Height   = 0f;
            tableLayout.RowStyles[6].SizeType = SizeType.Percent;
            tableLayout.RowStyles[6].Height   = 33.4f;
        }
        else
        {
            lblFlavor.Visible = false;
            rtbFlavor.Visible = false;
            rtbFlavor.Text    = "";

            tableLayout.RowStyles[2].Height   = 50f;
            tableLayout.RowStyles[4].Height   = 50f;
            tableLayout.RowStyles[5].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[5].Height   = 0f;
            tableLayout.RowStyles[6].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[6].Height   = 0f;
        }
    }
}
