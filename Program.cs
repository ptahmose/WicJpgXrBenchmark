using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WicJpgXrBenchmark;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            Options.PrintUsage();
            return 1;
        }

        if (options.ShowHelp)
        {
            Options.PrintUsage();
            return 0;
        }

        PrintEnvironment();

        byte[]? encoded = null;
        BitmapSource? encodingInput = null;
        if (options.InputFile is not null)
        {
            encoded = File.ReadAllBytes(options.InputFile);
            Console.WriteLine($"Input file      : {options.InputFile} ({encoded.Length:N0} bytes)");
            if (options.BenchmarkEncoding)
            {
                encodingInput = LoadEncodingInput(encoded);
            }
        }
        else
        {
            var sw = Stopwatch.StartNew();
            encodingInput = CreateSyntheticImage(options);
            if (!options.EncodeOnly)
            {
                using var output = EncodeImage(encodingInput, options);
                encoded = output.ToArray();
            }
            sw.Stop();
            Console.WriteLine($"Generated image : {options.Width}x{options.Height} {options.PixelFormat}, " +
                              $"quality={options.Quality.ToString(CultureInfo.InvariantCulture)}, lossless={options.Lossless}, " +
                              $"preparation {sw.Elapsed.TotalMilliseconds:F1} ms" +
                              (encoded is null ? "" : $", {encoded.Length:N0} encoded bytes"));
            if (!options.BenchmarkEncoding && options.SaveFile is not null)
            {
                File.WriteAllBytes(options.SaveFile, encoded!);
                Console.WriteLine($"Saved to        : {options.SaveFile}");
            }
        }

        Console.WriteLine($"Warmup/Iter.    : {options.Warmup}/{options.Iterations}");
        Console.WriteLine();

        if (!options.EncodeOnly)
        {
            BenchmarkDecoding(encoded!, options);
        }

        if (options.BenchmarkEncoding)
        {
            BenchmarkEncoding(encodingInput!, options);
        }

        return 0;
    }

    private static void BenchmarkDecoding(byte[] encoded, Options options)
    {
        Console.WriteLine("=== Decoding ===");
        for (int i = 0; i < options.Warmup; i++)
        {
            DecodeOnce(encoded, out _);
        }

        var timings = new double[options.Iterations];
        BitmapInfo info = default;
        for (int i = 0; i < options.Iterations; i++)
        {
            timings[i] = DecodeOnce(encoded, out info);
            if (options.Verbose)
            {
                Console.WriteLine($"  #{i + 1,4}: {timings[i]:F3} ms");
            }
        }

        Console.WriteLine($"Decoded         : {info.Width}x{info.Height} {info.Format}, {info.Bytes:N0} bytes of pixel data");
        PrintStatistics(timings, info.Bytes);
    }

    private static void BenchmarkEncoding(BitmapSource bitmap, Options options)
    {
        int stride = checked((int)(((long)bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8));
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);

        Console.WriteLine();
        Console.WriteLine("=== Encoding ===");
        Console.WriteLine($"Input pixels    : {bitmap.PixelWidth}x{bitmap.PixelHeight} {bitmap.Format}, {pixels.Length:N0} bytes");
        Console.WriteLine($"Pixel SHA-256   : {Convert.ToHexString(SHA256.HashData(pixels))}");
        Console.WriteLine($"Compression     : {(options.Lossless ? "lossless" : $"lossy, quality={options.Quality.ToString(CultureInfo.InvariantCulture)}")}");
        Console.WriteLine("Timed operation : encoder creation and encoding to memory");

        for (int i = 0; i < options.Warmup; i++)
        {
            using var output = EncodeImage(bitmap, options);
        }

        var timings = new double[options.Iterations];
        long encodedSize = 0;
        for (int i = 0; i < options.Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            using var output = EncodeImage(bitmap, options);
            sw.Stop();
            timings[i] = sw.Elapsed.TotalMilliseconds;
            encodedSize = output.Length;
            if (options.Verbose)
            {
                Console.WriteLine($"  #{i + 1,4}: {timings[i]:F3} ms");
            }

            if (i == options.Iterations - 1 && options.SaveFile is not null)
            {
                File.WriteAllBytes(options.SaveFile, output.ToArray());
                Console.WriteLine($"Saved to        : {options.SaveFile}");
            }
        }

        Console.WriteLine($"Encoded         : {encodedSize:N0} bytes");
        PrintStatistics(timings, pixels.LongLength);
    }

    /// <summary>
    /// Decodes the JPEG-XR stream with WPF's WIC wrapper and copies out all pixels so that the
    /// (lazy) decode is fully executed. Returns the elapsed time in milliseconds.
    /// </summary>
    private static double DecodeOnce(byte[] encoded, out BitmapInfo info)
    {
        var sw = Stopwatch.StartNew();
        using var stream = new MemoryStream(encoded, writable: false);
        var decoder = new WmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
        BitmapSource frame = decoder.Frames[0];
        int stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(pixels, stride, 0);
        sw.Stop();

        info = new BitmapInfo(frame.PixelWidth, frame.PixelHeight, frame.Format, pixels.LongLength);
        return sw.Elapsed.TotalMilliseconds;
    }

    private static BitmapSource LoadEncodingInput(byte[] encoded)
    {
        using var stream = new MemoryStream(encoded, writable: false);
        var decoder = new WmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames[0];
        int stride = checked((int)(((long)frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8));
        var pixels = new byte[checked(stride * frame.PixelHeight)];
        frame.CopyPixels(pixels, stride, 0);
        return BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, frame.DpiX, frame.DpiY, frame.Format, frame.Palette, pixels, stride);
    }

    private static BitmapSource CreateSyntheticImage(Options options)
    {
        PixelFormat format = options.PixelFormat;
        int bytesPerPixel = (format.BitsPerPixel + 7) / 8;
        int stride = options.Width * bytesPerPixel;
        var pixels = new byte[stride * options.Height];
        FillSyntheticPattern(pixels, options.Width, options.Height, stride, bytesPerPixel, format == PixelFormats.Gray16);

        return BitmapSource.Create(options.Width, options.Height, 96, 96, format, null, pixels, stride);
    }

    private static MemoryStream EncodeImage(BitmapSource bitmap, Options options)
    {
        var encoder = new WmpBitmapEncoder
        {
            Lossless = options.Lossless,
            ImageQualityLevel = options.Quality,
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var output = new MemoryStream();
        encoder.Save(output);
        return output;
    }

    /// <summary>
    /// Creates microscopy-like content (smooth structures plus deterministic noise) so that the
    /// codec has a realistic amount of work to do; a constant image would compress trivially.
    /// </summary>
    private static void FillSyntheticPattern(byte[] pixels, int width, int height, int stride, int bytesPerPixel, bool is16Bit)
    {
        var random = new Random(12345);
        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                double signal = 0.5 + 0.25 * Math.Sin(x * 0.013) * Math.Cos(y * 0.017) + 0.15 * Math.Sin((x + y) * 0.05);
                int offset = row + x * bytesPerPixel;
                if (is16Bit)
                {
                    int value = Math.Clamp((int)(signal * 60000) + random.Next(-800, 800), 0, 65535);
                    pixels[offset] = (byte)value;
                    pixels[offset + 1] = (byte)(value >> 8);
                }
                else
                {
                    for (int c = 0; c < bytesPerPixel; c++)
                    {
                        pixels[offset + c] = c == 3
                            ? (byte)255
                            : (byte)Math.Clamp((int)(signal * (230 - 20 * c)) + random.Next(-8, 8), 0, 255);
                    }
                }
            }
        }
    }

    private static void PrintEnvironment()
    {
        Console.WriteLine("=== WIC JPEG-XR benchmark ===");
        Console.WriteLine($"OS              : {Environment.OSVersion.VersionString} (UBR {GetWindowsUbr()})");
        Console.WriteLine($"Runtime         : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {(Environment.Is64BitProcess ? "x64" : "x86")}");
        foreach (string dll in new[] { "wmphoto.dll", "WindowsCodecs.dll" })
        {
            string path = Path.Combine(Environment.SystemDirectory, dll);
            string version = File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion ?? "?" : "not found";
            Console.WriteLine($"{dll,-16}: {version}");
        }
    }

    private static string GetWindowsUbr()
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("UBR")?.ToString() ?? "?";
    }

    private static void PrintStatistics(double[] timings, long pixelBytes)
    {
        var sorted = timings.OrderBy(t => t).ToArray();
        double mean = sorted.Average();
        double stdDev = Math.Sqrt(sorted.Sum(t => (t - mean) * (t - mean)) / Math.Max(1, sorted.Length - 1));
        double Percentile(double p) => sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)];

        Console.WriteLine($"Min             : {sorted[0]:F3} ms");
        Console.WriteLine($"Median          : {Percentile(0.5):F3} ms");
        Console.WriteLine($"Mean ± StdDev   : {mean:F3} ± {stdDev:F3} ms");
        Console.WriteLine($"P95             : {Percentile(0.95):F3} ms");
        Console.WriteLine($"Max             : {sorted[^1]:F3} ms");
        Console.WriteLine($"Throughput      : {pixelBytes / (1024.0 * 1024.0) / (Percentile(0.5) / 1000.0):F1} MiB/s (median)");
    }

    private readonly record struct BitmapInfo(int Width, int Height, PixelFormat Format, long Bytes);
}

internal sealed class Options
{
    public string? InputFile { get; private set; }
    public string? SaveFile { get; private set; }
    public int Width { get; private set; } = 2048;
    public int Height { get; private set; } = 2048;
    public PixelFormat PixelFormat { get; private set; } = PixelFormats.Gray16;
    public float Quality { get; private set; } = 0.9f;
    public bool Lossless { get; private set; }
    public int Iterations { get; private set; } = 50;
    public int Warmup { get; private set; } = 5;
    public bool Verbose { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool BenchmarkEncoding { get; private set; }
    public bool EncodeOnly { get; private set; }

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}");
            switch (args[i].ToLowerInvariant())
            {
                case "-i": case "--input": o.InputFile = Next(); break;
                case "-s": case "--save": o.SaveFile = Next(); break;
                case "-w": case "--width": o.Width = ParsePositive(Next()); break;
                case "-h": case "--height": o.Height = ParsePositive(Next()); break;
                case "-f": case "--format": o.PixelFormat = ParseFormat(Next()); break;
                case "-q": case "--quality": o.Quality = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "-l": case "--lossless": o.Lossless = true; break;
                case "-n": case "--iterations": o.Iterations = ParsePositive(Next()); break;
                case "--warmup": o.Warmup = Math.Max(0, int.Parse(Next(), CultureInfo.InvariantCulture)); break;
                case "-v": case "--verbose": o.Verbose = true; break;
                case "--encode": o.BenchmarkEncoding = true; break;
                case "--encode-only": o.BenchmarkEncoding = true; o.EncodeOnly = true; break;
                case "-?": case "--help": o.ShowHelp = true; break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (o.Quality is < 0f or > 1f)
        {
            throw new ArgumentException("Quality must be in the range 0..1");
        }

        return o;
    }

    private static int ParsePositive(string s)
    {
        int value = int.Parse(s, CultureInfo.InvariantCulture);
        return value > 0 ? value : throw new ArgumentException($"Value must be positive: {s}");
    }

    private static PixelFormat ParseFormat(string s) => s.ToLowerInvariant() switch
    {
        "gray8" => PixelFormats.Gray8,
        "gray16" => PixelFormats.Gray16,
        "bgr24" => PixelFormats.Bgr24,
        "bgra32" => PixelFormats.Bgra32,
        _ => throw new ArgumentException($"Unsupported pixel format: {s} (use gray8, gray16, bgr24, bgra32)"),
    };

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Usage: WicJpgXrBenchmark [options]
              -i, --input <file>       Decode an existing JPEG-XR (.jxr/.wdp) file instead of generating one
              -s, --save <file>        Save generated JPEG-XR, or the last measured encoding with --encode
              -w, --width <px>         Width of generated image (default 2048)
              -h, --height <px>        Height of generated image (default 2048)
              -f, --format <fmt>       gray8 | gray16 | bgr24 | bgra32 (default gray16)
              -q, --quality <0..1>     JPEG-XR quality level (default 0.9)
              -l, --lossless           Encode losslessly
              -n, --iterations <n>     Number of measured operations per benchmark (default 50)
                  --warmup <n>         Number of warmup operations per benchmark (default 5)
                  --encode             Benchmark encoding as well as decoding
                  --encode-only        Benchmark encoding without decoding timings
              -v, --verbose            Print every single timing
              -?, --help               Show this help
            """);
    }
}
