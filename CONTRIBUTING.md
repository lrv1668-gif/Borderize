# Contributing

Notes for developing, testing, and releasing borderize. (End-user docs live in
[`README.md`](README.md).)

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
git clone <this repo>
cd borderize
dotnet pack -o ./nupkg -c Release
dotnet tool install --global --add-source ./nupkg borderize
```

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

## Testing

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

1. On nuget.org, open your username menu → **Trusted Publishing** and add a policy for this repo: Repository Owner `lrv1668-gif`, Repository `Borderize`, Workflow File `publish.yml`, and **Environment** `production`. The Environment must match the `environment:` in `publish.yml` (this repo uses `production`) — if the policy's Environment is set but the workflow's doesn't match, the OIDC token exchange fails with `Environment mismatch`.
2. Add a repository secret `NUGET_USER` set to your nuget.org **profile name** (not your email) — Settings → Secrets and variables → Actions.

To cut a release, publish a [GitHub Release](https://github.com/lrv1668-gif/Borderize/releases) tagged `vX.Y.Z` (e.g. `v1.1.0`). The `.github/workflows/publish.yml` workflow then:

1. Derives the package version from the tag (drops the leading `v`).
2. Runs the test suite in Docker as a gate.
3. Exchanges the GitHub OIDC token for a short-lived NuGet API key (`NuGet/login`).
4. Packs and pushes `Borderize X.Y.Z` to nuget.org.

Prerelease tags like `v1.1.0-rc1` are supported; install them with `dotnet tool install --global Borderize --prerelease`.

> **Note:** for a brand-new private repo, the trusted publishing policy is provisional for 7 days until the first successful publish locks it to the repo/owner IDs. See the [docs](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing#policies-pending-full-activation).
