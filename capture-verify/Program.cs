using System.Drawing;
using StS2Capture.Recognition;

// キャプチャ静止画でエンシェントレリック（名前テキスト OCR）検出を実証・較正する CLI。
// 使い方: capture-verify <pngPath> [--client x,y,w,h] [--crops <dir>]
//   --client : 認識に渡す client 矩形（既定 = (0,45,W,H-45)。タイトルバー込み静止画用）
//   --crops  : 指定すると各名前バンドの二値化クロップを PNG 保存

if (args.Length == 0)
{
    Console.WriteLine("usage: capture-verify <pngPath> [--client x,y,w,h] [--crops <dir>]");
    return 1;
}

string pngPath = args[0];
if (!File.Exists(pngPath)) { Console.WriteLine($"file not found: {pngPath}"); return 1; }

Rectangle? clientOverride = null;
string? cropsDir = null;
double? dy = null, bandH = null, bandW = null, cx = null;
double? iconDx = null, iconSize = null, iconMax = null, scanRange = null;
int? scale = null, scanSteps = null;
for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--client" when i + 1 < args.Length:
            var p = args[++i].Split(',');
            if (p.Length == 4) clientOverride = new Rectangle(
                int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
            break;
        case "--crops" when i + 1 < args.Length: cropsDir = args[++i]; break;
        case "--dy" when i + 1 < args.Length: dy = double.Parse(args[++i]); break;
        case "--bandh" when i + 1 < args.Length: bandH = double.Parse(args[++i]); break;
        case "--bandw" when i + 1 < args.Length: bandW = double.Parse(args[++i]); break;
        case "--cx" when i + 1 < args.Length: cx = double.Parse(args[++i]); break;
        case "--scale" when i + 1 < args.Length: scale = int.Parse(args[++i]); break;
        case "--icondx" when i + 1 < args.Length: iconDx = double.Parse(args[++i]); break;
        case "--iconsize" when i + 1 < args.Length: iconSize = double.Parse(args[++i]); break;
        case "--iconmax" when i + 1 < args.Length: iconMax = double.Parse(args[++i]); break;
        case "--scansteps" when i + 1 < args.Length: scanSteps = int.Parse(args[++i]); break;
        case "--scanrange" when i + 1 < args.Length: scanRange = double.Parse(args[++i]); break;
    }
}

using var bmp = new Bitmap(pngPath);
// 既定 client: タイトルバー（≈45px）を除いた領域。ライブ WGC（client のみ）相当。
var client = clientOverride ?? new Rectangle(0, 45, bmp.Width, bmp.Height - 45);

Console.WriteLine($"image: {pngPath}  size={bmp.Width}x{bmp.Height}");
Console.WriteLine($"client: {client.X},{client.Y},{client.Width},{client.Height}");

var ancient = new AncientRelicRecognizer();
if (cropsDir is not null) ancient.SaveCropsDir = cropsDir;
if (scale is int sc) ancient.TitleScale = sc;
if (iconDx is double idx) ancient.IconDxFrac = idx;
if (iconSize is double isz) ancient.IconSizeFrac = isz;
if (iconMax is double imx) ancient.IconMaxDistance = imx;
if (scanSteps is int ss) ancient.BandScanSteps = ss;       // 1 で縦スキャン無効（旧挙動）
if (scanRange is double sr) ancient.BandScanRange = sr;
if (dy is not null || bandH is not null || bandW is not null || cx is not null)
    ancient.NameBands = ancient.NameBands
        .Select(b => new AncientRelicRecognizer.NameBand(
            cx ?? b.CxFrac, b.CyFrac + (dy ?? 0), bandW ?? b.WFrac, bandH ?? b.HFrac))
        .ToList();
Console.WriteLine($"OCR engine available: {ancient.IsAvailable}");
Console.WriteLine();

// 1) 各バンドの OCR 生テキスト・名前 family・アイコンで選んだレリック。
var diag = ancient.Diagnose(bmp, client);
for (int s = 0; s < diag.Count; s++)
{
    var d = diag[s];
    Console.WriteLine($"[band {s}] name={d.NameRect.X},{d.NameRect.Y},{d.NameRect.Width},{d.NameRect.Height}"
        + $"  icon={d.IconRect.X},{d.IconRect.Y},{d.IconRect.Width},{d.IconRect.Height}");
    Console.WriteLine($"    OCR   : \"{d.Ocr}\"");
    Console.WriteLine($"    family: {(d.Family.Count > 0 ? string.Join(", ", d.Family) : "(none)")}");
    Console.WriteLine(d.ChosenId is not null
        ? $"    chosen: {d.ChosenId} ({d.ChosenName})  nameDist={(d.NameDist == int.MaxValue ? "-" : d.NameDist.ToString())}  iconChi={d.IconChi:F3}  accepted={d.Accepted}"
        : "    chosen: (none)");
}
Console.WriteLine();

// 2) 実際の採否（Detect）。
var det = ancient.Detect(bmp, client);
Console.WriteLine($"Detect: matched(IsShop)={det.IsShop}  accepted={det.Items.Count(i => i.Accepted)}/{det.Items.Count}");
foreach (var it in det.Items)
{
    var label = it.Candidates.Count == 0 ? "(no match)" :
        string.Join(" / ", it.Candidates.Select(c => $"{c.Id} ({c.Name})"));
    Console.WriteLine($"    accepted={it.Accepted}  {label}");
}
Console.WriteLine();

// 3) ショップ誤検出（プリエンプト）診断。ScreenRecognizer は shop 判定を先に走らせ、
//    IsShop:true なら AncientSelect に到達しない（ScreenRecognizer.cs:93-97）。
var shopReco = new ShopItemRecognizer();
var shop = shopReco.Detect(bmp, client);
Console.WriteLine($"Shop probe: IsShop={shop.IsShop}  accepted={shop.Items.Count(i => i.Accepted)}/{shop.Items.Count} (MinMatchesForShop={shopReco.MinMatchesForShop})");
foreach (var it in shop.Items)
{
    var label = it.Candidates.Count == 0 ? "(no match)" :
        string.Join(" / ", it.Candidates.Select(c => $"{c.Id} ({c.Name}) d={c.Distance:F3}"));
    Console.WriteLine($"    {it.Kind} accepted={it.Accepted}  {label}");
}
Console.WriteLine();

// 4) 実ディスパッチ再現（ScreenRecognizer.Recognize）。ライブアプリの最終判定と同じ。
var screenReco = new ScreenRecognizer(shopReco);
var screen = screenReco.Recognize(bmp, client, null);
Console.WriteLine($"ScreenRecognizer.Recognize => {screen.Type}  (portraits available={screenReco.IsAvailable})");
return 0;
