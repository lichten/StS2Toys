using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

var toolsRoot       = FindToolsRoot();
var portraitPngRoot = Path.Combine(toolsRoot, "images", "card_portraits_png");
var jpegOutRoot     = Path.GetFullPath(Path.Combine(toolsRoot, "..", "..", "card-images"));
var ctexImport      = Path.Combine(toolsRoot, ".godot", "imported");

const int JpegWidth   = 300;
const int JpegHeight  = 420;
const int JpegQuality = 40;

// Character mode: convert character select art to JPEG for website
// Usage: dotnet run --project ctex-to-png -- characters
if (args.Length == 1 && args[0] == "characters")
{
    var charIds   = new[] { "ironclad", "silent", "defect", "necrobinder", "regent" };
    var outDir    = Path.GetFullPath(Path.Combine(toolsRoot, "..", "..", "StS2SiteBuilder", "dist", "images", "characters"));
    Directory.CreateDirectory(outDir);

    var charSelectDir = Path.Combine(toolsRoot, "images", "packed", "character_select");
    foreach (var id in charIds)
    {
        var importPath = Path.Combine(charSelectDir, $"char_select_{id}.png.import");
        var ctexRel    = ParseImportCtexPath(importPath);
        if (ctexRel is null) { Console.WriteLine($"  not found: char_select_{id}"); continue; }
        var ctexFull = Path.Combine(toolsRoot, ctexRel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var outPath  = Path.Combine(outDir, $"{id}.jpg");
        ConvertCtexToJpeg(ctexFull, outPath, maxWidth: 0, quality: 85);
        Console.WriteLine($"  characters/{id}.jpg");
    }
    return;
}

// Card JPEG mode: convert specified card IDs to web-sized JPEG for git tracking
// Usage: dotnet run --project ctex-to-png -- <id1> <id2> ...
// Example: dotnet run --project ctex-to-png -- bash defend_ironclad
if (args.Length > 0)
{
    Directory.CreateDirectory(jpegOutRoot);
    foreach (var id in args)
    {
        var matches = Directory.GetFiles(portraitPngRoot, $"{id}.png", SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            Console.WriteLine($"  not found: {id}.png  (run without args first to generate source PNGs)");
            continue;
        }
        foreach (var srcPath in matches)
        {
            var relDir  = Path.GetRelativePath(portraitPngRoot, Path.GetDirectoryName(srcPath)!);
            var outDir  = relDir == "." ? jpegOutRoot : Path.Combine(jpegOutRoot, relDir);
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, id + ".jpg");
            ConvertToJpeg(srcPath, outPath);
            var label = relDir == "." ? id : $"{relDir}/{id}";
            Console.WriteLine($"  {label}.jpg");
        }
    }
    return;
}

// --- 1. Card atlases (card_atlas_N.png) ---
Console.WriteLine("=== Card atlases ===");
var atlasOutDir = Path.Combine(toolsRoot, "images", "atlases");
Directory.CreateDirectory(atlasOutDir);

foreach (var path in Directory.GetFiles(ctexImport, "card_atlas_*.ctex").OrderBy(f => f))
{
    var outName = Path.GetFileNameWithoutExtension(path).Split('-')[0]; // "card_atlas_0.png"
    outName = Path.GetFileNameWithoutExtension(outName) + ".png";       // "card_atlas_0.png"
    var outPath = Path.Combine(atlasOutDir, outName);
    if (File.Exists(outPath)) { Console.WriteLine($"  skip {outName}"); continue; }
    ConvertCtex(path, outPath);
}

// --- 2. Individual card portraits (via .import files) ---
Console.WriteLine("\n=== Card portraits ===");
var portraitsRoot   = Path.Combine(toolsRoot, "images", "packed", "card_portraits");
var portraitOutRoot = Path.Combine(toolsRoot, "images", "card_portraits_png");
Directory.CreateDirectory(portraitOutRoot);

var importFiles = Directory.GetFiles(portraitsRoot, "*.png.import", SearchOption.AllDirectories)
    .Where(f => !f.Contains(@"\beta\", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToList();

Console.WriteLine($"Found {importFiles.Count} card portrait(s)");
int converted = 0, skipped = 0;

foreach (var importPath in importFiles)
{
    var ctexRelPath = ParseImportCtexPath(importPath);
    if (ctexRelPath is null) continue;

    var ctexFull = Path.Combine(toolsRoot, ctexRelPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(ctexFull)) continue;

    var relDir  = Path.GetRelativePath(portraitsRoot, Path.GetDirectoryName(importPath)!);
    var outDir  = Path.Combine(portraitOutRoot, relDir);
    Directory.CreateDirectory(outDir);

    var outName = Path.GetFileNameWithoutExtension(importPath); // e.g. "bash.png"
    var outPath = Path.Combine(outDir, outName);

    if (File.Exists(outPath)) { skipped++; continue; }

    ConvertCtex(ctexFull, outPath, verbose: false);
    converted++;
    if (converted % 50 == 0)
        Console.WriteLine($"  {converted}/{importFiles.Count}...");
}
Console.WriteLine($"  Done. converted={converted} skipped={skipped}");
Console.WriteLine("\nAll done.");

// ── helpers ──────────────────────────────────────────────────────────────────

static string FindToolsRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "tools", "extracted");
        if (Directory.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException(
        "tools/extracted が見つかりません。dotnet run --project ctex-to-png をリポジトリルートから実行してください。");
}

// GST2 header layout:
//   [0]  "GST2" magic (4 bytes)
//   [4]  unknown (4)
//   [8]  width  (uint32)
//   [12] height (uint32)
//   [16..35] unknown
//   [36] data_format: 0 = raw BC-data, 2 = WebP
//   [40..47] unknown
//   [48] Image::Format enum (19 = FORMAT_DXT5/BC3, 22 = FORMAT_BPTC_RGBA/BC7)
//   [52] raw BC data -OR- uint32 data_size followed by WebP RIFF
static void ConvertCtex(string srcPath, string outPath, bool verbose = true)
{
    var data = File.ReadAllBytes(srcPath);

    var magic = System.Text.Encoding.ASCII.GetString(data, 0, 4);
    if (magic != "GST2")
        throw new InvalidDataException($"{Path.GetFileName(srcPath)}: Expected GST2, got {magic}");

    var width      = (int)BitConverter.ToUInt32(data, 8);
    var height     = (int)BitConverter.ToUInt32(data, 12);
    var dataFormat = BitConverter.ToUInt32(data, 36); // 0=BC raw, 2=WebP
    var imgFormat  = BitConverter.ToUInt32(data, 48);

    if (verbose)
        Console.Write($"  {Path.GetFileName(outPath)} {width}x{height} fmt={dataFormat} ... ");

    const int HEADER_SIZE = 52;
    using Image<Rgba32> image =
        dataFormat == 2
            ? LoadWebP(data, HEADER_SIZE)
            : DecodeBc(data, HEADER_SIZE, width, height, BcFormat(imgFormat));

    image.SaveAsPng(outPath);

    if (verbose)
        Console.WriteLine("ok");
}

static void ConvertCtexToJpeg(string srcPath, string outPath, int maxWidth, int quality)
{
    var data       = File.ReadAllBytes(srcPath);
    var width      = (int)BitConverter.ToUInt32(data, 8);
    var height     = (int)BitConverter.ToUInt32(data, 12);
    var dataFormat = BitConverter.ToUInt32(data, 36);
    var imgFormat  = BitConverter.ToUInt32(data, 48);

    const int HEADER_SIZE = 52;
    using var image =
        dataFormat == 2
            ? LoadWebP(data, HEADER_SIZE)
            : DecodeBc(data, HEADER_SIZE, width, height, BcFormat(imgFormat));

    if (maxWidth > 0 && image.Width > maxWidth)
        image.Mutate(x => x.Resize(maxWidth, 0));

    image.Mutate(x => x.BackgroundColor(Color.White));
    image.SaveAsJpeg(outPath, new JpegEncoder { Quality = quality });
}

static CompressionFormat BcFormat(uint imgFormat) => imgFormat switch
{
    19 => CompressionFormat.Bc3,  // FORMAT_DXT5
    _  => CompressionFormat.Bc7,  // FORMAT_BPTC_RGBA (default)
};

static Image<Rgba32> LoadWebP(byte[] data, int headerSize)
{
    // [headerSize] uint32 webpSize, [headerSize+4] RIFF...WEBP bytes
    var webpSize   = (int)BitConverter.ToUInt32(data, headerSize);
    var webpOffset = headerSize + 4;
    using var ms   = new MemoryStream(data, webpOffset, webpSize);
    return Image.Load<Rgba32>(ms);
}

static Image<Rgba32> DecodeBc(byte[] data, int headerSize, int width, int height, CompressionFormat format)
{
    var bcData    = new ReadOnlyMemory<byte>(data, headerSize, data.Length - headerSize);
    var decoder   = new BcDecoder();
    var pixels    = decoder.DecodeRaw(bcData.ToArray(), width, height, format);
    var rgbaBytes = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
    return Image.LoadPixelData<Rgba32>(rgbaBytes, width, height);
}

static string? ParseImportCtexPath(string importPath)
{
    var content = File.ReadAllText(importPath);
    var m = Regex.Match(content, @"^path=""res://(.+\.ctex)""", RegexOptions.Multiline);
    return m.Success ? m.Groups[1].Value : null;
}

static void ConvertToJpeg(string srcPath, string outPath)
{
    using var image = Image.Load<Rgba32>(srcPath);
    image.Mutate(x => x
        .Resize(JpegWidth, JpegHeight)
        .BackgroundColor(Color.White));
    image.SaveAsJpeg(outPath, new JpegEncoder { Quality = JpegQuality });
}
