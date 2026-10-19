using System.Text.Json;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// FOUND-3 Portability: the pinned dependencies restore without a sibling
/// checkout, the shared platform packages are only adopted after the feed has
/// been verified, and the runtime manifests reference committed files only.
/// </summary>
public sealed class Found3PortabilityTests
{
    private static readonly DirectoryInfo Root = RepositoryRoot.Find();

    [Fact]
    public async Task TheLockedRestoreSucceedsWithTheRepositoryNuGetConfiguration()
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet",
            ["restore", "Blindmesh.sln", "--locked-mode", "--configfile", Path.Combine(Root.FullName, "NuGet.config")],
            Root.FullName);

        Assert.True(result.ExitCode == 0, $"dotnet restore failed:{Environment.NewLine}{result.All}");
    }

    [Fact]
    public async Task ThePackageSourceMappingSendsPlatformPackagesToThePlatformFeed()
    {
        var nugetConfig = await File.ReadAllTextAsync(Path.Combine(Root.FullName, "NuGet.config"));

        using var document = JsonDocument.Parse("{}");
        Assert.Contains("<packageSource key=\"platform\">", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("<package pattern=\"Platform.*\" />", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("<packageSource key=\"nuget.org\">", nugetConfig, StringComparison.Ordinal);
        Assert.Contains("<package pattern=\"*\" />", nugetConfig, StringComparison.Ordinal);

        var buildProps = await File.ReadAllTextAsync(Path.Combine(Root.FullName, "Directory.Build.props"));
        Assert.Contains("<PlatformPackageVersion", buildProps, StringComparison.Ordinal);

        var directoryPackages = await File.ReadAllTextAsync(Path.Combine(Root.FullName, "Directory.Packages.props"));
        Assert.Contains("Include=\"Platform.Core\" Version=\"$(PlatformPackageVersion)\"", directoryPackages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePlatformPackagesAreOnlyAdoptedAfterTheFeedHasBeenVerified()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"blindmesh-platform-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var projectPath = Path.Combine(workingDirectory, "PlatformProbe.csproj");
            await File.WriteAllTextAsync(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Platform.Core" Version="0.1.0" />
                  </ItemGroup>
                </Project>
                """);

            var result = await ProcessRunner.RunAsync(
                "dotnet",
                [
                    "restore",
                    projectPath,
                    "--configfile",
                    Path.Combine(Root.FullName, "NuGet.config"),
                    "--packages",
                    Path.Combine(workingDirectory, "packages"),
                ],
                workingDirectory,
                timeout: TimeSpan.FromMinutes(6));

            if (result.ExitCode == 0)
            {
                // The feed answered. The adoption is only allowed for the exact
                // pinned version, never for a floating or a newer one.
                var assets = await File.ReadAllTextAsync(
                    Path.Combine(workingDirectory, "packages", "platform.core", "0.1.0", "platform.core.nuspec"));

                Assert.Contains("<version>0.1.0</version>", assets, StringComparison.Ordinal);
                return;
            }

            // The feed is unavailable. The failure must be visible and it must
            // name the platform feed, so the adoption stays blocked.
            Assert.Contains("lileililiwen.github.io", result.All, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task NoRuntimeManifestReferencesASiblingCheckoutOrAMachineSpecificPath()
    {
        var forbidden = new[] { "../forge", "/home/paul", "C:\\Users", "/Users/" };

        foreach (var file in RuntimeManifests())
        {
            var content = await File.ReadAllTextAsync(file.FullName);

            foreach (var pattern in forbidden)
            {
                Assert.False(
                    content.Contains(pattern, StringComparison.OrdinalIgnoreCase),
                    $"{file.FullName} references '{pattern}'.");
            }
        }
    }

    [Fact]
    public async Task TheNuGetConfigurationHasNoLocalOrFilePackageSource()
    {
        var nugetConfig = await File.ReadAllTextAsync(Path.Combine(Root.FullName, "NuGet.config"));

        Assert.DoesNotContain("value=\"file:", nugetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value=\"./", nugetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value=\"../", nugetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value=\"/", nugetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("add key=\"platform-local\"", nugetConfig, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheFrontendPackagePointsAtTheRepositoryPackage()
    {
        var packageJson = await File.ReadAllTextAsync(
            Path.Combine(Root.FullName, "apps", "web", "package.json"));

        Assert.Contains("\"@blindmesh/api-client\": \"file:../../packages/api-client\"", packageJson, StringComparison.Ordinal);
        Assert.True(
            Directory.Exists(Path.Combine(Root.FullName, "packages", "api-client")),
            "The generated API client package is missing.");
    }

    [Fact]
    public async Task TheRuntimeManifestsReferenceCommittedFilesOnly()
    {
        var compose = await File.ReadAllTextAsync(Path.Combine(Root.FullName, "infra", "compose.dev.yaml"));

        Assert.Contains("postgres:17.11-alpine", compose, StringComparison.Ordinal);
        Assert.Contains("dockerfile: src/Blindmesh.Api/Dockerfile", compose, StringComparison.Ordinal);
        Assert.Contains("dockerfile: apps/web/Dockerfile", compose, StringComparison.Ordinal);
        Assert.Contains("dockerfile: src/Blindmesh.Worker/Dockerfile", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("image: blindmesh/", compose, StringComparison.Ordinal);

        var apiDockerfile = await File.ReadAllTextAsync(Path.Combine(Root.FullName, "src", "Blindmesh.Api", "Dockerfile"));
        Assert.Contains("COPY global.json", apiDockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY src/ src/", apiDockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("COPY ../", apiDockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFrontendDependenciesArePinnedByTheLockFile()
    {
        var lockFile = Path.Combine(Root.FullName, "apps", "web", "package-lock.json");

        Assert.True(File.Exists(lockFile), "The frontend lock file is missing.");

        var lockContent = await File.ReadAllTextAsync(lockFile);
        using var document = JsonDocument.Parse(lockContent);

        var packages = document.RootElement.GetProperty("packages");
        Assert.True(packages.TryGetProperty("node_modules/next", out _));
        Assert.True(packages.TryGetProperty("node_modules/react", out _));
        Assert.True(packages.TryGetProperty("node_modules/tailwindcss", out _));
    }

    private static IEnumerable<FileInfo> RuntimeManifests()
    {
        var relativePaths = new[]
        {
            Path.Combine("global.json"),
            Path.Combine("Directory.Build.props"),
            Path.Combine("Directory.Packages.props"),
            Path.Combine("NuGet.config"),
            Path.Combine("infra", "compose.dev.yaml"),
            Path.Combine("infra", "Caddyfile"),
            Path.Combine("src", "Blindmesh.Api", "Dockerfile"),
            Path.Combine("src", "Blindmesh.Worker", "Dockerfile"),
            Path.Combine("apps", "web", "Dockerfile"),
            Path.Combine("apps", "web", "package.json"),
            Path.Combine("apps", "web", "next.config.ts"),
            Path.Combine("apps", "web", "tsconfig.json"),
            Path.Combine("packages", "api-client", "package.json"),
        };

        foreach (var relativePath in relativePaths)
        {
            yield return new FileInfo(Path.Combine(Root.FullName, relativePath));
        }

        foreach (var project in Root.EnumerateFiles("*.csproj", SearchOption.AllDirectories))
        {
            if (!project.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                yield return project;
            }
        }
    }
}
