using StS2Toys.Models;

namespace StS2Toys;

record FloorPoint(int ActIndex, int ActFloor, int GlobalFloor, string FloorType,
    int CurrentHp, int MaxHp, int DamageTaken, int HpHealed, int MaxHpGained, int MaxHpLost);

public partial class HpHistoryForm : Form
{
    const int PadL = 38, PadR = 10, PadT = 14, PadB = 36, FloorBarH = 8;

    private List<FloorPoint> _points = [];

    public HpHistoryForm()
    {
        InitializeComponent();
        VisibleChanged += (_, _) => { if (Visible) Recompose(); };
        _splitContainer.Panel1.Resize += (_, _) => { if (Visible) Recompose(); };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chartBox.Image?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void UpdateHistory(List<List<MapPointHistoryEntry>> history)
    {
        _points = BuildPoints(history);
        RefreshList();
        if (Visible) Recompose();
    }

    static List<FloorPoint> BuildPoints(List<List<MapPointHistoryEntry>> history)
    {
        var result = new List<FloorPoint>();
        int globalFloor = 0;
        for (int actIdx = 0; actIdx < history.Count; actIdx++)
        {
            var act = history[actIdx];
            for (int fi = 0; fi < act.Count; fi++)
            {
                var entry = act[fi];
                var stat = entry.PlayerStats?.FirstOrDefault();
                if (stat is null) continue;
                globalFloor++;
                int maxHp = stat.MaxHp > 0 ? stat.MaxHp : (result.Count > 0 ? result[^1].MaxHp : 1);
                result.Add(new FloorPoint(actIdx, fi + 1, globalFloor, entry.MapPointType,
                    stat.CurrentHp, maxHp, stat.DamageTaken, stat.HpHealed,
                    stat.MaxHpGained, stat.MaxHpLost));
            }
        }
        return result;
    }

    void Recompose()
    {
        if (_points.Count == 0) return;
        var w = _splitContainer.Panel1.ClientSize.Width;
        var h = _splitContainer.Panel1.ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        var bmp = RenderChart(w, h);
        var old = _chartBox.Image;
        _chartBox.Image = bmp;
        old?.Dispose();
    }

    Bitmap RenderChart(int w, int h)
    {
        var bmp = new Bitmap(w, Math.Max(h, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int n = _points.Count;
        int chartW = w - PadL - PadR;
        int chartH = h - PadT - PadB;
        if (chartW < 10 || chartH < 10) return bmp;

        int maxHpVal = Math.Max(1, _points.Max(p => p.MaxHp));

        float ScaleX(int i) => PadL + (float)i / Math.Max(n - 1, 1) * chartW;
        float ScaleY(int hp) => PadT + (chartH - FloorBarH) * (1f - (float)hp / maxHpVal);

        // Floor type color bar
        for (int i = 0; i < n; i++)
        {
            float x0 = i == 0 ? PadL : (ScaleX(i) + ScaleX(i - 1)) / 2;
            float x1 = i == n - 1 ? PadL + chartW : (ScaleX(i) + ScaleX(i + 1)) / 2;
            using var barBrush = new SolidBrush(FloorTypeColor(_points[i].FloorType));
            g.FillRectangle(barBrush, x0, PadT + chartH - FloorBarH, x1 - x0, FloorBarH);
        }

        // Act separator lines and labels
        int prevAct = -1;
        using var actLabelFont = new Font("Segoe UI", 7f);
        using var actLabelBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
        using var sepPen = new Pen(Color.FromArgb(100, 100, 100, 100)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        for (int i = 0; i < n; i++)
        {
            if (_points[i].ActIndex != prevAct)
            {
                if (prevAct >= 0)
                    g.DrawLine(sepPen, ScaleX(i), PadT, ScaleX(i), PadT + chartH - FloorBarH);
                g.DrawString($"Act{_points[i].ActIndex + 1}", actLabelFont, actLabelBrush,
                    new PointF(ScaleX(i) + 2, PadT + 1));
                prevAct = _points[i].ActIndex;
            }
        }

        // Max HP step line
        var maxHpPts = new List<PointF>();
        for (int i = 0; i < n; i++)
        {
            float x = ScaleX(i);
            float y = ScaleY(_points[i].MaxHp);
            if (i > 0 && _points[i].MaxHp != _points[i - 1].MaxHp)
                maxHpPts.Add(new PointF(x, ScaleY(_points[i - 1].MaxHp)));
            maxHpPts.Add(new PointF(x, y));
        }
        if (maxHpPts.Count >= 2)
        {
            using var maxHpPen = new Pen(Color.FromArgb(140, 140, 140), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawLines(maxHpPen, maxHpPts.ToArray());
        }

        // HP area fill
        var areaPts = new List<PointF> { new(ScaleX(0), PadT + chartH - FloorBarH) };
        for (int i = 0; i < n; i++)
            areaPts.Add(new PointF(ScaleX(i), ScaleY(_points[i].CurrentHp)));
        areaPts.Add(new PointF(ScaleX(n - 1), PadT + chartH - FloorBarH));

        using var areaPath = new System.Drawing.Drawing2D.GraphicsPath();
        areaPath.AddLines(areaPts.ToArray());
        areaPath.CloseFigure();
        using var areaBrush = new SolidBrush(Color.FromArgb(55, 70, 130, 220));
        g.FillPath(areaBrush, areaPath);

        // HP line
        if (n >= 2)
        {
            var hpPts = new PointF[n];
            for (int i = 0; i < n; i++)
                hpPts[i] = new PointF(ScaleX(i), ScaleY(_points[i].CurrentHp));
            using var hpPen = new Pen(Color.FromArgb(220, 60, 120, 200), 2f);
            g.DrawLines(hpPen, hpPts);
        }

        // Death marker
        for (int i = 0; i < n; i++)
        {
            if (_points[i].CurrentHp == 0)
            {
                float x = ScaleX(i);
                float y = ScaleY(0);
                using var deathBrush = new SolidBrush(Color.FromArgb(220, 200, 30, 30));
                g.FillEllipse(deathBrush, x - 5, y - 5, 10, 10);
                using var deathPen = new Pen(Color.White, 1.5f);
                g.DrawEllipse(deathPen, x - 5, y - 5, 10, 10);
            }
        }

        // Y-axis grid and labels
        using var axisFont = new Font("Segoe UI", 7f);
        using var axisBrush = new SolidBrush(SystemColors.GrayText);
        using var gridPen = new Pen(Color.FromArgb(40, 0, 0, 0));
        int step = maxHpVal <= 40 ? 10 : maxHpVal <= 80 ? 20 : 40;
        for (int v = 0; v <= maxHpVal; v += step)
        {
            float y = ScaleY(v);
            g.DrawLine(gridPen, PadL, y, w - PadR, y);
            var label = v.ToString();
            var sz = g.MeasureString(label, axisFont);
            g.DrawString(label, axisFont, axisBrush, new PointF(PadL - sz.Width - 2, y - sz.Height / 2));
        }

        // X-axis floor number labels
        int labelInterval = n <= 20 ? 5 : n <= 50 ? 10 : 20;
        using var floorFont = new Font("Segoe UI", 7f);
        using var floorBrush = new SolidBrush(SystemColors.GrayText);
        for (int i = 0; i < n; i++)
        {
            if (i == 0 || i == n - 1 || _points[i].GlobalFloor % labelInterval == 0)
            {
                float x = ScaleX(i);
                var label = _points[i].GlobalFloor.ToString();
                var sz = g.MeasureString(label, floorFont);
                g.DrawString(label, floorFont, floorBrush,
                    new PointF(x - sz.Width / 2, PadT + chartH - FloorBarH + 2));
            }
        }

        return bmp;
    }

    void RefreshList()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var p in _points)
        {
            int netChange = p.HpHealed - p.DamageTaken + p.MaxHpGained - p.MaxHpLost;
            var item = new ListViewItem($"Act{p.ActIndex + 1}");
            item.SubItems.Add(p.ActFloor.ToString());
            item.SubItems.Add(LocalizeFloorType(p.FloorType));
            item.SubItems.Add($"{p.CurrentHp}/{p.MaxHp}");
            item.SubItems.Add(netChange > 0 ? $"+{netChange}" : netChange < 0 ? netChange.ToString() : "-");
            item.SubItems.Add(p.DamageTaken > 0 ? p.DamageTaken.ToString() : "-");
            item.SubItems.Add(p.HpHealed > 0 ? p.HpHealed.ToString() : "-");

            if (p.CurrentHp == 0)
                item.ForeColor = Color.DarkRed;
            else if (p.DamageTaken > p.HpHealed)
                item.ForeColor = Color.FromArgb(180, 60, 0);
            else if (p.HpHealed > p.DamageTaken)
                item.ForeColor = Color.DarkGreen;

            _listView.Items.Add(item);
        }
        _listView.EndUpdate();
        if (_listView.Items.Count > 0)
            _listView.EnsureVisible(_listView.Items.Count - 1);
    }

    static Color FloorTypeColor(string type) => type.ToLowerInvariant() switch
    {
        "monster"  => Color.FromArgb(200, 255, 110, 110),
        "elite"    => Color.FromArgb(200, 210, 60,  60),
        "boss"     => Color.FromArgb(200, 170, 20,  20),
        "rest"     => Color.FromArgb(200, 100, 200, 100),
        "merchant" => Color.FromArgb(200, 255, 220, 80),
        "treasure" => Color.FromArgb(200, 255, 200, 50),
        "event"    => Color.FromArgb(200, 150, 150, 255),
        "ancient"  => Color.FromArgb(200, 200, 150, 255),
        "unknown"  => Color.FromArgb(200, 180, 160, 220),
        _          => Color.FromArgb(200, 180, 180, 180),
    };

    static string LocalizeFloorType(string type) => type.ToLowerInvariant() switch
    {
        "monster"  => "戦闘",
        "elite"    => "精鋭",
        "boss"     => "ボス",
        "rest"     => "休憩",
        "merchant" => "商人",
        "treasure" => "宝",
        "event"    => "イベント",
        "ancient"  => "古代の儀式",
        "unknown"  => "???",
        _          => type.Length > 0 ? type : "不明",
    };
}
