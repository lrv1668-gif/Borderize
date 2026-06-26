using Borderize;
using Borderize.Models;
using System.CommandLine;

var inputArg = new Argument<string>("input")
{
    Description = "File path, folder path, or glob pattern (e.g. \"./photos/*.jpg\")",
};

var styleOption = new Option<string>("--style", [])
{
    Description = "Border style: uniform | polaroid | aspect",
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

var aspectOption = new Option<string>("--aspect", [])
{
    Description = "Target ratio W:H for --style aspect (e.g. 1:1, 4:5). Ignored for other styles.",
    DefaultValueFactory = _ => "1:1",
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

var parallelOption = new Option<int>("--parallel", [])
{
    Description = "Max files to process concurrently. 0 = one per CPU core.",
    DefaultValueFactory = _ => 0,
};

var rootCommand = new RootCommand("borderize — add photo borders from the command line")
{
    inputArg,
    styleOption,
    sizeOption,
    bottomOption,
    aspectOption,
    colorOption,
    suffixOption,
    recursiveOption,
    qualityOption,
    dryRunOption,
    verboseOption,
    parallelOption,
};

rootCommand.SetAction((ParseResult result) =>
{
    var input = result.GetValue(inputArg)!;
    var style = OptionParsing.ParseStyle(result.GetValue(styleOption)!);
    var size = result.GetValue(sizeOption)!;
    var bottom = result.GetValue(bottomOption);
    var aspect = OptionParsing.ParseAspect(result.GetValue(aspectOption)!);
    var color = OptionParsing.ParseColor(result.GetValue(colorOption)!);
    var suffix = result.GetValue(suffixOption)!;
    var recursive = result.GetValue(recursiveOption);
    var quality = result.GetValue(qualityOption);
    var dryRun = result.GetValue(dryRunOption);
    var verbose = result.GetValue(verboseOption);
    var parallel = result.GetValue(parallelOption);

    if (style != BorderStyle.Aspect && result.GetResult(aspectOption)?.Implicit == false)
        Console.Error.WriteLine("Warning: --aspect is ignored unless --style aspect is set.");

    var options = new BorderOptions(style, size, bottom, aspect, color, suffix, recursive, quality, dryRun, verbose);

    var files = InputResolver.Resolve(input, recursive, suffix).ToList();

    if (files.Count == 0)
    {
        Console.WriteLine("No matching files found.");
        return;
    }

    Console.WriteLine(dryRun
        ? $"Dry run — {files.Count} file(s) would be processed:"
        : $"Processing {files.Count} file(s)...");

    if (dryRun)
    {
        foreach (var file in files)
            Console.WriteLine($"  {file}  ->  {BorderProcessor.BuildOutputPath(file, suffix)}");
        return;
    }

    int processed = 0, skipped = 0;
    var consoleLock = new object();

    var parallelOptions = new ParallelOptions
    {
        MaxDegreeOfParallelism = parallel > 0 ? parallel : Environment.ProcessorCount,
    };

    Parallel.ForEach(files, parallelOptions, file =>
    {
        try
        {
            BorderProcessor.Process(file, options);
            Interlocked.Increment(ref processed);
            if (verbose)
                lock (consoleLock)
                    Console.WriteLine($"  {file}  ->  {BorderProcessor.BuildOutputPath(file, suffix)}");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref skipped);
            lock (consoleLock)
                Console.Error.WriteLine($"  ERROR {file}: {ex.Message}");
        }
    });

    Console.WriteLine($"Done. {processed} processed, {skipped} failed.");
});

return await rootCommand.Parse(args).InvokeAsync();
