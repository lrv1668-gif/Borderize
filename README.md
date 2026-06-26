# borderize

A CLI tool for adding borders to photos. Designed for camera export workflows — point it at a folder or glob pattern and it writes bordered copies alongside the originals.

## Border styles

**Uniform** — equal white border on all four sides. Good for a clean, gallery-print look.

**Polaroid** — thinner border on the top and sides, thick border on the bottom. Mimics the classic Polaroid print proportions.

## Installation

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
git clone <this repo>
cd borderize
dotnet pack -o ./nupkg -c Release
dotnet tool install --global --add-source ./nupkg borderize
```

Then add the .NET tools directory to your PATH (one-time setup):

```sh
cat << \EOF >> ~/.zprofile
# Add .NET Core SDK tools
export PATH="$PATH:/Users/$USER/.dotnet/tools"
EOF
```

Restart your terminal, or run `zsh -l` to apply immediately.

## Usage

```
borderize <input> [options]
```

`<input>` can be:
- A **single file**: `./IMG_1234.jpg`
- A **folder**: `./export/2024-japan/` — processes all supported images inside
- A **glob pattern**: `"./photos/*.jpg"` — quote it to prevent shell expansion

Supported formats: `.jpg`, `.jpeg`, `.png`, `.webp`

Output is written alongside the original with a suffix appended to the filename. The original is never modified.

```
IMG_0042.jpg  →  IMG_0042-border.jpg
```

## Examples

```sh
# Polaroid border on a single photo
borderize ./IMG_1234.jpg --style polaroid

# Uniform border on every photo in a folder
borderize ./export/2024-japan/ --style uniform

# Glob — only select files by pattern
borderize "./export/*.jpg" --style polaroid --suffix -x-border

# Preview what would be processed without writing anything
borderize ./photos/ --dry-run

# Custom pixel border size with a hex color
borderize ./photos/ --size 80 --color "#F5F0EB"

# Recurse into subfolders
borderize ./library/ --recursive --style polaroid
```

## Options

| Option | Default | Description |
|---|---|---|
| `--style` | `uniform` | Border style: `uniform` or `polaroid` |
| `--size` | `5%` | Border size as pixels (e.g. `80`) or percentage of the shorter dimension (e.g. `5%`). Applies to top, left, and right. |
| `--bottom` | — | Override bottom border size. Polaroid default is 3× `--size`; uniform default matches `--size`. |
| `--color` | `white` | Border color: `white`, `black`, or a hex value like `#F5F0EB` |
| `--suffix` | `-border` | Suffix added before the file extension on output files |
| `--quality` | `95` | JPEG/WebP output quality, 1–100 |
| `--recursive`, `-r` | off | Recurse into subfolders when input is a directory |
| `--dry-run` | off | Print what would be processed without writing any files |
| `--verbose`, `-v` | off | Print each file and its output path as it's processed |

## Updating

After making code changes, rebuild and reinstall:

```sh
dotnet pack -o ./nupkg -c Release
dotnet tool update --global --add-source ./nupkg borderize
```

## Notes

- Files whose names already end with the configured suffix are skipped automatically, so re-running on a folder won't double-border your outputs.
- PNG output is lossless; `--quality` only applies to JPEG and WebP.
- Border size as a percentage scales with the image, so the same command works on both high-resolution camera files and smaller exports.
