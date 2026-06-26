# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
# Run during development (no install needed)
dotnet run -- ./photo.jpg --style polaroid

# Build
dotnet build

# Pack and reinstall the global tool after code changes
dotnet pack -o ./nupkg -c Release
dotnet tool update --global --add-source ./nupkg borderize

# First-time install
dotnet tool install --global --add-source ./nupkg borderize
```

There are no tests.

## Architecture

The pipeline is: **Program.cs** parses CLI args → **InputResolver** resolves them to a file list → **BorderProcessor** transforms each file.

**`Program.cs`** — CLI entry point using System.CommandLine 3.x (preview). Uses `DefaultValueFactory` (not `DefaultValue`) and passes aliases as an array in the constructor (not `.AddAlias()`). Parses color/style strings into typed values before constructing `BorderOptions`, then drives the main loop.

**`InputResolver.cs`** — Converts the `input` argument into a flat list of file paths. Handles three modes: directory enumeration, glob (`*`/`?`), and single file. Filters to supported extensions and skips files whose names already end with the configured suffix (prevents double-bordering).

**`BorderProcessor.cs`** — Core image logic using SkiaSharp. `ComputeBorders` resolves pixel vs percentage sizes (percentage is relative to the shorter dimension) and applies Uniform vs Polaroid proportions. Draws by filling a new canvas with the border color then blitting the original at an offset. Output format is inferred from the source extension; quality only applies to JPEG/WebP.

**`Models/`** — `BorderOptions` is a positional record threaded through the pipeline. `BorderStyle` is a two-value enum.

## Key behaviors to preserve

- Percentage sizes scale with `Math.Min(width, height)` (shorter dimension).
- Polaroid default bottom = `sideSize * 3`; `--bottom` overrides it for both styles.
- Already-bordered files are detected by suffix match on the filename stem (case-insensitive), not by metadata.
- Output is written alongside the original (`dir/stem + suffix + ext`); the original is never modified.
