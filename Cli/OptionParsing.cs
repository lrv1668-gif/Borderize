using System.Globalization;
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
        // TryParse (not Convert.ToByte) so a non-hex digit surfaces as ArgumentException,
        // which the CLI reports as "Error: ..." instead of an unhandled FormatException.
        var h = hex.AsSpan(1);
        if (h.Length != 6
            || !byte.TryParse(h[..2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte r)
            || !byte.TryParse(h[2..4], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte g)
            || !byte.TryParse(h[4..6], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b))
        {
            throw new ArgumentException($"Hex color must be 6 hex digits, e.g. #FFFFFF. Got: {hex}");
        }
        return new SKColor(r, g, b, 255);
    }

    /// <summary>
    /// Resolves a size string to pixels. A trailing '%' is interpreted as a
    /// percentage of <paramref name="referenceDimension"/>; otherwise the value
    /// is treated as a raw pixel count. Negative results are rejected; a
    /// percentage clamps to a minimum of 1 pixel. Parsing is culture-invariant
    /// ("2.5%" means the same on every machine).
    /// </summary>
    public static int ParseSize(string value, int referenceDimension)
    {
        if (value.EndsWith('%'))
        {
            if (!double.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct) || pct < 0)
                throw new ArgumentException($"Invalid percentage: {value}");
            return Math.Max(1, (int)Math.Round(referenceDimension * pct / 100.0));
        }
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int px))
            throw new ArgumentException($"Invalid size value: {value}");
        if (px < 0)
            throw new ArgumentException($"Size must not be negative: {value}");
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
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)
            || w <= 0 || h <= 0)
        {
            throw new ArgumentException($"Invalid aspect ratio '{value}'. Use W:H, e.g. 1:1 or 4:5");
        }
        return (w, h);
    }
}
