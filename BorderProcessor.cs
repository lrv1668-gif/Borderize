using Borderize.Models;
using SkiaSharp;

namespace Borderize;

public static class BorderProcessor
{
    public static void Process(string inputPath, BorderOptions options)
    {
        using var codec = SKCodec.Create(inputPath)
            ?? throw new InvalidOperationException($"Could not decode image: {inputPath}");
        var decoded = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException($"Could not decode image: {inputPath}");
        using var original = OrientUpright(decoded, codec.EncodedOrigin);

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

    /// <summary>
    /// Returns an upright copy of <paramref name="decoded"/> by applying the
    /// image's EXIF orientation (SkiaSharp's decoders do not apply it). The four
    /// transpose/rotate origins swap width and height. For an already-upright
    /// image the input is returned unchanged; otherwise the input is disposed and
    /// a new bitmap is returned, so the caller owns exactly one bitmap either way.
    /// </summary>
    public static SKBitmap OrientUpright(SKBitmap decoded, SKEncodedOrigin origin)
    {
        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int dw = swap ? decoded.Height : decoded.Width;
        int dh = swap ? decoded.Width : decoded.Height;

        // Maps source pixel coordinates to the upright destination. dw/dh are the
        // destination dimensions; mirrors Skia's SkEncodedOriginToMatrix.
        SKMatrix matrix;
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:    matrix = M(-1, 0, dw, 0, 1, 0); break;   // flip horizontal
            case SKEncodedOrigin.BottomRight: matrix = M(-1, 0, dw, 0, -1, dh); break; // rotate 180
            case SKEncodedOrigin.BottomLeft:  matrix = M(1, 0, 0, 0, -1, dh); break;   // flip vertical
            case SKEncodedOrigin.LeftTop:     matrix = M(0, 1, 0, 1, 0, 0); break;     // transpose
            case SKEncodedOrigin.RightTop:    matrix = M(0, -1, dw, 1, 0, 0); break;   // rotate 90 CW
            case SKEncodedOrigin.RightBottom: matrix = M(0, -1, dw, -1, 0, dh); break; // transverse
            case SKEncodedOrigin.LeftBottom:  matrix = M(0, 1, 0, -1, 0, dh); break;   // rotate 90 CCW
            default:                          return decoded;                          // TopLeft / unknown — already upright
        }

        var upright = new SKBitmap(dw, dh, decoded.ColorType, decoded.AlphaType);
        using (var canvas = new SKCanvas(upright))
        {
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(decoded, new SKPoint(0, 0), new SKSamplingOptions(SKFilterMode.Nearest));
        }
        decoded.Dispose();
        return upright;
    }

    static SKMatrix M(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY)
        => new() { ScaleX = scaleX, SkewX = skewX, TransX = transX, SkewY = skewY, ScaleY = scaleY, TransY = transY, Persp2 = 1 };

    public static (int top, int right, int bottom, int left) ComputeBorders(int width, int height, BorderOptions options)
    {
        int shorter = Math.Min(width, height);
        int sideSize = OptionParsing.ParseSize(options.Size, shorter);

        switch (options.Style)
        {
            case BorderStyle.Polaroid:
                int bottomSize = options.Bottom is not null
                    ? OptionParsing.ParseSize(options.Bottom, shorter)
                    : sideSize * 3;
                return (sideSize, sideSize, bottomSize, sideSize);

            case BorderStyle.Aspect:
                return ComputeAspectBorders(width, height, sideSize, options.Aspect);

            default: // Uniform
                int overrideBottom = options.Bottom is not null
                    ? OptionParsing.ParseSize(options.Bottom, shorter)
                    : sideSize;
                return (sideSize, sideSize, overrideBottom, sideSize);
        }
    }

    /// <summary>
    /// Pads the image, centered, to the target aspect ratio with at least
    /// <paramref name="side"/> margin on every edge.
    /// </summary>
    static (int top, int right, int bottom, int left) ComputeAspectBorders(int width, int height, int side, (int W, int H) aspect)
    {
        int baseW = width + 2 * side;
        int baseH = height + 2 * side;
        double target = (double)aspect.W / aspect.H;

        if ((double)baseW / baseH < target)
        {
            // Too narrow — widen, splitting the extra between left and right.
            int extra = (int)Math.Round(baseH * target) - baseW;
            int leftExtra = extra / 2;
            return (side, side + (extra - leftExtra), side, side + leftExtra);
        }
        else
        {
            // Too wide — heighten, splitting the extra between top and bottom.
            int extra = (int)Math.Round(baseW / target) - baseH;
            int topExtra = extra / 2;
            return (side + topExtra, side, side + (extra - topExtra), side);
        }
    }

    public static string BuildOutputPath(string inputPath, string suffix)
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
