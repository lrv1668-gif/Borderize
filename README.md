# borderize

A CLI tool for adding borders to photos. Designed for camera export workflows — point it at a folder or glob pattern and it writes bordered copies alongside the originals.

## Border styles

**Uniform** — equal white border on all four sides. Good for a clean, gallery-print look.

**Polaroid** — thinner border on the top and sides, thick border on the bottom. Mimics the classic Polaroid print proportions.

**Aspect** — pads the image, centered, to a target aspect ratio (default 1:1) with at least the `--size` margin around it. Good for square or 4:5 social-media framing.

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

# Square pad for social media (1:1 by default)
borderize ./photos/ --style aspect

# 4:5 portrait pad
borderize ./photos/ --style aspect --aspect 4:5

# Throttle concurrency on a large batch
borderize ./export/ --recursive --parallel 4
```

## Options

| Option | Default | Description |
|---|---|---|
| `--style` | `uniform` | Border style: `uniform`, `polaroid`, or `aspect` |
| `--size` | `5%` | Border size as pixels (e.g. `80`) or percentage of the shorter dimension (e.g. `5%`). Applies to top, left, and right. For `aspect`, this is the minimum margin. |
| `--bottom` | — | Override bottom border size. Polaroid default is 3× `--size`; uniform default matches `--size`. Ignored for `aspect`. |
| `--aspect` | `1:1` | Target ratio `W:H` for `--style aspect` (e.g. `1:1`, `4:5`). Ignored for other styles. |
| `--color` | `white` | Border color: `white`, `black`, or a hex value like `#F5F0EB` |
| `--suffix` | `-border` | Suffix added before the file extension on output files |
| `--quality` | `95` | JPEG/WebP output quality, 1–100 |
| `--recursive`, `-r` | off | Recurse into subfolders when input is a directory |
| `--parallel` | `0` | Max files to process concurrently. `0` = one per CPU core. Lower it (e.g. `4`) to cap memory on very large batches. |
| `--dry-run` | off | Print what would be processed without writing any files |
| `--verbose`, `-v` | off | Print each file and its output path as it's processed |

## Updating

After making code changes, rebuild and reinstall:

```sh
dotnet pack -o ./nupkg -c Release
dotnet tool uninstall --global borderize
dotnet tool install --global --add-source ./nupkg borderize
```

> **Note:** use uninstall + install, not `dotnet tool update`. Update is
> version-based, so if `<Version>` in the csproj hasn't changed it reports the
> tool as already up to date and your new build is never installed. (When you
> publish a real release you bump `<Version>` anyway, and `update` works then.)

## Development

Run the test suite locally:

```sh
dotnet test
```

Or build and test inside Docker — this is exactly what CI runs, so it reproduces the CI environment without a local .NET install:

```sh
# Build + run the test suite (a failing test fails the build)
docker build --target test .

# Compile only, no tests
docker build --target build .
```

CI builds and tests in Docker on every push and pull request via the multi-stage `Dockerfile` and `.github/workflows/ci.yml`.

## Releasing

Publishing is automated and uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OIDC) — no long-lived API key is stored in the repo. One-time setup:

1. On nuget.org, open your username menu → **Trusted Publishing** and add a policy for this repo: Repository Owner `lrv1668-gif`, Repository `Borderize`, Workflow File `publish.yml`.
2. Add a repository secret `NUGET_USER` set to your nuget.org **profile name** (not your email) — Settings → Secrets and variables → Actions.

To cut a release, publish a [GitHub Release](https://github.com/lrv1668-gif/Borderize/releases) tagged `vX.Y.Z` (e.g. `v1.1.0`). The `.github/workflows/publish.yml` workflow then:

1. Derives the package version from the tag (drops the leading `v`).
2. Runs the test suite in Docker as a gate.
3. Exchanges the GitHub OIDC token for a short-lived NuGet API key (`NuGet/login`).
4. Packs and pushes `Borderize X.Y.Z` to nuget.org.

Prerelease tags like `v1.1.0-rc1` are supported; install them with `dotnet tool install --global Borderize --prerelease`.

> **Note:** for a brand-new private repo, the trusted publishing policy is provisional for 7 days until the first successful publish locks it to the repo/owner IDs. See the [docs](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing#policies-pending-full-activation).

## Notes

- Files whose names already end with the configured suffix are skipped automatically, so re-running on a folder won't double-border your outputs.
- PNG output is lossless; `--quality` only applies to JPEG and WebP.
- Border size as a percentage scales with the image, so the same command works on both high-resolution camera files and smaller exports.
