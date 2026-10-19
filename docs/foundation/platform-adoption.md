# Shared platform package adoption

Date: 2026-10-19. Owner: Blindmesh. Decision: adoption is **blocked** until the public platform feed answers.

## What was planned

`docs/architecture.md` and the runtime foundation design adopt the shared `Platform.*` packages from the public platform feed through one defaulted `PlatformPackageVersion` and the package source mapping in `NuGet.config`:

| Item | Value |
|---|---|
| Feed | `https://lileililiwen.github.io/forge/nuget/index.json` |
| Packages | `Platform.Core`, `Platform.AspNetCore` |
| Version | `0.1.0`, defaulted by `PlatformPackageVersion` in `Directory.Build.props` |
| Mapping | `Platform.*` to the platform feed, `*` to nuget.org |

## What the feed answers

The feed is not reachable. The probe and its result:

```sh
curl -sSL -o /dev/null -w "%{http_code} -> %{url_effective}\n" \
  https://lileililiwen.github.io/forge/nuget/index.json
# 404 -> https://about.tooosall.uk/forge/nuget/index.json
```

The legacy address `https://lileililiwen.github.io/dotnet-platform-libs/nuget/index.json` answers the same way. The `quay.io` mirror of the same project requires a token and is not an anonymous feed.

## What the product does now

The product does not reference any `Platform.*` package. It uses the in-box ASP.NET Core, EF Core and Npgsql primitives, which carry no shared runtime dependency. No replacement framework was written to imitate the platform packages, and no sibling checkout, local feed path or machine specific address was added.

The failure stays visible in every restore:

```
warning NU1900: An error occurred while fetching package vulnerability data: Could not load the service index for source https://lileililiwen.github.io/forge/nuget/index.json.
```

The warning is not promoted to an error, so the product restore keeps working. It is never hidden: `NU1900` is listed in `WarningsNotAsErrors` in `Directory.Build.props` with this file as the reason.

## Test coverage

`Found3PortabilityTests.ThePlatformPackagesAreOnlyAdoptedAfterTheFeedHasBeenVerified` restores a scratch project that references `Platform.Core 0.1.0` with the repository `NuGet.config` and an isolated package cache.

- The feed answers: the restore must succeed and the resolved package must be exactly `0.1.0`.
- The feed does not answer: the restore must fail visibly and name the platform feed, so the adoption stays blocked.

## Retry

```sh
curl -sSL -o /dev/null -w "%{http_code}\n" https://lileililiwen.github.io/forge/nuget/index.json
dotnet restore Blindmesh.sln --locked-mode
dotnet test Blindmesh.sln -c Release --filter "FullyQualifiedName~ThePlatformPackagesAreOnlyAdopted"
```

When the feed answers, the adoption is completed by adding the `Platform.*` package references to the projects that need them, using `$(PlatformPackageVersion)`, and by recording the package, version and provenance receipt here.
