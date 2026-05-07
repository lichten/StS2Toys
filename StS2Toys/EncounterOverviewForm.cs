using StS2Toys.Models;
using StS2Toys.Services;

namespace StS2Toys;

public partial class EncounterOverviewForm : Form
{
    private RunSaveData? _data;

    public EncounterOverviewForm()
    {
        InitializeComponent();
        VisibleChanged += (_, _) => { if (Visible) Recompose(); };
        ResizeEnd      += (_, _) => { if (Visible) Recompose(); };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pictureBox.Image?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void UpdateData(RunSaveData data)
    {
        _data = data;
        if (Visible) Recompose();
    }

    void Recompose()
    {
        if (_data is null) return;
        int w = _scrollPanel.ClientSize.Width;
        if (w <= 0) return;

        var bmp = RenderContent(w);
        var old = _pictureBox.Image;
        _pictureBox.Size  = new Size(bmp.Width, bmp.Height);
        _pictureBox.Image = bmp;
        old?.Dispose();
    }

    Bitmap RenderContent(int w)
    {
        const int PadX = 12, PadY = 10;
        const int LineH = 22, HeaderH = 30, CompletedH = 20, SectionGap = 12;

        var acts       = _data!.Acts;
        int currentIdx = _data.CurrentActIndex;

        // ---- height calculation ----
        int totalH = PadY;
        for (int i = 0; i < acts.Count; i++)
        {
            if (i < currentIdx)
            {
                totalH += CompletedH + 2;
                continue;
            }
            var rooms = acts[i].Rooms;
            totalH += HeaderH;
            if (rooms is not null)
            {
                if (!string.IsNullOrEmpty(rooms.BossId))       totalH += LineH;
                if (!string.IsNullOrEmpty(rooms.SecondBossId)) totalH += LineH;
                totalH += LineH; // elite label
                totalH += RemainingElites(rooms, i == currentIdx).Count * LineH;
            }
            totalH += SectionGap;
        }
        totalH += PadY;

        var bmp = new Bitmap(w, Math.Max(totalH, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var completedFont = new Font("Segoe UI", 8f);
        using var headerFont    = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var labelFont     = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var nameFont      = new Font("Segoe UI", 8.5f);

        using var completedBrush = new SolidBrush(Color.FromArgb(140, 120, 120, 120));
        using var currentBrush   = new SolidBrush(Color.DarkBlue);
        using var futureBrush    = new SolidBrush(SystemColors.ControlText);
        using var bossBrush      = new SolidBrush(Color.FromArgb(180, 30, 30));
        using var eliteBrush     = new SolidBrush(Color.FromArgb(160, 80, 0));

        int y = PadY;

        for (int i = 0; i < acts.Count; i++)
        {
            var act       = acts[i];
            bool done     = i < currentIdx;
            bool isCurr   = i == currentIdx;

            var nameEn = EncounterDatabaseService.GetActName(act.Id, japanese: false);
            var nameJp = EncounterDatabaseService.GetActName(act.Id, japanese: true);

            if (done)
            {
                g.DrawString($"Act {i + 1}  {nameJp}  ✓  完了", completedFont, completedBrush, new PointF(PadX, y + 1));
                y += CompletedH + 2;
                continue;
            }

            // act header
            var tag = isCurr ? "  【現在】" : "";
            g.DrawString($"Act {i + 1}:  {nameJp}  ({nameEn}){tag}",
                headerFont, isCurr ? currentBrush : futureBrush, new PointF(PadX, y));
            y += HeaderH;

            var rooms = act.Rooms;
            if (rooms is null) { y += SectionGap; continue; }

            // boss
            if (!string.IsNullOrEmpty(rooms.BossId))
            {
                var bEn = EncounterDatabaseService.GetEncounterName(rooms.BossId, false);
                var bJp = EncounterDatabaseService.GetEncounterName(rooms.BossId, true);
                float lx = PadX + 16;
                g.DrawString("ボス:  ", labelFont, bossBrush, new PointF(lx, y));
                float lw = g.MeasureString("ボス:  ", labelFont).Width;
                g.DrawString($"{bJp}  ({bEn})", nameFont, bossBrush, new PointF(lx + lw, y));
                y += LineH;
            }

            if (!string.IsNullOrEmpty(rooms.SecondBossId))
            {
                var bEn = EncounterDatabaseService.GetEncounterName(rooms.SecondBossId, false);
                var bJp = EncounterDatabaseService.GetEncounterName(rooms.SecondBossId, true);
                g.DrawString($"第2ボス:  {bJp}  ({bEn})", nameFont, bossBrush, new PointF(PadX + 16, y));
                y += LineH;
            }

            // elites
            var elites     = RemainingElites(rooms, isCurr);
            int uniqueTotal = rooms.EliteEncounterIds.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var eliteLabel  = isCurr
                ? $"エリート  残り {elites.Count} / 全 {uniqueTotal} 種:"
                : $"エリート候補  {uniqueTotal} 種:";
            g.DrawString(eliteLabel, labelFont, eliteBrush, new PointF(PadX + 16, y));
            y += LineH;

            foreach (var id in elites)
            {
                var eEn = EncounterDatabaseService.GetEncounterName(id, false);
                var eJp = EncounterDatabaseService.GetEncounterName(id, true);
                g.DrawString($"   · {eJp}  ({eEn})", nameFont, eliteBrush, new PointF(PadX + 16, y));
                y += LineH;
            }

            y += SectionGap;
        }

        return bmp;
    }

    static List<string> RemainingElites(ActRooms rooms, bool isCurrent)
    {
        int skip = isCurrent ? rooms.EliteEncountersVisited : 0;
        return [.. rooms.EliteEncounterIds
            .Skip(skip)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
