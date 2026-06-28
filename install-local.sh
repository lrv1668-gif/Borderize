#!/usr/bin/env bash
#
# Build borderize from source and (re)install it as a global .NET tool.
#
# Uses uninstall + install rather than `dotnet tool update`: update is
# version-based and silently no-ops when <Version> in the csproj is unchanged,
# so a fresh local build would never land. See CONTRIBUTING.md.
#
# Usage: ./install-local.sh
set -euo pipefail

# Run from the repo root regardless of where the script is invoked from.
cd "$(dirname "${BASH_SOURCE[0]}")"

# The local pack version (csproj <Version>). We pin install to it below: the
# package is published on nuget.org at a higher version, and --add-source is
# *additive* (it doesn't replace nuget.org), so without --version, install would
# pick the higher published version instead of the local build we just packed.
VERSION="$(grep -oE '<Version>[^<]+</Version>' Borderize.csproj | sed -E 's#</?Version>##g')"
if [ -z "$VERSION" ]; then
    echo "Could not read <Version> from Borderize.csproj" >&2
    exit 1
fi

echo "==> Packing borderize $VERSION (Release) into ./nupkg"
# Clear stale packages so --add-source only ever sees the build we just made.
rm -f ./nupkg/Borderize.*.nupkg
dotnet pack -o ./nupkg -c Release

# Uninstall any existing copy. The first install has nothing to remove, so
# don't let that abort the script.
if dotnet tool list --global | grep -qi 'borderize'; then
    echo "==> Removing previously installed global tool"
    dotnet tool uninstall --global borderize
fi

echo "==> Installing borderize $VERSION from ./nupkg"
dotnet tool install --global --add-source ./nupkg borderize --version "$VERSION"

echo
echo "Done. Run 'borderize --help' to verify."
echo "If 'borderize' isn't found, add ~/.dotnet/tools to your PATH (see README.md)."
