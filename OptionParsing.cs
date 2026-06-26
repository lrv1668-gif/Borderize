using Borderize.Models;
using SkiaSharp;

namespace Borderize;

public static class OptionParsing
{
    public static BorderStyle ParseStyle(string value) => value.ToLowerInvariant() switch
    {
        "uniform" => BorderStyle.Uniform,
        "polaroid" => BorderStyle.Polaroid,
        "aspect" => BorderStyle.Aspect,
        _ => throw new ArgumentException($"Unknown style '{value}'. Use: uniform, polaroid, aspect"),
    };

    public static SKColor ParseColor(string value) => value.ToLowerInvariant() switch
    {
        "white" => SKColors.White,
        "black" => SKColors.Black,
        _ when value.StartsWith('#') => HexToSKColor(value),
        _ => throw new ArgumentException($"Unknown color '{value}'. Use: white, black, or #RRGGBB"),
    };

    static SKColor HexToSKColor(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6)
            throw new ArgumentException($"Hex color must be 6 digits, e.g. #FFFFFF. Got: {hex}");
        return new SKColor(
            Convert.ToByte(h[..2], 16),
            Convert.ToByte(h[2..4], 16),
            Convert.ToByte(h[4..6], 16),
            255);
    }

    /// <summary>
    /// Resolves a size string to pixels. A trailing '%' is interpreted as a
    /// percentage of <paramref name="referenceDimension"/>; otherwise the value
    /// is treated as a raw pixel count.
    /// </summary>
    public static int ParseSize(string value, int referenceDimension)
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

    /// <summary>
    /// Parses an aspect ratio in "W:H" form (e.g. "1:1", "4:5") into its
    /// component integers. Both parts must be positive.
    /// </summary>
    public static (int W, int H) ParseAspect(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out int w)
            || !int.TryParse(parts[1], out int h)
            || w <= 0 || h <= 0)
        {
            throw new ArgumentException($"Invalid aspect ratio '{value}'. Use W:H, e.g. 1:1 or 4:5");
        }
        return (w, h);
    }
}
