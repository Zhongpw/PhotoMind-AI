using ImageMagick;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Infrastructure.ImageProcessing;
using PhotoMind.Infrastructure.Services;

int pass = 0, fail = 0;

void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"PASS: {name}"); }
    else { fail++; Console.WriteLine($"FAIL: {name} {detail}"); }
}

var dataDir = Environment.GetEnvironmentVariable("PHOTOMIND_DATA_DIR")
    ?? Path.Combine(Path.GetTempPath(), "photomind-format-smoke-" + Guid.NewGuid().ToString("N"));
if (!Directory.Exists(dataDir))
    Directory.CreateDirectory(dataDir);

var paths = new AppPaths();
paths.EnsureCreated();

// --- Report HEIC read/write support in the native Magick build ---
var heicFormats = MagickNET.SupportedFormats.Where(f => f.Format == MagickFormat.Heic).ToList();
foreach (var f in heicFormats)
    Console.WriteLine($"INFO: HEIC format support read={f.SupportsReading} write={f.SupportsWriting}");

// --- Generate TIFF via Magick.NET ---
var tiffPath = Path.Combine(dataDir, "test.tiff");
using (var t = new MagickImage(MagickColors.Red, 200, 150)) { t.Format = MagickFormat.Tiff; t.Write(tiffPath); }
Check("generated TIFF", File.Exists(tiffPath) && new FileInfo(tiffPath).Length > 0);

// --- HEIC sample (real file downloaded from filesamples.com) ---
var heicPath = Environment.GetEnvironmentVariable("HEIC_SAMPLE") ?? "";
var hasHeic = !string.IsNullOrEmpty(heicPath) && File.Exists(heicPath);
if (hasHeic)
    Check("HEIC sample present", new FileInfo(heicPath).Length > 0);
else
    Console.WriteLine("INFO: no HEIC sample; skipping HEIC decode checks");

byte[] ReadHeader(string p) { using var fs = File.OpenRead(p); var b = new byte[16]; int n = fs.Read(b, 0, 16); Array.Resize(ref b, n); return b; }

var detector = new ImageFormatDetector();

// --- Format detection ---
Check("detect TIFF", detector.Detect(ReadHeader(tiffPath)) == ImageFormat.Tiff);
if (hasHeic)
    Check("detect HEIC", detector.Detect(ReadHeader(heicPath)) == ImageFormat.Heic);

Check("IsSupported Tiff", detector.IsSupported(ImageFormat.Tiff));
Check("IsSupported Heic", detector.IsSupported(ImageFormat.Heic));
Check("IsSupported Raw", detector.IsSupported(ImageFormat.Raw));

// --- RAW detection (synthetic headers) ---
Check("detect RAF", detector.Detect("FUJIFILMCCD-RAW"u8) == ImageFormat.Raw);
Check("detect ORF", detector.Detect("IIRO\x08\x00"u8) == ImageFormat.Raw);
Check("detect RW2", detector.Detect("IIU\x00\x24\x00"u8) == ImageFormat.Raw);
Check("detect CR2", detector.Detect(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x10, 0x00, 0x00, 0x00, (byte)'C', (byte)'R', 0x02, 0x00 }) == ImageFormat.Raw);
Check("detect NEF", detector.Detect(new byte[] { 0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, (byte)'N', (byte)'E', (byte)'F', 0x00 }) == ImageFormat.Raw);

// --- Metadata ---
var reader = new ImageMetadataReader();
var tiffMeta = await reader.ReadAsync(tiffPath, ImageFormat.Tiff);
Check("TIFF metadata 200x150", tiffMeta.Width == 200 && tiffMeta.Height == 150, $"{tiffMeta.Width}x{tiffMeta.Height}");

if (hasHeic)
{
    var heicMeta = await reader.ReadAsync(heicPath, ImageFormat.Heic);
    Check("HEIC metadata >0", heicMeta.Width > 0 && heicMeta.Height > 0, $"{heicMeta.Width}x{heicMeta.Height}");
}

// --- Thumbnails ---
var thumb = new ThumbnailService(paths, detector);
var tiffThumb = await thumb.GetOrCreateThumbnailAsync(tiffPath);
Check("TIFF thumbnail JPEG", tiffThumb != null && File.Exists(tiffThumb) && new FileInfo(tiffThumb!).Length > 0);

if (hasHeic)
{
    var heicThumb = await thumb.GetOrCreateThumbnailAsync(heicPath);
    Check("HEIC thumbnail JPEG", heicThumb != null && File.Exists(heicThumb) && new FileInfo(heicThumb!).Length > 0);
}

// --- AI previews ---
var normalizer = new ImageNormalizer(paths, detector);
var cfg = new AiConfig { MaxPreviewDimension = 1024, PreviewQuality = 85 };

var tiffPreview = await normalizer.CreatePreviewAsync(tiffPath, cfg);
Check("TIFF preview JPEG", File.Exists(tiffPreview.TempFilePath) && new FileInfo(tiffPreview.TempFilePath).Length > 0);

if (hasHeic)
{
    var heicPreview = await normalizer.CreatePreviewAsync(heicPath, cfg);
    Check("HEIC preview JPEG", File.Exists(heicPreview.TempFilePath) && new FileInfo(heicPreview.TempFilePath).Length > 0);
}

Console.WriteLine($"\nRESULT: {pass} PASS, {fail} FAIL");
return fail == 0 ? 0 : 1;
