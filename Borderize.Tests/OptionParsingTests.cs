using Borderize;
using Borderize.Models;
using SkiaSharp;
using Xunit;

namespace Borderize.Tests;

public class OptionParsingTests
{
    [Theory]
    [InlineData("uniform", BorderStyle.Uniform)]
    [InlineData("polaroid", BorderStyle.Polaroid)]
    [InlineData("aspect", BorderStyle.Aspect)]
    [InlineData("POLAROID", BorderStyle.Polaroid)]
    public void ParseStyle_RecognizesKnownStyles(string input, BorderStyle expected)
    {
        Assert.Equal(expected, OptionParsing.ParseStyle(input));
    }

    [Fact]
    public void ParseStyle_Throws_OnUnknown()
    {
        Assert.Throws<ArgumentException>(() => OptionParsing.ParseStyle("circle"));
    }

    [Fact]
    public void ParseColor_HandlesNamedColors()
    {
        Assert.Equal(SKColors.White, OptionParsing.ParseColor("white"));
        Assert.Equal(SKColors.Black, OptionParsing.ParseColor("BLACK"));
    }

    [Fact]
    public void ParseColor_ParsesHex()
    {
        Assert.Equal(new SKColor(0xF5, 0xF0, 0xEB, 255), OptionParsing.ParseColor("#F5F0EB"));
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("turquoise")]
    public void ParseColor_Throws_OnInvalid(string input)
    {
        Assert.ThrowsAny<Exception>(() => OptionParsing.ParseColor(input));
    }

    [Fact]
    public void ParseSize_Percentage_IsRelativeToReferenceDimension()
    {
        Assert.Equal(40, OptionParsing.ParseSize("5%", 800));
    }

    [Fact]
    public void ParseSize_Pixels_AreReturnedDirectly()
    {
        Assert.Equal(80, OptionParsing.ParseSize("80", 800));
    }

    [Fact]
    public void ParseSize_Percentage_ClampsToAtLeastOne()
    {
        Assert.Equal(1, OptionParsing.ParseSize("0%", 800));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("10px")]
    [InlineData("%")]
    public void ParseSize_Throws_OnInvalid(string input)
    {
        Assert.Throws<ArgumentException>(() => OptionParsing.ParseSize(input, 800));
    }

    [Theory]
    [InlineData("1:1", 1, 1)]
    [InlineData("4:5", 4, 5)]
    [InlineData("16:9", 16, 9)]
    public void ParseAspect_ParsesValidRatios(string input, int w, int h)
    {
        Assert.Equal((w, h), OptionParsing.ParseAspect(input));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1:0")]
    [InlineData("0:1")]
    [InlineData("1:2:3")]
    [InlineData("a:b")]
    public void ParseAspect_Throws_OnInvalid(string input)
    {
        Assert.Throws<ArgumentException>(() => OptionParsing.ParseAspect(input));
    }
}
