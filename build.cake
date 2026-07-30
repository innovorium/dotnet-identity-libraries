#nullable enable

var target = Argument("target", "Verify");
var configuration = Argument("configuration", "Release");

var solution = "./dotnet-identity-libraries.slnx";
DirectoryPath packageDirectory = Directory("./artifacts/packages");
DirectoryPath inspectionDirectory = Directory("./artifacts/inspection");
DirectoryPath consumerDirectory = Directory("./artifacts/consumer");
var runningInCi = BuildSystem.IsRunningOnGitHubActions;
var packageIds = new[]
{
    "Innovorium.AspNetCore.Identity.Marten",
    "Innovorium.OpenIddict.Marten"
};
var expectedPackageDependencies = new Dictionary<string, Dictionary<string, string>>
{
    ["Innovorium.AspNetCore.Identity.Marten"] = new()
    {
        ["Marten"] = "[9.21.0, 10.0.0)",
        ["Microsoft.Extensions.Identity.Stores"] = "[10.0.10, 11.0.0)"
    },
    ["Innovorium.OpenIddict.Marten"] = new()
    {
        ["Marten"] = "[9.21.0, 10.0.0)",
        ["Npgsql"] = "[9.0.4, 10.0.0)",
        ["OpenIddict.Core"] = "[7.6.0, 8.0.0)",
        ["Weasel.Storage"] = "[9.17.0, 10.0.0)"
    }
};
const string ExpectedLicenseExpression = "MIT";
const string ExpectedRepositoryUrl = "https://github.com/innovorium/dotnet-identity-libraries";
var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");

void RunProcessOrFail(FilePath executable, ProcessArgumentBuilder arguments)
{
    var exitCode = StartProcess(
        executable,
        new ProcessSettings
        {
            Arguments = arguments,
            WorkingDirectory = MakeAbsolute(Directory("."))
        });

    if (exitCode != 0)
    {
        throw new CakeException($"'{executable}' exited with code {exitCode}.");
    }
}

string PackageVersion(FilePath package, string packageId)
{
    var fileName = package.GetFilenameWithoutExtension().FullPath;
    return fileName[(packageId.Length + 1)..];
}

FilePath GetPackage(string packageId, string extension)
{
    var matches = GetFiles($"{packageDirectory}/{packageId}.*{extension}");
    if (matches.Count != 1)
    {
        throw new CakeException($"Expected one {extension} for {packageId}, found {matches.Count}.");
    }

    return matches.Single();
}

void RequireFile(FilePath path)
{
    if (!FileExists(path))
    {
        throw new CakeException($"Required package content is missing: {path}");
    }
}

string GetRepositoryCommit()
{
    var process = StartAndReturnProcess(
        "git",
        new ProcessSettings
        {
            Arguments = "rev-parse HEAD",
            RedirectStandardOutput = true
        });
    process.WaitForExit();
    var commit = process.GetStandardOutput().SingleOrDefault()?.Trim();

    if (process.GetExitCode() != 0 || string.IsNullOrWhiteSpace(commit))
    {
        throw new CakeException("Could not determine the repository commit for package inspection.");
    }

    return commit;
}

string RequireMetadataValue(System.Xml.Linq.XElement metadata, string name, string packageId)
{
    var elements = metadata.Elements().Where(element => element.Name.LocalName == name).ToArray();
    if (elements.Length != 1 || string.IsNullOrWhiteSpace(elements[0].Value))
    {
        throw new CakeException($"Expected exactly one non-empty '{name}' element in {packageId} metadata.");
    }

    return elements[0].Value;
}

void InspectPortablePdb(FilePath pdb, string repositoryCommit)
{
    using var stream = System.IO.File.OpenRead(pdb.FullPath);
    using var provider = System.Reflection.Metadata.MetadataReaderProvider.FromPortablePdbStream(stream);
    var reader = provider.GetMetadataReader();
    var sourceLinks = reader.CustomDebugInformation
        .Select(handle => reader.GetCustomDebugInformation(handle))
        .Where(information =>
            information.Parent.Kind == System.Reflection.Metadata.HandleKind.ModuleDefinition &&
            reader.GetGuid(information.Kind) == sourceLinkKind)
        .ToArray();

    if (sourceLinks.Length != 1)
    {
        throw new CakeException($"Expected one SourceLink record in portable PDB {pdb}, found {sourceLinks.Length}.");
    }

    using var sourceLink = System.Text.Json.JsonDocument.Parse(reader.GetBlobBytes(sourceLinks[0].Value));
    if (!sourceLink.RootElement.TryGetProperty("documents", out var documents))
    {
        throw new CakeException($"SourceLink record in {pdb} does not contain a documents map.");
    }

    var mappings = documents.EnumerateObject().ToArray();
    var expectedSourcePrefix = $"https://raw.githubusercontent.com/innovorium/dotnet-identity-libraries/{repositoryCommit}/";
    if (mappings.Length == 0 || mappings.Any(mapping =>
        mapping.Value.ValueKind != System.Text.Json.JsonValueKind.String ||
        !mapping.Value.GetString()!.StartsWith(expectedSourcePrefix, StringComparison.Ordinal)))
    {
        throw new CakeException(
            $"SourceLink mappings in {pdb} must reference repository commit {repositoryCommit}.");
    }
}

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(packageDirectory);
        CleanDirectory(inspectionDirectory);
        CleanDirectory(consumerDirectory);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        var settings = new DotNetRestoreSettings();

        if (runningInCi)
        {
            settings.ArgumentCustomization = arguments => arguments.Append("--locked-mode");
        }

        DotNetRestore(solution, settings);
    });

Task("Format")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetFormat(
            solution,
            new DotNetFormatSettings
            {
                NoRestore = true,
                VerifyNoChanges = true
            });
    });

Task("Build")
    .IsDependentOn("Format")
    .Does(() =>
    {
        DotNetBuild(
            solution,
            new DotNetBuildSettings
            {
                Configuration = configuration,
                NoRestore = true
            });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DotNetTest(
            solution,
            new DotNetTestSettings
            {
                Configuration = configuration,
                NoBuild = true,
                NoRestore = true,
                // Both provider suites apply Marten's shared database support
                // objects. Run test projects sequentially to avoid concurrent
                // DDL against the same disposable PostgreSQL service.
                ArgumentCustomization = arguments => arguments.Append("--maxcpucount:1")
            });
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
    {
        DotNetPack(
            solution,
            new DotNetPackSettings
            {
                Configuration = configuration,
                NoBuild = true,
                NoRestore = true,
                OutputDirectory = packageDirectory
            });
    });

Task("Inspect-Packages")
    .IsDependentOn("Pack")
    .Does(() =>
    {
        var repositoryCommit = GetRepositoryCommit();
        var packageFiles = GetFiles($"{packageDirectory}/*.*nupkg");
        if (packageFiles.Count != packageIds.Length * 2)
        {
            throw new CakeException(
                $"Expected exactly {packageIds.Length * 2} package artifacts, found {packageFiles.Count}.");
        }

        string? releaseVersion = null;
        foreach (var packageId in packageIds)
        {
            var package = GetPackage(packageId, ".nupkg");
            var symbols = GetPackage(packageId, ".snupkg");
            var packageContents = inspectionDirectory.Combine(packageId).Combine("package");
            var symbolContents = inspectionDirectory.Combine(packageId).Combine("symbols");

            CleanDirectory(packageContents);
            CleanDirectory(symbolContents);
            Unzip(package, packageContents);
            Unzip(symbols, symbolContents);

            RequireFile(packageContents.CombineWithFilePath("README.md"));
            var nuspec = packageContents.CombineWithFilePath($"{packageId}.nuspec");
            RequireFile(nuspec);
            RequireFile(packageContents.CombineWithFilePath($"lib/net10.0/{packageId}.dll"));
            var pdb = symbolContents.CombineWithFilePath($"lib/net10.0/{packageId}.pdb");
            RequireFile(pdb);

            var metadata = System.Xml.Linq.XDocument
                .Load(nuspec.FullPath)
                .Descendants()
                .Single(element => element.Name.LocalName == "metadata");
            var packageVersion = PackageVersion(package, packageId);
            var nuspecVersion = RequireMetadataValue(metadata, "version", packageId);
            if (!string.Equals(RequireMetadataValue(metadata, "id", packageId), packageId, StringComparison.Ordinal) ||
                !string.Equals(nuspecVersion, packageVersion, StringComparison.Ordinal))
            {
                throw new CakeException($"Package identity or version metadata does not match {package.GetFilename()}.");
            }

            releaseVersion ??= packageVersion;
            if (!string.Equals(releaseVersion, packageVersion, StringComparison.Ordinal))
            {
                throw new CakeException(
                    $"Packages must share one release version; expected {releaseVersion}, found {packageVersion}.");
            }

            var license = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "license");
            if (license is null ||
                !string.Equals((string?)license.Attribute("type"), "expression", StringComparison.Ordinal) ||
                !string.Equals(license.Value, ExpectedLicenseExpression, StringComparison.Ordinal))
            {
                throw new CakeException($"{packageId} must declare the {ExpectedLicenseExpression} license expression.");
            }

            var repository = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "repository");
            if (repository is null ||
                !string.Equals((string?)repository.Attribute("type"), "git", StringComparison.Ordinal) ||
                !string.Equals((string?)repository.Attribute("url"), ExpectedRepositoryUrl, StringComparison.Ordinal) ||
                !string.Equals((string?)repository.Attribute("commit"), repositoryCommit, StringComparison.OrdinalIgnoreCase))
            {
                throw new CakeException(
                    $"{packageId} repository metadata must identify {ExpectedRepositoryUrl} at commit {repositoryCommit}.");
            }

            var dependencyGroups = metadata.Descendants()
                .Where(element => element.Name.LocalName == "group")
                .ToArray();
            if (dependencyGroups.Length != 1 ||
                !string.Equals((string?)dependencyGroups[0].Attribute("targetFramework"), "net10.0", StringComparison.Ordinal))
            {
                throw new CakeException($"{packageId} must contain exactly one net10.0 dependency group.");
            }

            var actualDependencies = dependencyGroups[0].Elements()
                .Where(element => element.Name.LocalName == "dependency")
                .ToDictionary(
                    element => (string?)element.Attribute("id") ?? string.Empty,
                    element => (string?)element.Attribute("version") ?? string.Empty,
                    StringComparer.Ordinal);
            var expectedDependencies = expectedPackageDependencies[packageId];

            if (actualDependencies.Count != expectedDependencies.Count ||
                expectedDependencies.Any(expected =>
                    !actualDependencies.TryGetValue(expected.Key, out var version) ||
                    !string.Equals(version, expected.Value, StringComparison.Ordinal)))
            {
                throw new CakeException(
                    $"Unexpected dependency ranges in {packageId}: expected " +
                    $"[{string.Join(", ", expectedDependencies.Select(dependency => $"{dependency.Key} {dependency.Value}"))}], " +
                    $"found [{string.Join(", ", actualDependencies.Select(dependency => $"{dependency.Key} {dependency.Value}"))}].");
            }

            InspectPortablePdb(pdb, repositoryCommit);
        }
    });

Task("Consumer-Smoke-Test")
    .IsDependentOn("Inspect-Packages")
    .Does(() =>
    {
        var identityPackage = GetPackage(packageIds[0], ".nupkg");
        var version = PackageVersion(identityPackage, packageIds[0]);
        var packageSource = MakeAbsolute(packageDirectory).FullPath;
        var consumers = new[]
        {
            new
            {
                Name = "IdentityConsumer",
                PackageId = packageIds[0],
                Program = string.Join("\n", new[]
                {
                    "using Innovorium.AspNetCore.Identity.Marten;",
                    "using Marten;",
                    "using Microsoft.AspNetCore.Identity;",
                    "using Microsoft.Extensions.DependencyInjection;",
                    "",
                    "var services = new ServiceCollection();",
                    "services.AddMarten(_ => { });",
                    "services.AddIdentityCore<ApplicationUser>()",
                    "    .AddRoles<ApplicationRole>()",
                    "    .AddMartenStores();",
                    "",
                    "sealed class ApplicationUser : MartenIdentityUser;",
                    "sealed class ApplicationRole : MartenIdentityRole;"
                })
            },
            new
            {
                Name = "OpenIddictConsumer",
                PackageId = packageIds[1],
                Program = string.Join("\n", new[]
                {
                    "using Innovorium.OpenIddict.Marten;",
                    "using Marten;",
                    "using Microsoft.Extensions.DependencyInjection;",
                    "",
                    "var services = new ServiceCollection();",
                    "services.AddMarten(_ => { });",
                    "services.AddOpenIddict()",
                    "    .AddCore(options => options.UseMarten());"
                })
            }
        };

        foreach (var consumer in consumers)
        {
            var directory = consumerDirectory.Combine(consumer.Name);
            EnsureDirectoryExists(directory);
            var project = directory.CombineWithFilePath($"{consumer.Name}.csproj");
            var program = directory.CombineWithFilePath("Program.cs");
            var nugetConfig = directory.CombineWithFilePath("NuGet.Config");

            System.IO.File.WriteAllText(
                project.FullPath,
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <IsPackable>false</IsPackable>
                    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <RestorePackagesPath>../packages-cache</RestorePackagesPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="{consumer.PackageId}" Version="{version}" />
                  </ItemGroup>
                </Project>
                """);
            System.IO.File.WriteAllText(program.FullPath, consumer.Program);
            System.IO.File.WriteAllText(
                nugetConfig.FullPath,
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{System.Security.SecurityElement.Escape(packageSource)}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="local"><package pattern="Innovorium.*" /></packageSource>
                    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
                  </packageSourceMapping>
                </configuration>
                """);

            RunProcessOrFail(
                "dotnet",
                new ProcessArgumentBuilder()
                    .Append("restore")
                    .AppendQuoted(project.FullPath)
                    .AppendSwitchQuoted("--configfile", nugetConfig.FullPath));
            RunProcessOrFail(
                "dotnet",
                new ProcessArgumentBuilder()
                    .Append("build")
                    .AppendQuoted(project.FullPath)
                    .Append("--configuration Release --no-restore"));
        }
    });

Task("Checksums")
    .IsDependentOn("Inspect-Packages")
    .Does(() =>
    {
        var packages = GetFiles($"{packageDirectory}/*.*nupkg").OrderBy(path => path.FullPath);
        var lines = packages.Select(path =>
        {
            var bytes = System.IO.File.ReadAllBytes(path.FullPath);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            return $"{hash}  {path.GetFilename()}";
        });

        System.IO.File.WriteAllLines(packageDirectory.CombineWithFilePath("SHA256SUMS").FullPath, lines);
    });

Task("Security")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        DotNetRestore(
            solution,
            new DotNetRestoreSettings
            {
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("NuGetAudit", "true")
                    .WithProperty("NuGetAuditMode", "all")
            });

        RunProcessOrFail(
            "dotnet",
            new ProcessArgumentBuilder()
                .Append("package list")
                .AppendSwitchQuoted("--project", solution)
                .Append("--vulnerable --include-transitive --no-restore"));

        var gitleaks = Context.Tools.Resolve("gitleaks");
        if (gitleaks is null)
        {
            Warning("gitleaks is not installed; the local secret scan was skipped.");
            return;
        }

        RunProcessOrFail(gitleaks, new ProcessArgumentBuilder().Append("git --redact --no-banner"));
        RunProcessOrFail(gitleaks, new ProcessArgumentBuilder().Append("dir . --redact --no-banner"));
    });

Task("Verify")
    .IsDependentOn("Consumer-Smoke-Test")
    .IsDependentOn("Checksums")
    .IsDependentOn("Security");

Task("Release")
    .IsDependentOn("Verify")
    .Does(() =>
    {
        var status = StartAndReturnProcess(
            "git",
            new ProcessSettings { Arguments = "status --porcelain" });
        if (status.GetStandardOutput().Any())
        {
            throw new CakeException("Release requires a clean Git working tree.");
        }

        var tag = EnvironmentVariable("GITHUB_REF_NAME");
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var version = PackageVersion(GetPackage(packageIds[0], ".nupkg"), packageIds[0]);
            if (!string.Equals(tag, $"v{version}", StringComparison.OrdinalIgnoreCase))
            {
                throw new CakeException($"Git tag '{tag}' does not match MinVer package version '{version}'.");
            }
        }
    });

RunTarget(target);
