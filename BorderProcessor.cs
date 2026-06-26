using Borderize.Models;
using SkiaSharp;

namespace Borderize;

static class BorderProcessor
{
    public static void Process(string inputPath, BorderOptions options)
    {
        using var original = SKBitmap.Decode(inputPath)
            ?? throw new InvalidOperationException($"Could not decode image: {inputPath}");

        var (top, right, bottom, left) = ComputeBorders(original.Width, original.Height, options);

        int canvasWidth = original.Width + left + right;
        int canvasHeight = original.Height + top + bottom;

        using var canvasBitmap = new SKBitmap(canvasWidth, canvasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(canvasBitmap);

        canvas.Clear(options.Color);
        canvas.DrawBitmap(original, new SKPoint(left, top), new SKSamplingOptions(SKFilterMode.Nearest));
        canvas.Flush();

        var outputPath = BuildOutputPath(inputPath, options.Suffix);

        using var image = SKImage.FromBitmap(canvasBitmap);
        Save(image, outputPath, inputPath, options.Quality);
    }

    static (int top, int right, int bottom, int left) ComputeBorders(int width, int height, BorderOptions options)
    {
        int sideSize = ParseSize(options.Size, Math.Min(width, height));

        if (options.Style == BorderStyle.Polaroid)
        {
            int bottomSize = options.Bottom is not null
                ? ParseSize(options.Bottom, Math.Min(width, height))
                : sideSize * 3;
            return (sideSize, sideSize, bottomSize, sideSize);
        }

        int overrideBottom = options.Bottom is not null
            ? ParseSize(options.Bottom, Math.Min(width, height))
            : sideSize;
        return (sideSize, sideSize, overrideBottom, sideSize);
    }

    static int ParseSize(string value, int referenceDimension)
    {
        if (value.EndsWith('%'))
        {
            if (!double.TryParse(value[..^1], out double pct))
                throw new ArgumentException($"Invalid percentage: {value}");
            return Math.Max(1, (int)Math.Round(referenceDimension * pct / 100.0));
        }
        if (!int.TryParse(value, out int px))
            throw new ArgumentException($"Invalid size value: {value}");
        return px;
    }

    static string BuildOutputPath(string inputPath, string suffix)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        return Path.Combine(dir, $"{name}{suffix}{ext}");
    }

    static void Save(SKImage image, string outputPath, string sourcePath, int quality)
    {
        var format = Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg,
        };

        using var data = image.Encode(format, quality);
        using var stream = File.Create(outputPath);
        data.SaveTo(stream);
    }
}
