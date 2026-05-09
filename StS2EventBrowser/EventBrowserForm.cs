using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp.Formats.Png;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISColor = SixLabors.ImageSharp.Color;
using ISImage = SixLabors.ImageSharp.Image;
using ISRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using WinColor = System.Drawing.Color;
using WinImage = System.Drawing.Image;
using WinSize = System.Drawing.Size;

namespace StS2EventBrowser;

record EventOption(string Title, string Description);
record EventInfo(string Id, string Title, string Description, List<EventOption> Options);

public partial class EventBrowserForm : Form
{
    readonly string _toolsRoot;
    List<EventInfo> _allEvents = [];
    List<EventInfo> _filtered = [];
    bool _isJp = true;
    string? _selectedEventId;

    TextBox _filterBox = null!;
    Button _btnJp = null!, _btnEn = null!;
    ListBox _eventList = null!;
    PictureBox _pictureBox = null!;
    RichTextBox _textBox = null!;
    Label _statusLabel = null!;

    public EventBrowserForm()
    {
        _toolsRoot = FindToolsRoot();
        BuildUi();
        LoadEvents();
        PopulateList();
    }

    void BuildUi()
    {
        Text = "StS2 Event Browser";
        Size = new WinSize(1200, 800);
        MinimumSize = new WinSize(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Top bar
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 36 };
        var filterLabel = new Label { Text = "検索:", AutoSize = true, Left = 8, Top = 9 };
        _filterBox = new TextBox { Left = 48, Top = 6, Width = 260, PlaceholderText = "イベント名でフィルタ..." };
        _btnJp = new Button { Text = "JP", Left = 318, Top = 5, Width = 40, Height = 26, FlatStyle = FlatStyle.Flat };
        _btnEn = new Button { Text = "EN", Left = 362, Top = 5, Width = 40, Height = 26, FlatStyle = FlatStyle.Flat };
        _statusLabel = new Label { Left = 420, Top = 9, Width = 300, AutoSize = true, ForeColor = SystemColors.GrayText };
        topPanel.Controls.AddRange([filterLabel, _filterBox, _btnJp, _btnEn, _statusLabel]);

        // Status bar
        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = SystemColors.ControlLight };
        var countLabel = new Label { Name = "countLabel", Left = 4, Top = 4, AutoSize = true, ForeColor = SystemColors.GrayText, Font = new Font("Segoe UI", 8f) };
        statusBar.Controls.Add(countLabel);

        // Main split: list | detail
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 260,
            FixedPanel = FixedPanel.Panel1,
        };

        // Left: event list
        _eventList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            IntegralHeight = false,
        };
        mainSplit.Panel1.Controls.Add(_eventList);

        // Right: image + text
        var detailSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320,
        };
        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = WinColor.FromArgb(24, 24, 28),
        };
        detailSplit.Panel1.Controls.Add(_pictureBox);

        _textBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Font = new Font("Segoe UI", 9.5f),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };
        detailSplit.Panel2.Controls.Add(_textBox);
        mainSplit.Panel2.Controls.Add(detailSplit);

        Controls.Add(mainSplit);
        Controls.Add(statusBar);
        Controls.Add(topPanel);

        UpdateLangButtons();

        _filterBox.TextChanged += (_, _) => PopulateList();
        _btnJp.Click += (_, _) => { _isJp = true; UpdateLangButtons(); LoadEvents(); PopulateList(); };
        _btnEn.Click += (_, _) => { _isJp = false; UpdateLangButtons(); LoadEvents(); PopulateList(); };
        _eventList.SelectedIndexChanged += (_, _) => ShowSelected();
    }

    void UpdateLangButtons()
    {
        _btnJp.Font = new Font("Segoe UI", 9f, _isJp ? FontStyle.Bold : FontStyle.Regular);
        _btnEn.Font = new Font("Segoe UI", 9f, _isJp ? FontStyle.Regular : FontStyle.Bold);
        _btnJp.FlatAppearance.BorderSize = _isJp ? 2 : 1;
        _btnEn.FlatAppearance.BorderSize = _isJp ? 1 : 2;
    }

    // ── Data loading ──────────────────────────────────────────────

    void LoadEvents()
    {
        var lang = _isJp ? "jpn" : "eng";
        var jsonPath = Path.Combine(_toolsRoot, "localization", lang, "events.json");
        if (!File.Exists(jsonPath)) return;

        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Collect all keys grouped by event ID
        var byId = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in root.EnumerateObject())
        {
            var dotIdx = prop.Name.IndexOf('.');
            if (dotIdx < 0) continue;
            var id = prop.Name[..dotIdx];
            var subKey = prop.Name[(dotIdx + 1)..];
            if (!byId.TryGetValue(id, out var sub))
                byId[id] = sub = [];
            sub[subKey] = prop.Value.GetString() ?? "";
        }

        static bool Skip(string id) => id is "DEPRECATED_EVENT" or "MOCK_EVENT_MODEL"
            or "ERROR" or "PROCEED";

        _allEvents = [];
        foreach (var (id, keys) in byId)
        {
            if (!keys.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title)) continue;
            if (Skip(id)) continue;

            var desc = ResolveInitialDescription(keys);
            var options = ResolveInitialOptions(keys);

            _allEvents.Add(new EventInfo(id, title, desc, options));
        }
        _allEvents.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCulture));
    }

    static string ResolveInitialDescription(Dictionary<string, string> keys)
    {
        if (keys.TryGetValue("pages.INITIAL.description", out var d) && !string.IsNullOrEmpty(d))
            return StripTags(d);
        // fallback: any page description
        foreach (var k in keys.Keys.Where(k => k.EndsWith(".description") && k.StartsWith("pages.")))
        {
            if (!string.IsNullOrEmpty(keys[k])) return StripTags(keys[k]);
        }
        return "";
    }

    static List<EventOption> ResolveInitialOptions(Dictionary<string, string> keys)
    {
        var opts = new List<EventOption>();
        const string prefix = "pages.INITIAL.options.";
        foreach (var k in keys.Keys.Where(k => k.StartsWith(prefix) && k.EndsWith(".title")))
        {
            var base_ = k[..^".title".Length];
            var t = StripTags(keys[k]);
            var d = keys.TryGetValue(base_ + ".description", out var od) ? StripTags(od) : "";
            if (!string.IsNullOrWhiteSpace(t))
                opts.Add(new EventOption(t, d));
        }
        return opts;
    }

    // ── List UI ───────────────────────────────────────────────────

    void PopulateList()
    {
        var filter = _filterBox.Text.Trim();
        _filtered = string.IsNullOrEmpty(filter)
            ? [.._allEvents]
            : [.._allEvents.Where(e => e.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                    || e.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))];

        _eventList.BeginUpdate();
        _eventList.Items.Clear();
        foreach (var e in _filtered)
            _eventList.Items.Add(e.Title);
        _eventList.EndUpdate();

        // Update count label
        if (Controls.Find("countLabel", true).FirstOrDefault() is Label lbl)
            lbl.Text = $"{_filtered.Count} / {_allEvents.Count} イベント";

        // Restore selection
        if (_selectedEventId != null)
        {
            var idx = _filtered.FindIndex(e => e.Id == _selectedEventId);
            if (idx >= 0) _eventList.SelectedIndex = idx;
        }
    }

    void ShowSelected()
    {
        var idx = _eventList.SelectedIndex;
        if (idx < 0 || idx >= _filtered.Count) return;
        var ev = _filtered[idx];
        _selectedEventId = ev.Id;
        ShowEvent(ev);
    }

    // ── Detail view ───────────────────────────────────────────────

    void ShowEvent(EventInfo ev)
    {
        // Image (load in background to avoid freezing)
        var old = _pictureBox.Image;
        _pictureBox.Image = null;
        old?.Dispose();

        Task.Run(() => TryLoadEventImage(ev.Id)).ContinueWith(t =>
        {
            if (t.Result is WinImage img && _selectedEventId == ev.Id)
            {
                var prev = _pictureBox.Image;
                _pictureBox.Image = img;
                prev?.Dispose();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        // Text
        _textBox.SuspendLayout();
        _textBox.Clear();

        using (var titleFont = new Font("Segoe UI", 14f, FontStyle.Bold))
        {
            _textBox.SelectionFont = titleFont;
            _textBox.SelectionColor = WinColor.FromArgb(20, 20, 60);
            _textBox.AppendText(ev.Title + "\n\n");
        }

        if (!string.IsNullOrEmpty(ev.Description))
        {
            _textBox.SelectionFont = _textBox.Font;
            _textBox.SelectionColor = SystemColors.ControlText;
            _textBox.AppendText(ev.Description + "\n");
        }

        if (ev.Options.Count > 0)
        {
            _textBox.AppendText("\n");
            using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _textBox.SelectionFont = boldFont;
            _textBox.SelectionColor = WinColor.FromArgb(40, 40, 120);
            _textBox.AppendText(_isJp ? "選択肢\n" : "Options\n");

            foreach (var opt in ev.Options)
            {
                _textBox.SelectionFont = boldFont;
                _textBox.SelectionColor = SystemColors.ControlText;
                _textBox.AppendText($"◆ {opt.Title}");

                if (!string.IsNullOrWhiteSpace(opt.Description))
                {
                    _textBox.SelectionFont = _textBox.Font;
                    _textBox.AppendText($": {opt.Description}");
                }
                _textBox.AppendText("\n");
            }
        }

        _textBox.SelectionStart = 0;
        _textBox.ScrollToCaret();
        _textBox.ResumeLayout();
    }

    // ── Image loading ─────────────────────────────────────────────

    WinImage? TryLoadEventImage(string eventId)
    {
        try { return LoadEventImage(eventId); }
        catch { return null; }
    }

    WinImage? LoadEventImage(string eventId)
    {
        var pngCacheDir = Path.Combine(_toolsRoot, "images", "events_png");
        Directory.CreateDirectory(pngCacheDir);
        var pngPath = Path.Combine(pngCacheDir, eventId.ToLowerInvariant() + ".png");

        if (!File.Exists(pngPath))
        {
            var importPath = Path.Combine(_toolsRoot, "images", "events",
                eventId.ToLowerInvariant() + ".png.import");
            if (!File.Exists(importPath)) return null;

            var ctexRelPath = ParseCtexPath(importPath);
            if (ctexRelPath is null) return null;

            var ctexFull = Path.Combine(_toolsRoot,
                ctexRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(ctexFull)) return null;

            ConvertCtex(ctexFull, pngPath);
        }

        var bytes = File.ReadAllBytes(pngPath);
        using var ms = new MemoryStream(bytes);
        return new System.Drawing.Bitmap(ms);
    }

    static string? ParseCtexPath(string importPath)
    {
        var content = File.ReadAllText(importPath);
        // Matches: path="res://..." or path.bptc="res://..." or path.s3tc="res://..."
        var m = Regex.Match(content, @"^path(?:\.\w+)?=""res://(.+?\.ctex)""",
            RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    static void ConvertCtex(string srcPath, string outPath)
    {
        var data = File.ReadAllBytes(srcPath);
        if (System.Text.Encoding.ASCII.GetString(data, 0, 4) != "GST2")
            throw new InvalidDataException("Not a GST2 ctex file");

        var width      = (int)BitConverter.ToUInt32(data, 8);
        var height     = (int)BitConverter.ToUInt32(data, 12);
        var dataFormat = BitConverter.ToUInt32(data, 36); // 0=BC raw, 2=WebP

        const int Hdr = 52;
        using var img = dataFormat == 2
            ? LoadWebP(data, Hdr)
            : DecodeBc7(data, Hdr, width, height);
        using var outStream = File.OpenWrite(outPath);
        img.Save(outStream, new PngEncoder());
    }

    static ISRgba32 LoadWebP(byte[] data, int hdr)
    {
        var size = (int)BitConverter.ToUInt32(data, hdr);
        using var ms = new MemoryStream(data, hdr + 4, size);
        return ISImage.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(ms);
    }

    static ISRgba32 DecodeBc7(byte[] data, int hdr, int w, int h)
    {
        var bc7Data = new ReadOnlyMemory<byte>(data, hdr, data.Length - hdr);
        var decoder = new BcDecoder();
        var pixels  = decoder.DecodeRaw(bc7Data.ToArray(), w, h, CompressionFormat.Bc7);
        var bytes   = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
        return ISImage.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes, w, h);
    }

    // ── Utilities ─────────────────────────────────────────────────

    static string StripTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var s = Regex.Replace(text, @"\[[^\]]*\]", "");
        s = Regex.Replace(s, @"\{[^}]+\}", "?");
        return s.Trim();
    }

    static string FindToolsRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "tools", "extracted")))
                return Path.Combine(dir, "tools", "extracted");
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("tools/extracted が見つかりません");
    }
}
