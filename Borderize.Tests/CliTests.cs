using ExifLibrary;
using SkiaSharp;
using Xunit;

namespace Borderize.Tests;

// Console.Out/Error are process-global, so redirecting them to capture CLI output is not
// safe to do concurrently. Keep these tests in a non-parallel collection.
[CollectionDefinition("CLI", DisableParallelization = true)]
public class CliCollection { }

/// <summary>
/// End-to-end tests that drive the real CLI (arg parsing → InputResolver → processing
/// loop → exit code/console output) via <see cref="Cli.RunAsync"/> against synthesized
/// photos on disk. Fixtures are generated in-process (no external tools), matching the
/// approach in ProcessTests/ExifOrientationTests.
/// </summary>
[Collection("CLI")]
public class CliTests : IDisposable
{
    private readonly string _tempDir;

    public CliTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    /// <summary>Runs the CLI in-process, capturing stdout/stderr and the exit code.</summary>
    private static (int exit, string stdout, string stderr) Run(params string[] args)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            int exit = Cli.RunAsync(args).GetAwaiter().GetResult();
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private string CreateImage(string fileName, int width, int height, SKEncodedImageFormat format)
    {
        var path = Path.Combine(_tempDir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bmp = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bmp)) canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(format, 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }

    private string CreateOrientedJpeg(string fileName, int pixelWidth, int pixelHeight, ushort orientation)
    {
        var path = CreateImage(fileName, pixelWidth, pixelHeight, SKEncodedImageFormat.Jpeg);
        var file = ImageFile.FromFile(path);
        file.Properties.Set(ExifTag.Orientation, orientation);
        file.Save(path);
        return path;
    }

    private static string Output(string inputPath, string suffix = "-border")
        => BorderProcessor.BuildOutputPath(inputPath, suffix);

    [Fact]
    public void SingleFile_DefaultOptions_WritesBorderedOutput()
    {
        var path = CreateImage("photo.jpg", 100, 60, SKEncodedImageFormat.Jpeg);

        var (exit, stdout, _) = Run(path);

        Assert.Equal(0, exit);
        Assert.Contains("Processing 1 file(s)", stdout);
        Assert.True(File.Exists(Output(path)));
        using var result = SKBitmap.Decode(Output(path));
        // Default --size is 5% of the shorter dimension (60) = 3px on each side.
        Assert.Equal(106, result.Width);
        Assert.Equal(66, result.Height);
    }

    [Fact]
    public void StylePolaroid_BindsThroughParser_AndGivesTripleBottom()
    {
        var path = CreateImage("photo.jpg", 100, 60, SKEncodedImageFormat.Jpeg);

        var (exit, _, _) = Run(path, "--style", "polaroid", "--size", "10");

        Assert.Equal(0, exit);
        using var result = SKBitmap.Decode(Output(path));
        // sideSize=10 on all sides except bottom = 10*3 = 30.
        Assert.Equal(120, result.Width);  // 100 + 10 + 10
        Assert.Equal(100, result.Height); // 60 + 10 (top) + 30 (bottom)
    }

    [Fact]
    public void StyleAspect_PadsNonSquareToSquare()
    {
        var path = CreateImage("photo.jpg", 100, 60, SKEncodedImageFormat.Jpeg);

        var (exit, _, _) = Run(path, "--style", "aspect", "--aspect", "1:1", "--size", "5");

        Assert.Equal(0, exit);
        using var result = SKBitmap.Decode(Output(path));
        Assert.Equal(result.Width, result.Height);
    }

    [Fact]
    public void ColorOption_ChangesBorderColor()
    {
        // PNG keeps exact pixels (JPEG would alter the border color via lossy compression).
        var path = CreateImage("photo.png", 40, 40, SKEncodedImageFormat.Png);

        var (exit, _, _) = Run(path, "--color", "black", "--size", "5");

        Assert.Equal(0, exit);
        using var result = SKBitmap.Decode(Output(path));
        Assert.Equal(SKColors.Black, result.GetPixel(0, 0)); // corner is border
    }

    [Fact]
    public void DirectoryInput_BordersEveryImage()
    {
        CreateImage("a.jpg", 40, 40, SKEncodedImageFormat.Jpeg);
        CreateImage("b.png", 40, 40, SKEncodedImageFormat.Png);
        CreateImage("c.webp", 40, 40, SKEncodedImageFormat.Webp);

        var (exit, stdout, _) = Run(_tempDir, "--size", "5");

        Assert.Equal(0, exit);
        Assert.Contains("Processing 3 file(s)", stdout);
        Assert.True(File.Exists(Path.Combine(_tempDir, "a-border.jpg")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "b-border.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "c-border.webp")));
    }

    [Fact]
    public void Recursive_OnlyProcessesNestedFilesWhenFlagSet()
    {
        CreateImage("top.jpg", 40, 40, SKEncodedImageFormat.Jpeg);
        CreateImage(Path.Combine("sub", "nested.jpg"), 40, 40, SKEncodedImageFormat.Jpeg);

        // Without --recursive only the top-level file is processed.
        var (exit1, _, _) = Run(_tempDir, "--size", "5");
        Assert.Equal(0, exit1);
        Assert.True(File.Exists(Path.Combine(_tempDir, "top-border.jpg")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "sub", "nested-border.jpg")));

        // With --recursive the nested file is processed too.
        var (exit2, _, _) = Run(_tempDir, "--recursive", "--size", "5");
        Assert.Equal(0, exit2);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sub", "nested-border.jpg")));
    }

    [Fact]
    public void DryRun_WritesNothing_AndListsPlannedOutputs()
    {
        var path = CreateImage("photo.jpg", 40, 40, SKEncodedImageFormat.Jpeg);

        var (exit, stdout, _) = Run(path, "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains("Dry run", stdout);
        Assert.Contains("->", stdout);
        Assert.Contains(Output(path), stdout);
        Assert.False(File.Exists(Output(path)));
    }

    [Fact]
    public void Rerun_DoesNotBorderAlreadyBorderedOutput()
    {
        CreateImage("photo.jpg", 40, 40, SKEncodedImageFormat.Jpeg);

        var (exit1, _, _) = Run(_tempDir, "--size", "5");
        var (exit2, stdout2, _) = Run(_tempDir, "--size", "5");

        Assert.Equal(0, exit1);
        Assert.Equal(0, exit2);
        // Second pass re-processes photo.jpg only; it must not create photo-border-border.jpg.
        Assert.Contains("Processing 1 file(s)", stdout2);
        Assert.False(File.Exists(Path.Combine(_tempDir, "photo-border-border.jpg")));
        var files = Directory.GetFiles(_tempDir, "*.jpg");
        Assert.Equal(2, files.Length); // photo.jpg + photo-border.jpg
    }

    [Fact]
    public void NoMatchingFiles_ExitsZeroWithMessage()
    {
        var (exit, stdout, _) = Run(_tempDir);

        Assert.Equal(0, exit);
        Assert.Contains("No matching files found.", stdout);
    }

    [Fact]
    public void InvalidQuality_ExitsOne_WithError_AndWritesNothing()
    {
        var path = CreateImage("photo.jpg", 40, 40, SKEncodedImageFormat.Jpeg);

        var (exit, _, stderr) = Run(path, "--quality", "0");

        Assert.Equal(1, exit);
        Assert.Contains("Quality must be between 1 and 100", stderr);
        Assert.False(File.Exists(Output(path)));
    }

    [Fact]
    public void EmptySuffix_ExitsOne()
    {
        var path = CreateImage("photo.jpg", 40, 40, SKEncodedImageFormat.Jpeg);

        var (exit, _, stderr) = Run(path, "--suffix", "");

        Assert.Equal(1, exit);
        Assert.Contains("Suffix must not be empty", stderr);
    }

    [Fact]
    public void NonexistentInput_ExitsOne_WithError()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist.jpg");

        var (exit, _, stderr) = Run(missing);

        Assert.Equal(1, exit);
        Assert.Contains("Error:", stderr);
    }

    [Fact]
    public void ExifOrientedJpeg_ProducesUprightOutput_ThroughCli()
    {
        // Stored portrait (10x20) pixels, Orientation 6 = upright is landscape (20x10).
        var path = CreateOrientedJpeg("rotated.jpg", pixelWidth: 10, pixelHeight: 20, orientation: 6);

        var (exit, _, _) = Run(path, "--size", "2");

        Assert.Equal(0, exit);
        using var result = SKBitmap.Decode(Output(path));
        Assert.True(result.Width > result.Height,
            $"Expected landscape output, got {result.Width}x{result.Height}");
    }
}
