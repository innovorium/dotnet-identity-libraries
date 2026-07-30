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
var expectedPackageDependencies = new Dictionary<string, string[]>
{
    ["Innovorium.AspNetCore.Identity.Marten"] = new[]
    {
        "Marten",
        "Microsoft.Extensions.Identity.Stores"
    },
    ["Innovorium.OpenIddict.Marten"] = new[]
    {
        "Marten",
        "OpenIddict.Core"
    }
};

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
                NoRestore = true
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
            RequireFile(symbolContents.CombineWithFilePath($"lib/net10.0/{packageId}.pdb"));

            var actualDependencies = System.Xml.Linq.XDocument
                .Load(nuspec.FullPath)
                .Descendants()
                .Where(element => element.Name.LocalName == "dependency")
                .Select(element => (string?)element.Attribute("id"))
                .Where(id => id is not null)
                .Select(id => id!)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var expectedDependencies = expectedPackageDependencies[packageId]
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            if (!actualDependencies.SequenceEqual(expectedDependencies, StringComparer.Ordinal))
            {
                throw new CakeException(
                    $"Unexpected dependencies in {packageId}: expected [{string.Join(", ", expectedDependencies)}], " +
                    $"found [{string.Join(", ", actualDependencies)}].");
            }
        }
    });

Task("Consumer-Smoke-Test")
    .IsDependentOn("Inspect-Packages")
    .Does(() =>
    {
        var identityPackage = GetPackage(packageIds[0], ".nupkg");
        var version = PackageVersion(identityPackage, packageIds[0]);
        var project = consumerDirectory.CombineWithFilePath("Consumer.csproj");
        var program = consumerDirectory.CombineWithFilePath("Program.cs");
        var nugetConfig = consumerDirectory.CombineWithFilePath("NuGet.Config");
        var packageSource = MakeAbsolute(packageDirectory).FullPath;

        System.IO.File.WriteAllText(
            project.FullPath,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>false</IsPackable>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{packageIds[0]}" Version="{version}" />
                <PackageReference Include="{packageIds[1]}" Version="{version}" />
              </ItemGroup>
            </Project>
            """);
        System.IO.File.WriteAllText(program.FullPath, "Console.WriteLine(\"Package consumer smoke test\");");
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
    });

Task("Checksums")
    .IsDependentOn("Pack")
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
