using SkiaSharp;

namespace Borderize.Models;

public record BorderOptions(
    BorderStyle Style,
    string Size,
    string? Bottom,
    (int W, int H) Aspect,
    SKColor Color,
    string Suffix,
    bool Recursive,
    int Quality,
    bool DryRun,
    bool Verbose
);
