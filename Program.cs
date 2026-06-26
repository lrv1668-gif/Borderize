using Borderize;
using Borderize.Models;
using SkiaSharp;
using System.CommandLine;

var inputArg = new Argument<string>("input")
{
    Description = "File path, folder path, or glob pattern (e.g. \"./photos/*.jpg\")",
};

var styleOption = new Option<string>("--style", [])
{
    Description = "Border style: uniform | polaroid",
    DefaultValueFactory = _ => "uniform",
};

var sizeOption = new Option<string>("--size", [])
{
    Description = "Border size as pixels (e.g. 80) or percentage (e.g. 5%). Applies to top, left, right.",
    DefaultValueFactory = _ => "5%",
};

var bottomOption = new Option<string?>("--bottom", [])
{
    Description = "Override bottom border size. Polaroid default is 3x --size; uniform default matches --size.",
};

var colorOption = new Option<string>("--color", [])
{
    Description = "Border color: white | black | #RRGGBB",
    DefaultValueFactory = _ => "white",
};

var suffixOption = new Option<string>("--suffix", [])
{
    Description = "Suffix appended to the output filename before the extension.",
    DefaultValueFactory = _ => "-border",
};

var recursiveOption = new Option<bool>("--recursive", ["-r"])
{
    Description = "Recurse into subfolders when input is a directory.",
};

var qualityOption = new Option<int>("--quality", [])
{
    Description = "JPEG/WebP output quality (1-100).",
    DefaultValueFactory = _ => 95,
};

var dryRunOption = new Option<bool>("--dry-run", [])
{
    Description = "Print files that would be processed without writing any output.",
};

var verboseOption = new Option<bool>("--verbose", ["-v"])
{
    Description = "Print each file as it is processed.",
};

var rootCommand = new RootCommand("borderize — add photo borders from the command line")
{
    inputArg,
    styleOption,
    sizeOption,
    bottomOption,
    colorOption,
    suffixOption,
    recursiveOption,
    qualityOption,
    dryRunOption,
    verboseOption,
};

rootCommand.SetAction((ParseResult result) =>
{
    var input = result.GetValue(inputArg)!;
    var style = ParseStyle(result.GetValue(styleOption)!);
    var size = result.GetValue(sizeOption)!;
    var bottom = result.GetValue(bottomOption);
    var color = ParseColor(result.GetValue(colorOption)!);
    var suffix = result.GetValue(suffixOption)!;
    var recursive = result.GetValue(recursiveOption);
    var quality = result.GetValue(qualityOption);
    var dryRun = result.GetValue(dryRunOption);
    var verbose = result.GetValue(verboseOption);

    var options = new BorderOptions(style, size, bottom, color, suffix, recursive, quality, dryRun, verbose);

    var files = InputResolver.Resolve(input, recursive, suffix).ToList();

    if (files.Count == 0)
    {
        Console.WriteLine("No matching files found.");
        return;
    }

    Console.WriteLine(dryRun
        ? $"Dry run — {files.Count} file(s) would be processed:"
        : $"Processing {files.Count} file(s)...");

    int processed = 0, skipped = 0;

    foreach (var file in files)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(file) ?? ".",
            $"{Path.GetFileNameWithoutExtension(file)}{suffix}{Path.GetExtension(file)}");

        if (dryRun)
        {
            Console.WriteLine($"  {file}  ->  {outputPath}");
            continue;
        }

        try
        {
            if (verbose) Console.Write($"  {file}");
            BorderProcessor.Process(file, options);
            if (verbose) Console.WriteLine($"  ->  {outputPath}");
            processed++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ERROR {file}: {ex.Message}");
            skipped++;
        }
    }

    if (!dryRun)
        Console.WriteLine($"Done. {processed} processed, {skipped} failed.");
});

return await rootCommand.Parse(args).InvokeAsync();

static BorderStyle ParseStyle(string value) => value.ToLowerInvariant() switch
{
    "polaroid" => BorderStyle.Polaroid,
    "uniform" => BorderStyle.Uniform,
    _ => throw new ArgumentException($"Unknown style '{value}'. Use: uniform, polaroid"),
};

static SKColor ParseColor(string value) => value.ToLowerInvariant() switch
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
