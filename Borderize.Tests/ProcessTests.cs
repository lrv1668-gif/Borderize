using Borderize;
using Borderize.Models;
using SkiaSharp;
using Xunit;

namespace Borderize.Tests;

public class ProcessTests : IDisposable
{
    private readonly string _tempDir;

    public ProcessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static BorderOptions UniformOptions(string size = "10", string suffix = "-border")
        => new(BorderStyle.Uniform, size, Bottom: null, Aspect: (1, 1), SKColors.White,
            suffix, Recursive: false, Quality: 95, DryRun: false, Verbose: false);

    private string CreateImage(string fileName, int width, int height, SKEncodedImageFormat format)
    {
        var path = Path.Combine(_tempDir, fileName);
        using var bmp = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bmp)) canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(format, 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }

    [Fact]
    public void Process_Uniform_AddsBorderToEveryEdge()
    {
        var path = CreateImage("photo.jpg", 100, 60, SKEncodedImageFormat.Jpeg);

        BorderProcessor.Process(path, UniformOptions(size: "10"));

        using var result = SKBitmap.Decode(BorderProcessor.BuildOutputPath(path, "-border"));
        Assert.Equal(120, result.Width);   // 100 + 10 + 10
        Assert.Equal(80, result.Height);   // 60 + 10 + 10
    }

    [Theory]
    [InlineData("photo.png", SKEncodedImageFormat.Png)]
    [InlineData("photo.webp", SKEncodedImageFormat.Webp)]
    [InlineData("photo.jpg", SKEncodedImageFormat.Jpeg)]
    public void Process_PreservesSourceFormat(string fileName, SKEncodedImageFormat expected)
    {
        var path = CreateImage(fileName, 40, 40, expected);

        BorderProcessor.Process(path, UniformOptions());

        using var codec = SKCodec.Create(BorderProcessor.BuildOutputPath(path, "-border"));
        Assert.Equal(expected, codec.EncodedFormat);
    }

    [Fact]
    public void Process_Output_IsRecognizedAsAlreadyBordered_OnRerun()
    {
        CreateImage("photo.jpg", 40, 40, SKEncodedImageFormat.Jpeg);
        var path = Path.Combine(_tempDir, "photo.jpg");

        BorderProcessor.Process(path, UniformOptions());

        // A second pass over the directory must not re-border the output we just wrote.
        var second = InputResolver.Resolve(_tempDir, recursive: false, suffix: "-border").ToList();
        Assert.Single(second);
        Assert.Equal("photo.jpg", Path.GetFileName(second[0]));
    }
}
