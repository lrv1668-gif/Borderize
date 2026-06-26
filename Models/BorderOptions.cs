using SkiaSharp;

namespace Borderize.Models;

record BorderOptions(
    BorderStyle Style,
    string Size,
    string? Bottom,
    SKColor Color,
    string Suffix,
    bool Recursive,
    int Quality,
    bool DryRun,
    bool Verbose
);
