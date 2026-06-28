using Borderize.Models;
using System.CommandLine;

namespace Borderize;

/// <summary>
/// Builds and runs the borderize command line. Extracted from Program.cs so the
/// CLI (argument parsing, input resolution, processing loop, exit codes) can be
/// driven in-process by the integration tests.
/// </summary>
public static class Cli
{
    /// <summary>Parse <paramref name="args"/> and run the command, returning the process exit code.</summary>
    public static Task<int> RunAsync(string[] args) => Build().Parse(args).InvokeAsync();

    /// <summary>Constructs the root command with all options and the processing action wired up.</summary>
    public static RootCommand Build()
    {
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
            BorderOptions options;
            string input;
            bool recursive, dryRun, verbose;
            string suffix;
            int parallel;
            try
            {
                input = result.GetValue(inputArg)!;
                var style = OptionParsing.ParseStyle(result.GetValue(styleOption)!);
                var size = result.GetValue(sizeOption)!;
                var bottom = result.GetValue(bottomOption);
                var color = OptionParsing.ParseColor(result.GetValue(colorOption)!);
                suffix = result.GetValue(suffixOption)!;
                recursive = result.GetValue(recursiveOption);
                var quality = result.GetValue(qualityOption);
                dryRun = result.GetValue(dryRunOption);
                verbose = result.GetValue(verboseOption);
                parallel = result.GetValue(parallelOption);

                // --aspect only matters for the aspect style; for other styles warn (don't
                // fail) if it was explicitly set, and don't bother validating its value.
                bool aspectExplicit = result.GetResult(aspectOption)?.Implicit == false;
                (int W, int H) aspect = (1, 1);
                if (style == BorderStyle.Aspect)
                    aspect = OptionParsing.ParseAspect(result.GetValue(aspectOption)!);
                else if (aspectExplicit)
                    Console.Error.WriteLine("Warning: --aspect is ignored unless --style aspect is set.");

                if (quality < 1 || quality > 100)
                    throw new ArgumentException($"Quality must be between 1 and 100. Got: {quality}");
                if (string.IsNullOrEmpty(suffix))
                    throw new ArgumentException("Suffix must not be empty (it prevents overwriting the original).");

                options = new BorderOptions(style, size, bottom, aspect, color, suffix, recursive, quality, dryRun, verbose);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            List<string> files;
            try
            {
                files = InputResolver.Resolve(input, recursive, suffix).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            if (files.Count == 0)
            {
                Console.WriteLine("No matching files found.");
                return 0;
            }

            Console.WriteLine(dryRun
                ? $"Dry run — {files.Count} file(s) would be processed:"
                : $"Processing {files.Count} file(s)...");

            if (dryRun)
            {
                foreach (var file in files)
                    Console.WriteLine($"  {file}  ->  {BorderProcessor.BuildOutputPath(file, suffix)}");
                return 0;
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
            return skipped > 0 ? 1 : 0;
        });

        return rootCommand;
    }
}
