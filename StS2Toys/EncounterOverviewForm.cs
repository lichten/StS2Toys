using System.Diagnostics;
using System.Text;
using StS2Shared.Models;
using StS2Shared.Services;
using StS2Toys.Services;

namespace StS2Toys;

public partial class EncounterOverviewForm : Form
{
    private RunSaveData? _data;
    private string? _autoActId; // 現在Act id（接頭辞除去、例 "OVERGROWTH"）

    readonly ToolTip _hoverTip = new() { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000, ShowAlways = true };
    readonly ContextMenuStrip _linkMenu = new();
    List<(Rectangle Rect, string Id)> _hitMap = [];
    string? _hoveredId;

    const int PadX = 12, PadY = 10, HeaderH = 26, RowH = 46, Thumb = 40, ThumbGap = 4, NameColW = 200, SectionGap = 10;

    public EncounterOverviewForm()
    {
        InitializeComponent();

        _actSelector.Items.Add(AppLanguage.IsJapanese ? "自動（現在Act）" : "Auto (current Act)");
        foreach (var g in EncounterActService.Groups)
            _actSelector.Items.Add(AppLanguage.IsJapanese ? g.NameJp : g.NameEn);
        _actSelector.SelectedIndex = 0;
        _actSelector.SelectedIndexChanged += (_, _) => Recompose();

        VisibleChanged += (_, _) => { if (Visible) Recompose(); };
        ResizeEnd      += (_, _) => { if (Visible) Recompose(); };
        _pictureBox.MouseClick += OnPictureBoxClick;
        _pictureBox.MouseMove  += OnPictureBoxMouseMove;
        _pictureBox.MouseLeave += (_, _) => { _hoverTip.Hide(_pictureBox); _hoveredId = null; _pictureBox.Cursor = Cursors.Default; };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pictureBox.Image?.Dispose();
            _hoverTip.Dispose();
            _linkMenu.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void UpdateData(RunSaveData data)
    {
        _data = data;
        _autoActId = data.CurrentActIndex >= 0 && data.CurrentActIndex < data.Acts.Count
            ? ToRaw(data.Acts[data.CurrentActIndex].Id)
            : null;
        if (Visible) Recompose();
    }

    static string ToRaw(string id) => id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;

    void Recompose()
    {
        int w = _scrollPanel.ClientSize.Width;
        if (w <= 0) return;

        var bmp = RenderContent(w);
        var old = _pictureBox.Image;
        _pictureBox.Size  = new Size(bmp.Width, bmp.Height);
        _pictureBox.Image = bmp;
        old?.Dispose();
    }

    /// <summary>セレクタの選択に対応する Act 定義。「自動」なら現在ランの Act。該当なしは null。</summary>
    EncounterActService.ActEncounters? SelectedGroup()
    {
        var groups = EncounterActService.Groups;
        int idx = _actSelector.SelectedIndex;
        if (idx >= 1 && idx - 1 < groups.Count) return groups[idx - 1];
        return _autoActId is null
            ? null
            : groups.FirstOrDefault(g => string.Equals(g.Id, _autoActId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>現在ラン Act で遭遇済みのエンカウンター ID 集合（接頭辞除去）。</summary>
    HashSet<string> BuildVisitedSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int ci = _data?.CurrentActIndex ?? -1;
        if (_data is null || ci < 0 || ci >= _data.Acts.Count) return set;
        var rooms = _data.Acts[ci].Rooms;
        if (rooms is null) return set;

        foreach (var id in rooms.NormalEncounterIds.Take(rooms.NormalEncountersVisited)) set.Add(ToRaw(id));
        foreach (var id in rooms.EliteEncounterIds.Take(rooms.EliteEncountersVisited)) set.Add(ToRaw(id));
        if (rooms.BossEncountersVisited > 0)
        {
            if (!string.IsNullOrEmpty(rooms.BossId)) set.Add(ToRaw(rooms.BossId));
            if (!string.IsNullOrEmpty(rooms.SecondBossId)) set.Add(ToRaw(rooms.SecondBossId));
        }
        return set;
    }

    /// <summary>
    /// 表示中 Act でセーブが確定済みのボス ID 集合（接頭辞除去）。Act 入場時に確定するため戦闘前でも出る。
    /// セーブに無い Act（未到達＝Rooms null）や未読込なら空集合。
    /// </summary>
    HashSet<string> BuildPredictedBossSet(EncounterActService.ActEncounters group)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var act = _data?.Acts.FirstOrDefault(a =>
            string.Equals(ToRaw(a.Id), group.Id, StringComparison.OrdinalIgnoreCase));
        var rooms = act?.Rooms;
        if (rooms is null) return set;
        if (!string.IsNullOrEmpty(rooms.BossId)) set.Add(ToRaw(rooms.BossId));
        if (!string.IsNullOrEmpty(rooms.SecondBossId)) set.Add(ToRaw(rooms.SecondBossId));
        return set;
    }

    Bitmap RenderContent(int w)
    {
        _hitMap.Clear();
        bool ja = AppLanguage.IsJapanese;
        var group = SelectedGroup();

        if (group is null)
        {
            var empty = new Bitmap(w, 60);
            using var ge = Graphics.FromImage(empty);
            ge.Clear(SystemColors.Control);
            using var ef = new Font("Segoe UI", 9f);
            using var eb = new SolidBrush(SystemColors.GrayText);
            ge.DrawString(ja ? "セーブを読み込むと現在Actを表示します。" : "Load a save to show the current Act.",
                ef, eb, new PointF(PadX, PadY));
            return empty;
        }

        bool isCurrentAct = _autoActId is not null && string.Equals(group.Id, _autoActId, StringComparison.OrdinalIgnoreCase);
        var visited = isCurrentAct ? BuildVisitedSet() : [];
        var predicted = BuildPredictedBossSet(group);

        var boss = group.BossOrder.Count > 0 ? group.BossOrder : group.Boss;
        (string Ja, string En, IReadOnlyList<string> Ids)[] tiers =
        [
            ("弱小",     "Weak",   group.Weak),
            ("通常",     "Normal", group.Normal),
            ("エリート", "Elite",  group.Elite),
            ("ボス",     "Boss",   boss),
        ];

        int totalH = PadY + HeaderH; // Act タイトル分
        foreach (var t in tiers)
            if (t.Ids.Count > 0) totalH += HeaderH + t.Ids.Count * RowH + SectionGap;
        totalH += PadY;

        var bmp = new Bitmap(w, Math.Max(totalH, 1));
        using var g = Graphics.FromImage(bmp);
        g.Clear(SystemColors.Control);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var actFont  = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var tierFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var nameFont = new Font("Segoe UI", 9f);
        using var actBrush  = new SolidBrush(Color.DarkBlue);
        using var tierBrush = new SolidBrush(Color.FromArgb(90, 90, 90));
        using var nameBrush = new SolidBrush(SystemColors.ControlText);
        using var grayBrush = new SolidBrush(SystemColors.GrayText);
        using var checkBrush = new SolidBrush(Color.FromArgb(20, 140, 40));
        using var rowPen     = new Pen(Color.FromArgb(40, 0, 0, 0));

        int y = PadY;
        var actName = ja ? group.NameJp : group.NameEn;
        var tag = isCurrentAct ? (ja ? "　【現在】" : "  [Current]") : "";
        g.DrawString(actName + tag, actFont, actBrush, new PointF(PadX, y));
        y += HeaderH;

        foreach (var (tierJa, tierEn, ids) in tiers)
        {
            if (ids.Count == 0) continue;
            g.DrawString(ja ? tierJa : tierEn, tierFont, tierBrush, new PointF(PadX, y));
            y += HeaderH;
            foreach (var encId in ids)
            {
                var rect = new Rectangle(PadX, y, w - 2 * PadX, RowH);
                DrawEncounterRow(g, encId, rect, ja, visited.Contains(ToRaw(encId)),
                    predicted.Contains(ToRaw(encId)),
                    nameFont, nameBrush, grayBrush, checkBrush, rowPen);
                _hitMap.Add((rect, encId));
                y += RowH;
            }
            y += SectionGap;
        }

        return bmp;
    }

    void DrawEncounterRow(Graphics g, string encId, Rectangle rect, bool ja, bool visited, bool predicted,
        Font nameFont, Brush nameBrush, Brush grayBrush, Brush checkBrush, Pen rowPen)
    {
        // 遭遇予定ボスのハイライト背景（最背面。遭遇済みなら後段のディムで落ち着く）。
        if (predicted)
        {
            using var hi = new SolidBrush(Color.FromArgb(255, 250, 224));
            g.FillRectangle(hi, rect);
        }

        // 名前
        var name = EncounterDatabaseService.GetEncounterName(encId, ja);
        int textX = rect.X + 20;
        using var fmt = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        using var predictedFont = predicted && !visited ? new Font(nameFont, FontStyle.Bold) : null;
        using var predictedBrush = new SolidBrush(Color.FromArgb(200, 120, 0));
        Brush nb = visited ? grayBrush : (predicted ? predictedBrush : nameBrush);
        g.DrawString(name, predictedFont ?? nameFont, nb,
            new RectangleF(textX, rect.Y, NameColW, rect.Height), fmt);

        // モンスターサムネ
        int mx = textX + NameColW + 6;
        int my = rect.Y + (rect.Height - Thumb) / 2;
        var dirs = MonsterDatabaseService.GetEncounterMonsterDirs(encId);
        if (dirs is not null)
        {
            foreach (var dir in dirs)
            {
                if (mx + Thumb > rect.Right) break; // 幅クリップ
                var box = new Rectangle(mx, my, Thumb, Thumb);
                var img = MonsterImageService.GetMonsterPng(dir);
                if (img is not null)
                {
                    double scale = Math.Min((double)Thumb / img.Width, (double)Thumb / img.Height);
                    int iw = Math.Max(1, (int)Math.Round(img.Width * scale));
                    int ih = Math.Max(1, (int)Math.Round(img.Height * scale));
                    var oldInterp = g.InterpolationMode;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, new Rectangle(mx + (Thumb - iw) / 2, my + (Thumb - ih) / 2, iw, ih));
                    g.InterpolationMode = oldInterp;
                }
                else
                {
                    using var qPen = new Pen(SystemColors.ControlDark);
                    g.DrawRectangle(qPen, box);
                    using var qFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                    using var qFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("?", qFont, grayBrush, box, qFmt);
                }
                mx += Thumb + ThumbGap;
            }
        }

        // 遭遇済みはディム＋✓（✓ はディムの上に描く）
        if (visited)
        {
            using var dim = new SolidBrush(Color.FromArgb(95, 245, 245, 245));
            g.FillRectangle(dim, rect);
            using var checkFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            g.DrawString("✓", checkFont, checkBrush, new PointF(rect.X, rect.Y + (rect.Height - 20) / 2));
        }
        else if (predicted)
        {
            // 未遭遇の遭遇予定ボスは左端（✓ と同位置）に ★。
            using var starFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            g.DrawString("★", starFont, predictedBrush, new PointF(rect.X, rect.Y + (rect.Height - 20) / 2));
        }

        // 行区切り
        g.DrawLine(rowPen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
    }

    string BuildTooltip(string encId, bool ja)
    {
        var sb = new StringBuilder();
        sb.AppendLine(EncounterDatabaseService.GetEncounterName(encId, ja));
        var monsters = MonsterDatabaseService.GetEncounterMonsters(encId);
        if (monsters.Count > 0)
            sb.Append(string.Join("、", monsters.Select(m => ja ? m.JaLabel : m.EnLabel)));
        return sb.ToString().Trim();
    }

    void OnPictureBoxMouseMove(object? sender, MouseEventArgs e)
    {
        var hit = _hitMap.FirstOrDefault(h => h.Rect.Contains(e.Location));
        _pictureBox.Cursor = hit.Id is not null ? Cursors.Hand : Cursors.Default;
        if (hit.Id == _hoveredId) return;
        _hoveredId = hit.Id;
        if (hit.Id is null) { _hoverTip.Hide(_pictureBox); return; }
        _hoverTip.Show(BuildTooltip(hit.Id, AppLanguage.IsJapanese), _pictureBox, e.X + 16, e.Y + 16);
    }

    void OnPictureBoxClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var hit = _hitMap.FirstOrDefault(h => h.Rect.Contains(e.Location));
        if (hit.Id is null) return;

        var links = SiteLinkService.BuildLinks(UrlTemplateService.Load(), "encounter", hit.Id);
        if (links.Count == 0) return;
        if (links.Count == 1) { OpenUrl(links[0].Url); return; }

        _linkMenu.Items.Clear();
        foreach (var link in links)
        {
            var url = link.Url;
            var item = new ToolStripMenuItem($"{link.Label}: {link.Url}");
            item.Click += (_, _) => OpenUrl(url);
            _linkMenu.Items.Add(item);
        }
        _linkMenu.Show(_pictureBox, e.Location);
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}
