# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
# Run during development (no install needed)
dotnet run -- ./photo.jpg --style polaroid

# Build
dotnet build

# Install / reinstall the global tool from a local build (first time and dev loop)
./install-local.sh

# Run the test suite
dotnet test

# Build + run tests in Docker (exactly what CI does)
docker build --target test .

# Compile-only stage (no tests)
docker build --target build .
```

Always use `./install-local.sh` to install a local build — never bare `dotnet tool install`/`update`, which silently install the nuget.org version or no-op. The script header explains why.

## Tests

Tests live in `Borderize.Tests/` (xUnit), one file per component plus `CliTests` for the full CLI end-to-end. Rules that aren't obvious from reading them:

- `CliTests` drive the real command in-process via `Cli.RunAsync(args)` and redirect the process-global `Console.Out`/`Error`, so they must stay in the non-parallel `[Collection("CLI")]`.
- EXIF-orientation fixtures are synthesized in-process with `ExifLibNet` (a test-only NuGet dep) — keep it that way; don't shell out to exiftool/ImageMagick, which aren't in the container.

CI builds and tests inside Docker on every push/PR: the multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0`) has a `build` stage and a `test` stage, and `.github/workflows/ci.yml` runs `docker build --target test` (a failing test fails the build). The `SkiaSharp.NativeAssets.Linux.NoDependencies` package means the native lib loads in the minimal container with no extra `apt` packages — keep it.

## Architecture

The pipeline is: **Cli/Cli.cs** parses CLI args → **Processing/InputResolver.cs** resolves them to a file list → **Processing/BorderProcessor.cs** transforms each file. **Program.cs** (project root) is a one-line entry point (`return await Borderize.Cli.RunAsync(args);`).

Source is organized into folders, all sharing the flat `Borderize` namespace except `Models/` (`Borderize.Models`); folders are organizational only and the `.csproj` globs `**/*.cs`, so adding/moving files needs no project edits.

**`Cli/Cli.cs`** — `Cli.Build()` constructs the `RootCommand` (all options + the `SetAction` handler) and `Cli.RunAsync(args)` parses and invokes it, returning the exit code. Kept separate from `Program.cs` so tests can drive the whole CLI in-process. Parses option strings into typed values via `OptionParsing`, validates, constructs `BorderOptions`, then drives the main loop with `Parallel.ForEach` (`--parallel`; `0` = `Environment.ProcessorCount`); counters use `Interlocked` and console writes are guarded by a lock.

**`Cli/OptionParsing.cs`** — `public static` helpers that turn CLI strings into typed values: `ParseStyle`, `ParseColor`, `ParseSize` (pixels vs percentage), `ParseAspect` (`W:H`).

**`Processing/InputResolver.cs`** — Converts the `input` argument into a flat list of file paths: directory enumeration, glob (`*`/`?`), or single file. Filters to supported extensions and skips files whose names already end with the configured suffix.

**`Processing/BorderProcessor.cs`** — Core image logic using SkiaSharp. `ComputeBorders` (public, pure) resolves pixel vs percentage sizes and applies Uniform / Polaroid / Aspect proportions. Draws by filling a new canvas with the border color then blitting the original at an offset. Output format is inferred from the source extension; quality only applies to JPEG/WebP.

**`Models/`** — `BorderOptions` is a positional record threaded through the pipeline. `BorderStyle` is a three-value enum (`Uniform`, `Polaroid`, `Aspect`).

## Library constraints

- **SkiaSharp, not ImageSharp.** ImageSharp v4 requires a paid license key at build time, so it was rejected — don't suggest switching.
- **System.CommandLine 3.x (preview), not 2.x.** The API differs from most online examples:
  - `new Option<T>("--name", ["-n"])` — aliases as an array in the constructor, not `.AddAlias()`
  - `option.DefaultValueFactory = _ => value` (not `DefaultValue`); `option.Description = "..."`
  - `rootCommand.SetAction((ParseResult result) => { ... })` — not `SetHandler`
  - Read values with `result.GetValue(option)`; run with `rootCommand.Parse(args).InvokeAsync()`

## Adding a CLI option

A new option touches several places — update all of them:

1. `Cli/Cli.cs` — declare the option, add it to the `RootCommand`, read it in `SetAction`, and validate it there (throw `ArgumentException` → `Error:` message, exit 1).
2. `Cli/OptionParsing.cs` — add a `Parse*` helper if the string needs converting to a typed value.
3. `Models/BorderOptions.cs` — add a field if `BorderProcessor` needs it.
4. `README.md` — add a row to the Options table (and a Notes entry for any caveat).
5. Tests — `OptionParsingTests` for parsing, `CliTests` for end-to-end behavior and validation errors.

## Key behaviors to preserve

- Percentage sizes scale with `Math.Min(width, height)` (shorter dimension).
- Polaroid default bottom = `sideSize * 3`; `--bottom` overrides it for Uniform and Polaroid (ignored for Aspect).
- Aspect pads centered to `--aspect` (`W:H`) with `--size` as the minimum margin; never shrinks below that margin.
- EXIF orientation is applied on decode via `BorderProcessor.OrientUpright` (SkiaSharp's decoders ignore it) — keep the `SKCodec.EncodedOrigin`-based decode so camera photos aren't bordered sideways.
- Already-bordered files are detected by suffix match on the filename stem (case-insensitive), not by metadata.
- Output is `stem + suffix + ext`, written alongside the original by default or into `--output-dir` if given; the original is never modified. `--output-dir` flattens output, so with `--recursive` same-named files from different subfolders overwrite each other.
- `BorderProcessor.BuildOutputPath` is the single source of the naming rule — `Cli.cs` (dry-run/verbose output) and `BorderProcessor.Process` both call it; don't re-inline it.
