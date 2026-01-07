using System.Buffers.Binary;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;

namespace Png2Ico;
class Program
{
static int Main(string[] args)
{
    if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
    {
        PrintHelp();
        return 0;
    }

    try
    {
        var (input, output, sizes) = ParseArgs(args);

        if (!File.Exists(input))
            throw new FileNotFoundException("Input PNG not found.", input);

        using Image<Rgba32> src = Image.Load<Rgba32>(input);

        // Create PNG-encoded frames for each requested size
        var frames = new List<IcoFrame>();
        foreach (int size in sizes.Distinct().OrderBy(s => s))
        {
            if (size <= 0 || size > 256) throw new ArgumentOutOfRangeException(nameof(sizes), "Sizes must be 1..256.");

            using var img = src.Clone(ctx =>
            {
                // ICO expects square icons typically; we will resize to fit inside and pad to square.
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max,     // keep aspect ratio
                    Sampler = KnownResamplers.Lanczos3
                });
            });

            // Pad/canvas to exact square (size x size), centered
            using var square = new Image<Rgba32>(size, size);
            square.Mutate(ctx =>
            {
                ctx.BackgroundColor(Color.Transparent);
                int x = (size - img.Width) / 2;
                int y = (size - img.Height) / 2;
                ctx.DrawImage(img, new Point(x, y), 1f);
            });

            // Encode as PNG (ICO can embed PNG images)
            using var ms = new MemoryStream();
            square.Save(ms, new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                CompressionLevel = PngCompressionLevel.BestCompression
            });

            frames.Add(new IcoFrame(size, size, ms.ToArray()));
        }

        WriteIco(output, frames);

        Console.WriteLine($"OK: '{input}' -> '{output}' (sizes: {string.Join(",", sizes)})");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("ERROR: " + ex.Message);
        return 1;
    }
}

static void PrintHelp()
{
    Console.WriteLine("""
png2ico - Convert PNG to ICO (multi-size)

Usage:
  png2ico --in input.png --out output.ico [--sizes 16,24,32,48,64,128,256]

Options:
  --in,  -i      Input PNG path (required)
  --out, -o      Output ICO path (required)
  --sizes        Comma-separated list of sizes (default: 16,24,32,48,64,128,256)

Notes:
  - Output ICO embeds PNG frames (Vista+ compatible; works in modern Windows & browsers).
  - Transparency is preserved.

Examples:
  png2ico -i logo.png -o favicon.ico
  png2ico -i logo.png -o app.ico --sizes 16,32,48,256
""");
}

static (string input, string output, List<int> sizes) ParseArgs(string[] args)
{
    string? input = null;
    string? output = null;
    List<int> sizes = new() { 16, 24, 32, 48, 64, 128, 256 };

    for (int i = 0; i < args.Length; i++)
    {
        string a = args[i];

        string Next()
        {
            if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for '{a}'.");
            return args[++i];
        }

        switch (a)
        {
            case "--in":
            case "-i":
                input = Next();
                break;

            case "--out":
            case "-o":
                output = Next();
                break;

            case "--sizes":
                var raw = Next();
                sizes = raw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.Parse(s, CultureInfo.InvariantCulture))
                    .ToList();
                if (sizes.Count == 0) throw new ArgumentException("Sizes list is empty.");
                break;

            default:
                throw new ArgumentException($"Unknown argument: {a}");
        }
    }

    if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Missing --in / -i");
    if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("Missing --out / -o");

    return (input, output, sizes);
}

static void WriteIco(string outputPath, List<IcoFrame> frames)
{
    // ICO structure:
    // ICONDIR (6 bytes)
    // ICONDIRENTRY[n] (16 bytes each)
    // image data blobs...

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

    using var fs = File.Open(outputPath, FileMode.Create, FileAccess.Write);
    using var bw = new BinaryWriter(fs);

    // ICONDIR
    bw.Write((ushort)0);              // Reserved
    bw.Write((ushort)1);              // Type 1 = icon
    bw.Write((ushort)frames.Count);   // Count

    long dirEntriesStart = fs.Position;
    // Reserve space for directory entries
    bw.Write(new byte[16 * frames.Count]);

    // Write image data and remember offsets
    var entries = new List<(IcoFrame frame, uint bytesInRes, uint imageOffset)>(frames.Count);

    foreach (var frame in frames)
    {
        uint offset = checked((uint)fs.Position);
        bw.Write(frame.PngData);
        uint size = checked((uint)frame.PngData.Length);
        entries.Add((frame, size, offset));
    }

    // Go back and write ICONDIRENTRY list
    fs.Position = dirEntriesStart;

    for (int i = 0; i < entries.Count; i++)
    {
        var (frame, bytesInRes, imageOffset) = entries[i];

        // Width/Height fields are 1 byte; 0 means 256
        bw.Write((byte)(frame.Width == 256 ? 0 : frame.Width));
        bw.Write((byte)(frame.Height == 256 ? 0 : frame.Height));

        bw.Write((byte)0);            // ColorCount (0 if no palette)
        bw.Write((byte)0);            // Reserved

        bw.Write((ushort)1);          // Planes (usually 1)
        bw.Write((ushort)32);         // BitCount (we embed PNG with alpha; keep 32)

        bw.Write(bytesInRes);         // BytesInRes
        bw.Write(imageOffset);        // ImageOffset
    }
}

readonly record struct IcoFrame(int Width, int Height, byte[] PngData);
}
