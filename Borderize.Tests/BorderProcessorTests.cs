using Borderize;
using Borderize.Models;
using SkiaSharp;
using Xunit;

namespace Borderize.Tests;

public class BorderProcessorTests
{
    static BorderOptions Options(
        BorderStyle style,
        string size = "5%",
        string? bottom = null,
        (int W, int H)? aspect = null)
        => new(style, size, bottom, aspect ?? (1, 1), SKColors.White, "-border",
            Recursive: false, Quality: 95, DryRun: false, Verbose: false);

    [Fact]
    public void Uniform_AppliesEqualBordersOnAllSides()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Uniform, "60"));
        Assert.Equal((60, 60, 60, 60), (top, right, bottom, left));
    }

    [Fact]
    public void Uniform_BottomOverride_AffectsOnlyBottom()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Uniform, "60", bottom: "100"));
        Assert.Equal((60, 60, 100, 60), (top, right, bottom, left));
    }

    [Fact]
    public void Polaroid_BottomOverride_ReplacesThreeTimesDefault()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Polaroid, "40", bottom: "100"));
        Assert.Equal((40, 40, 100, 40), (top, right, bottom, left));
    }

    [Fact]
    public void Polaroid_BottomDefaultsToThreeTimesSide()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Polaroid, "40"));
        Assert.Equal((40, 40, 120, 40), (top, right, bottom, left));
    }

    [Fact]
    public void Polaroid_PercentageSize_ScalesWithShorterDimension()
    {
        // 5% of min(1200, 800) = 40, bottom = 3 * 40 = 120
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Polaroid, "5%"));
        Assert.Equal((40, 40, 120, 40), (top, right, bottom, left));
    }

    [Fact]
    public void Aspect_Square_PadsLandscapeToEqualSides()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Aspect, "40"));

        // Canvas becomes 1280 x 1280 (square), image centered.
        Assert.Equal(1200 + left + right, 800 + top + bottom);
        Assert.Equal(1280, 1200 + left + right);
        Assert.Equal((240, 40, 240, 40), (top, right, bottom, left));
    }

    [Fact]
    public void Aspect_Square_PadsPortraitToEqualSides()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(800, 1200, Options(BorderStyle.Aspect, "40"));

        Assert.Equal(800 + left + right, 1200 + top + bottom);
        Assert.Equal(1280, 800 + left + right);
        Assert.Equal((40, 240, 40, 240), (top, right, bottom, left));
    }

    [Fact]
    public void Aspect_FourFive_ProducesTargetRatio()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Aspect, "40", aspect: (4, 5)));

        int width = 1200 + left + right;
        int height = 800 + top + bottom;
        Assert.Equal(4.0 / 5.0, (double)width / height, precision: 3);
    }

    [Fact]
    public void Aspect_NeverShrinksBelowMinimumMargin()
    {
        var (top, right, bottom, left) = BorderProcessor.ComputeBorders(1200, 800, Options(BorderStyle.Aspect, "40"));
        Assert.True(top >= 40 && right >= 40 && bottom >= 40 && left >= 40);
    }

    [Fact]
    public void BuildOutputPath_InsertsSuffixBeforeExtension()
    {
        var path = Path.Combine("photos", "IMG_0042.jpg");
        var result = BorderProcessor.BuildOutputPath(path, "-border");
        Assert.Equal("IMG_0042-border.jpg", Path.GetFileName(result));
        Assert.Equal(Path.GetDirectoryName(path), Path.GetDirectoryName(result));
    }

    [Fact]
    public void OrientUpright_TopLeft_ReturnsSameBitmapUnchanged()
    {
        var bmp = new SKBitmap(2, 4);
        var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.TopLeft);

        Assert.Same(bmp, result);
        Assert.Equal(2, result.Width);
        Assert.Equal(4, result.Height);
        bmp.Dispose();
    }

    [Fact]
    public void OrientUpright_RotateOrigin_SwapsWidthAndHeight()
    {
        var bmp = new SKBitmap(2, 4);
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.RightTop);

        Assert.Equal(4, result.Width);
        Assert.Equal(2, result.Height);
    }

    [Fact]
    public void OrientUpright_RightTop_MovesPixelToRotatedPosition()
    {
        // 2x4 source, all white except a red pixel at top-left (0,0).
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 0, SKColors.Red);

        // RightTop = 90° clockwise. Source (x,y) -> dest (sh-1-y, x); (0,0) -> (3,0).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.RightTop);

        Assert.Equal(SKColors.Red, result.GetPixel(3, 0));
        Assert.Equal(SKColors.White, result.GetPixel(0, 0));
    }

    [Fact]
    public void OrientUpright_TopRight_MirrorsHorizontallyWithoutSwappingDimensions()
    {
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 0, SKColors.Red);

        // TopRight = horizontal flip. Source (x,y) -> dest (sw-1-x, y); (0,0) -> (1,0).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.TopRight);

        Assert.Equal(2, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(1, 0));
    }

    [Fact]
    public void OrientUpright_BottomRight_Rotates180()
    {
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 0, SKColors.Red);

        // BottomRight = 180°. Source (x,y) -> dest (sw-1-x, sh-1-y); (0,0) -> (1,3).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.BottomRight);

        Assert.Equal(2, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(1, 3));
    }

    [Fact]
    public void OrientUpright_BottomLeft_FlipsVerticallyWithoutSwappingDimensions()
    {
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 0, SKColors.Red);

        // BottomLeft = vertical flip. Source (x,y) -> dest (x, sh-1-y); (0,0) -> (0,3).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.BottomLeft);

        Assert.Equal(2, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(0, 3));
    }

    [Fact]
    public void OrientUpright_LeftTop_TransposesAndSwapsDimensions()
    {
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 1, SKColors.Red);

        // LeftTop = transpose. Source (x,y) -> dest (y, x); (0,1) -> (1,0).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.LeftTop);

        Assert.Equal(4, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(1, 0));
    }

    [Fact]
    public void OrientUpright_RightBottom_TransposesWithFlipAndSwapsDimensions()
    {
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 0, SKColors.Red);

        // RightBottom = transverse. Source (x,y) -> dest (sh-1-y, sw-1-x); (0,0) -> (3,1).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.RightBottom);

        Assert.Equal(4, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(3, 1));
    }

    [Fact]
    public void OrientUpright_LeftBottom_Rotates90CounterClockwiseAndSwapsDimensions()
    {
        var bmp = new SKBitmap(2, 4);
        using (var c = new SKCanvas(bmp)) c.Clear(SKColors.White);
        bmp.SetPixel(0, 0, SKColors.Red);

        // LeftBottom = 90° CCW. Source (x,y) -> dest (y, sw-1-x); (0,0) -> (0,1).
        using var result = BorderProcessor.OrientUpright(bmp, SKEncodedOrigin.LeftBottom);

        Assert.Equal(4, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(SKColors.Red, result.GetPixel(0, 1));
    }
}
