# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
# Run during development (no install needed)
dotnet run -- ./photo.jpg --style polaroid

# Build
dotnet build

# First-time install
dotnet pack -o ./nupkg -c Release
dotnet tool install --global --add-source ./nupkg borderize

# Reinstall the global tool after code changes (local dev loop).
# Use uninstall + install, NOT `dotnet tool update`: update is version-based and
# silently no-ops when <Version> is unchanged, so the new build never lands.
dotnet pack -o ./nupkg -c Release
dotnet tool uninstall --global borderize
dotnet tool install --global --add-source ./nupkg borderize

# Run the test suite
dotnet test

# Build + run tests in Docker (exactly what CI does)
docker build --target test .

# Compile-only stage (no tests)
docker build --target build .
```

Tests live in `Borderize.Tests/` (xUnit) and cover `InputResolver`, `OptionParsing`, and `BorderProcessor.ComputeBorders`/`BuildOutputPath`/`OrientUpright`. EXIF-orientation fixtures are synthesized in-process with `ExifLibNet` (a test-only NuGet dep) — keep it that way; don't shell out to exiftool/ImageMagick, which aren't in the container. CI builds and tests inside Docker on every push/PR: the multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0`) has a `build` stage and a `test` stage, and `.github/workflows/ci.yml` runs `docker build --target test` (a failing test fails the build, gating CI). The `SkiaSharp.NativeAssets.Linux.NoDependencies` package means the native lib loads in the minimal container with no extra `apt` packages — keep it.

## Architecture

The pipeline is: **Program.cs** parses CLI args → **InputResolver** resolves them to a file list → **BorderProcessor** transforms each file.

**`Program.cs`** — CLI entry point using System.CommandLine 3.x (preview). Uses `DefaultValueFactory` (not `DefaultValue`) and passes aliases as an array in the constructor (not `.AddAlias()`). Parses option strings into typed values via `OptionParsing` before constructing `BorderOptions`, then drives the main loop with `Parallel.ForEach` (`--parallel`, default = `Environment.ProcessorCount`); counters use `Interlocked` and console writes are guarded by a lock.

**`OptionParsing.cs`** — `public static` helpers that turn CLI strings into typed values: `ParseStyle`, `ParseColor`, `ParseSize` (pixels vs percentage), `ParseAspect` (`W:H`). Public so the test project can exercise them directly.

**`InputResolver.cs`** — Converts the `input` argument into a flat list of file paths. Handles three modes: directory enumeration, glob (`*`/`?`), and single file. Filters to supported extensions and skips files whose names already end with the configured suffix (prevents double-bordering).

**`BorderProcessor.cs`** — Core image logic using SkiaSharp. `ComputeBorders` (public, pure — unit-tested) resolves pixel vs percentage sizes (percentage is relative to the shorter dimension) and applies Uniform / Polaroid / Aspect proportions. Aspect pads centered to a target ratio with a minimum margin. Draws by filling a new canvas with the border color then blitting the original at an offset. Output format is inferred from the source extension; quality only applies to JPEG/WebP.

**`Models/`** — `BorderOptions` is a positional record threaded through the pipeline. `BorderStyle` is a three-value enum (`Uniform`, `Polaroid`, `Aspect`). Both are `public` so tests can construct/inspect them.

## Key behaviors to preserve

- Percentage sizes scale with `Math.Min(width, height)` (shorter dimension).
- Polaroid default bottom = `sideSize * 3`; `--bottom` overrides it for Uniform and Polaroid (ignored for Aspect).
- Aspect pads centered to `--aspect` (`W:H`) with `--size` as the minimum margin; never shrinks below that margin.
- EXIF orientation is applied on decode via `BorderProcessor.OrientUpright` (SkiaSharp's decoders ignore it) — keep the `SKCodec.EncodedOrigin`-based decode so camera photos aren't bordered sideways.
- Already-bordered files are detected by suffix match on the filename stem (case-insensitive), not by metadata.
- Output is written alongside the original (`dir/stem + suffix + ext`); the original is never modified.
- `Program.cs` builds output paths via `BorderProcessor.BuildOutputPath` — keep that the single source of the naming rule (don't re-inline it).
