using Borderize;
using Borderize.Models;
using ExifLibrary;
using SkiaSharp;
using Xunit;

namespace Borderize.Tests;

public class ExifOrientationTests : IDisposable
{
    private readonly string _tempDir;

    public ExifOrientationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    /// <summary>
    /// Writes a JPEG with the given pixel dimensions and an EXIF Orientation tag.
    /// The stored pixels are <paramref name="pixelWidth"/> x <paramref name="pixelHeight"/>;
    /// the orientation tag tells viewers how to rotate them to upright.
    /// </summary>
    private string CreateOrientedJpeg(int pixelWidth, int pixelHeight, ushort orientation)
    {
        var path = Path.Combine(_tempDir, $"{Path.GetRandomFileName()}.jpg");

        using (var bmp = new SKBitmap(pixelWidth, pixelHeight))
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            canvas.Flush();
            using var image = SKImage.FromBitmap(bmp);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var stream = File.Create(path);
            data.SaveTo(stream);
        }

        var file = ImageFile.FromFile(path);
        file.Properties.Set(ExifTag.Orientation, orientation);
        file.Save(path);
        return path;
    }

    [Fact]
    public void SkiaCodec_ReadsExifOrientation_AsRightTop()
    {
        // EXIF Orientation 6 = rotate 90° clockwise to display upright.
        var path = CreateOrientedJpeg(pixelWidth: 10, pixelHeight: 20, orientation: 6);

        using var codec = SKCodec.Create(path);
        Assert.Equal(SKEncodedOrigin.RightTop, codec.EncodedOrigin);
    }

    [Fact]
    public void Process_AppliesExifOrientation_SoPortraitPixelsBecomeLandscapeOutput()
    {
        // Stored pixels are portrait (10x20) but Orientation 6 means the upright
        // image is landscape (20x10). With a symmetric uniform border, the output
        // must be wider than tall — and would be the opposite if orientation were ignored.
        var path = CreateOrientedJpeg(pixelWidth: 10, pixelHeight: 20, orientation: 6);

        var options = new BorderOptions(
            BorderStyle.Uniform, Size: "2", Bottom: null, Aspect: (1, 1),
            SKColors.White, Suffix: "-border", Recursive: false,
            Quality: 95, DryRun: false, Verbose: false);

        BorderProcessor.Process(path, options);

        var outputPath = BorderProcessor.BuildOutputPath(path, "-border");
        using var result = SKBitmap.Decode(outputPath);
        Assert.True(result.Width > result.Height,
            $"Expected landscape output, got {result.Width}x{result.Height}");
    }
}
