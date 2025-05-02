using System.Diagnostics;
using System.Text.RegularExpressions;

using SharedTools;

namespace Build.Commands;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

public sealed partial class UpdateMetadataCommands
{
    [Command("release", Description = "Prepare & build the next release")]
    public async Task Release(PrepareAction action)
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;
        
        // Prepare the metadata for the next release:
        await this.PerformPrepare(action, true);
        
        // Build once to allow the Rust compiler to read the changed metadata
        // and to update all .NET artifacts:
        await this.Build();
        
        // Now, we update the web assets (which may were updated by the first build):
        new UpdateWebAssetsCommand().UpdateWebAssets();

        // Collect the I18N keys from the source code. This step yields a I18N file
        // that must be part of the final release:
        await new CollectI18NKeysCommand().CollectI18NKeys();
        
        // Build the final release, where Rust knows the updated metadata, the .NET
        // artifacts are already in place, and .NET knows the updated web assets, etc.:
        await this.Build();
    }

    [Command("update-versions", Description = "The command will update the package versions in the metadata file")]
    public async Task UpdateVersions()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("==============================");
        Console.WriteLine("- Update the main package versions ...");
        
        await this.UpdateDotnetVersion();
        await this.UpdateRustVersion();
        await this.UpdateMudBlazorVersion();
        await this.UpdateTauriVersion();
    }
    
    [Command("prepare", Description = "Prepare the metadata for the next release")]
    public async Task Prepare(PrepareAction action)
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("==============================");
        Console.Write("- Are you trying to prepare a new release? (y/n) ");
        var userAnswer = Console.ReadLine();
        if (userAnswer?.ToLowerInvariant() == "y")
        {
            Console.WriteLine("- Please use the 'release' command instead");
            return;
        }
        
        await this.PerformPrepare(action, false);
    }

    private async Task PerformPrepare(PrepareAction action, bool internalCall)
    {
        if(internalCall)
            Console.WriteLine("==============================");
        
        Console.WriteLine("- Prepare the metadata for the next release ...");
        
        var appVersion = await this.UpdateAppVersion(action);
        if (!string.IsNullOrWhiteSpace(appVersion.VersionText))
        {
            var buildNumber = await this.IncreaseBuildNumber();
            var buildTime = await this.UpdateBuildTime();
            await this.UpdateChangelog(buildNumber, appVersion.VersionText, buildTime);
            await this.CreateNextChangelog(buildNumber, appVersion);
            await this.UpdateDotnetVersion();
            await this.UpdateRustVersion();
            await this.UpdateMudBlazorVersion();
            await this.UpdateTauriVersion();
            await this.UpdateProjectCommitHash();
            await this.UpdateLicenceYear(Path.GetFullPath(Path.Combine(Environment.GetAIStudioDirectory(), "..", "..", "LICENSE.md")));
            await this.UpdateLicenceYear(Path.GetFullPath(Path.Combine(Environment.GetAIStudioDirectory(), "Pages", "About.razor.cs")));
            Console.WriteLine();
        }
    }
    
    [Command("build", Description = "Build MindWork AI Studio")]
    public async Task Build()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;
        
        //
        // Build the .NET project:
        //
        var pathApp = Environment.GetAIStudioDirectory();
        var rid = Environment.GetCurrentRid();
        
        Console.WriteLine("==============================");
        await this.UpdateArchitecture(rid);
        
        var pdfiumVersion = await this.ReadPdfiumVersion();
        await Pdfium.InstallAsync(rid, pdfiumVersion);
        
        Console.Write($"- Start .NET build for {rid.ToUserFriendlyName()} ...");
        await this.ReadCommandOutput(pathApp, "dotnet", $"clean --configuration release --runtime {rid.AsMicrosoftRid()}");
        var dotnetBuildOutput = await this.ReadCommandOutput(pathApp, "dotnet", $"publish --configuration release --runtime {rid.AsMicrosoftRid()} --disable-build-servers --force");
        var dotnetBuildOutputLines = dotnetBuildOutput.Split([global::System.Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        var foundIssue = false;
        foreach (var buildOutputLine in dotnetBuildOutputLines)
        {
            if(buildOutputLine.Contains(" error ") || buildOutputLine.Contains("#warning"))
            {
                if(!foundIssue)
                {
                    foundIssue = true;
                    Console.WriteLine();
                    Console.WriteLine("- Build has issues:");
                }

                Console.Write("   - ");
                Console.WriteLine(buildOutputLine);
            }
        }
        
        if(foundIssue)
            Console.WriteLine();
        else
        {
            Console.WriteLine(" completed successfully.");
        }
        
        //
        // Prepare the .NET artifact to be used by Tauri as sidecar:
        //
        var os = Environment.GetOS();
        var tauriSidecarArtifactName = rid switch
        {
            RID.WIN_X64 => "mindworkAIStudioServer-x86_64-pc-windows-msvc.exe",
            RID.WIN_ARM64 => "mindworkAIStudioServer-aarch64-pc-windows-msvc.exe",
            
            RID.LINUX_X64 => "mindworkAIStudioServer-x86_64-unknown-linux-gnu",
            RID.LINUX_ARM64 => "mindworkAIStudioServer-aarch64-unknown-linux-gnu",
            
            RID.OSX_ARM64 => "mindworkAIStudioServer-aarch64-apple-darwin",
            RID.OSX_X64 => "mindworkAIStudioServer-x86_64-apple-darwin",
            
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(tauriSidecarArtifactName))
        {
            Console.WriteLine($"- Error: Unsupported rid '{rid.AsMicrosoftRid()}'.");
            return;
        }
    
        var dotnetArtifactPath = Path.Combine(pathApp, "bin", "dist");
        if(!Directory.Exists(dotnetArtifactPath))
            Directory.CreateDirectory(dotnetArtifactPath);
        
        var dotnetArtifactFilename = os switch
        {
            "windows" => "mindworkAIStudio.exe",
            _ => "mindworkAIStudio",
        };
        
        var dotnetPublishedPath = Path.Combine(pathApp, "bin", "release", Environment.DOTNET_VERSION, rid.AsMicrosoftRid(), "publish", dotnetArtifactFilename);
        var finalDestination = Path.Combine(dotnetArtifactPath, tauriSidecarArtifactName);
        
        if(File.Exists(dotnetPublishedPath))
            Console.WriteLine("- Published .NET artifact found.");
        else
        {
            Console.WriteLine($"- Error: Published .NET artifact not found: '{dotnetPublishedPath}'.");
            return;
        }

        Console.Write($"- Move the .NET artifact to the Tauri sidecar destination ...");
        try
        {
            File.Move(dotnetPublishedPath, finalDestination, true);
            Console.WriteLine(" done.");
        }
        catch (Exception e)
        {
            Console.WriteLine(" failed.");
            Console.WriteLine($"   - Error: {e.Message}");
        }
        
        //
        // Build the Rust project / runtime:
        //
        Console.WriteLine("- Start building the Rust runtime ...");
        
        var pathRuntime = Environment.GetRustRuntimeDirectory();
        var rustBuildOutput = await this.ReadCommandOutput(pathRuntime, "cargo", "tauri build --bundles none", true);
        var rustBuildOutputLines = rustBuildOutput.Split([global::System.Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        var foundRustIssue = false;
        foreach (var buildOutputLine in rustBuildOutputLines)
        {
            if(buildOutputLine.Contains("error", StringComparison.OrdinalIgnoreCase) || buildOutputLine.Contains("warning"))
            {
                if(!foundRustIssue)
                {
                    foundRustIssue = true;
                    Console.WriteLine();
                    Console.WriteLine("- Build has issues:");
                }

                Console.Write("   - ");
                Console.WriteLine(buildOutputLine);
            }
        }
        
        if(foundRustIssue)
            Console.WriteLine();
        else
        {
            Console.WriteLine();
            Console.WriteLine("- Compilation completed successfully.");
            Console.WriteLine();
        }
    }

    private async Task CreateNextChangelog(int currentBuildNumber, AppVersion currentAppVersion)
    {
        Console.Write("- Create the next changelog ...");
        var pathChangelogs = Path.Combine(Environment.GetAIStudioDirectory(), "wwwroot", "changelog");
        var nextBuildNumber = currentBuildNumber + 1;
        
        //
        // We assume that most of the time, there will be patch releases:
        //
        var nextMajor = currentAppVersion.Major;
        var nextMinor = currentAppVersion.Minor;
        var nextPatch = currentAppVersion.Patch + 1;
        
        var nextAppVersion = $"{nextMajor}.{nextMinor}.{nextPatch}";
        var nextChangelogFilename = $"v{nextAppVersion}.md";
        var nextChangelogFilePath = Path.Combine(pathChangelogs, nextChangelogFilename);
        
        //
        // Regarding the next build time: We assume that the next release will take place in one week from now.
        // Thus, we check how many days this month has left. In the end, we want to predict the year and month
        // for the next build. Day, hour, minute and second are all set to x.
        //
        var nextBuildMonth = (DateTime.Today + TimeSpan.FromDays(7)).Month;
        var nextBuildYear = (DateTime.Today + TimeSpan.FromDays(7)).Year;
        var nextBuildTimeString = $"{nextBuildYear}-{nextBuildMonth:00}-xx xx:xx UTC";

        var changelogHeader = $"""
                               # v{nextAppVersion}, build {nextBuildNumber} ({nextBuildTimeString})
                               
                               """;
        
        if(!File.Exists(nextChangelogFilePath))
        {
            await File.WriteAllTextAsync(nextChangelogFilePath, changelogHeader, Environment.UTF8_NO_BOM);
            Console.WriteLine($" done. Changelog '{nextChangelogFilename}' created.");
        }
        else
        {
            Console.WriteLine(" failed.");
            Console.WriteLine("- Error: The changelog file already exists.");
        }
    }

    private async Task UpdateChangelog(int buildNumber, string appVersion, string buildTime)
    {
        Console.Write("- Updating the in-app changelog list ...");
        var pathChangelogs = Path.Combine(Environment.GetAIStudioDirectory(), "wwwroot", "changelog");
        var expectedLogFilename = $"v{appVersion}.md";
        var expectedLogFilePath = Path.Combine(pathChangelogs, expectedLogFilename);
        
        if(!File.Exists(expectedLogFilePath))
        {
            Console.WriteLine(" failed.");
            Console.WriteLine($"- Error: The changelog file '{expectedLogFilename}' does not exist.");
            return;
        }

        // Right now, the build time is formatted as "yyyy-MM-dd HH:mm:ss UTC", but must remove the seconds:
        buildTime = buildTime[..^7] + " UTC";
        
        const string CODE_START =
        """
        LOGS = 
            [
        """;
        
        var changelogCodePath = Path.Join(Environment.GetAIStudioDirectory(), "Components", "Changelog.Logs.cs");
        var changelogCode = await File.ReadAllTextAsync(changelogCodePath, Encoding.UTF8);
        var updatedCode =
        $"""
        {CODE_START}
                new ({buildNumber}, "v{appVersion}, build {buildNumber} ({buildTime})", "{expectedLogFilename}"),
        """;
        
        changelogCode = changelogCode.Replace(CODE_START, updatedCode);
        await File.WriteAllTextAsync(changelogCodePath, changelogCode, Environment.UTF8_NO_BOM);
        Console.WriteLine(" done.");
    }
    
    private async Task<string> ReadPdfiumVersion()
    {
        const int PDFIUM_VERSION_INDEX = 10;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentPdfiumVersion = lines[PDFIUM_VERSION_INDEX].Trim();
        var shortVersion = currentPdfiumVersion.Split('.')[2];
        
        return shortVersion;
    }

    private async Task UpdateArchitecture(RID rid)
    {
        const int ARCHITECTURE_INDEX = 9;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        Console.Write($"- Updating architecture to {rid.ToUserFriendlyName()} ...");
        lines[ARCHITECTURE_INDEX] = rid.AsMicrosoftRid();
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
        Console.WriteLine(" done.");
    }

    private async Task UpdateProjectCommitHash()
    {
        const int COMMIT_HASH_INDEX = 8;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentCommitHash = lines[COMMIT_HASH_INDEX].Trim();
        var headCommitHash = await this.ReadCommandOutput(Environment.GetAIStudioDirectory(), "git", "rev-parse HEAD");
        var first10Chars = headCommitHash[..11];
        var updatedCommitHash = $"{first10Chars}, release";

        Console.WriteLine($"- Updating commit hash from '{currentCommitHash}' to '{updatedCommitHash}'.");
        lines[COMMIT_HASH_INDEX] = updatedCommitHash;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }

    private async Task<AppVersion> UpdateAppVersion(PrepareAction action)
    {
        const int APP_VERSION_INDEX = 0;
        
        if (action == PrepareAction.NONE)
        {
            Console.WriteLine("- No action specified. Skipping app version update.");
            return new(string.Empty, 0, 0, 0);
        }
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentAppVersionLine = lines[APP_VERSION_INDEX].Trim();
        var currentAppVersion = AppVersionRegex().Match(currentAppVersionLine);
        var currentPatch = int.Parse(currentAppVersion.Groups["patch"].Value);
        var currentMinor = int.Parse(currentAppVersion.Groups["minor"].Value);
        var currentMajor = int.Parse(currentAppVersion.Groups["major"].Value);
        
        switch (action)
        {
            case PrepareAction.PATCH:
                currentPatch++;
                break;
            
            case PrepareAction.MINOR:
                currentPatch = 0;
                currentMinor++;
                break;
            
            case PrepareAction.MAJOR:
                currentPatch = 0;
                currentMinor = 0;
                currentMajor++;
                break;
        }
        
        var updatedAppVersion = $"{currentMajor}.{currentMinor}.{currentPatch}";
        Console.WriteLine($"- Updating app version from '{currentAppVersionLine}' to '{updatedAppVersion}'.");
        
        lines[APP_VERSION_INDEX] = updatedAppVersion;
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
        
        return new(updatedAppVersion, currentMajor, currentMinor, currentPatch);
    }

    private async Task UpdateLicenceYear(string licenceFilePath)
    {
        var currentYear = DateTime.UtcNow.Year.ToString();
        var lines = await File.ReadAllLinesAsync(licenceFilePath, Encoding.UTF8);

        var found = false;
        var copyrightYear = string.Empty;
        var updatedLines = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var match = FindCopyrightRegex().Match(line);
            if (match.Success)
            {
                copyrightYear = match.Groups["year"].Value;
                
                if(!found && copyrightYear != currentYear)
                    Console.WriteLine($"- Updating the licence's year in '{Path.GetFileName(licenceFilePath)}' from '{copyrightYear}' to '{currentYear}'.");
                
                updatedLines.Add(ReplaceCopyrightYearRegex().Replace(line, currentYear));
                found = true;
            }
            else
                updatedLines.Add(line);
        }
        
        await File.WriteAllLinesAsync(licenceFilePath, updatedLines, Environment.UTF8_NO_BOM);
        if (!found)
            Console.WriteLine($"- Error: No copyright year found in '{Path.GetFileName(licenceFilePath)}'.");
        else if (copyrightYear == currentYear)
            Console.WriteLine($"- The copyright year in '{Path.GetFileName(licenceFilePath)}' is already up to date.");
    }
    
    private async Task UpdateTauriVersion()
    {
        const int TAURI_VERSION_INDEX = 7;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentTauriVersion = lines[TAURI_VERSION_INDEX].Trim();
        
        var matches = await this.DetermineVersion("Tauri", Environment.GetRustRuntimeDirectory(), TauriVersionRegex(), "cargo", "tree --depth 1");
        if (matches.Count == 0)
            return;
        
        var updatedTauriVersion = matches[0].Groups["version"].Value;
        if(currentTauriVersion == updatedTauriVersion)
        {
            Console.WriteLine("- The Tauri version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated Tauri version from {currentTauriVersion} to {updatedTauriVersion}.");
        lines[TAURI_VERSION_INDEX] = updatedTauriVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }
    
    private async Task UpdateMudBlazorVersion()
    {
        const int MUD_BLAZOR_VERSION_INDEX = 6;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentMudBlazorVersion = lines[MUD_BLAZOR_VERSION_INDEX].Trim();
        
        var matches = await this.DetermineVersion("MudBlazor", Environment.GetAIStudioDirectory(), MudBlazorVersionRegex(), "dotnet", "list package");
        if (matches.Count == 0)
            return;
        
        var updatedMudBlazorVersion = matches[0].Groups["version"].Value;
        if(currentMudBlazorVersion == updatedMudBlazorVersion)
        {
            Console.WriteLine("- The MudBlazor version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated MudBlazor version from {currentMudBlazorVersion} to {updatedMudBlazorVersion}.");
        lines[MUD_BLAZOR_VERSION_INDEX] = updatedMudBlazorVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }

    private async Task UpdateRustVersion()
    {
        const int RUST_VERSION_INDEX = 5;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentRustVersion = lines[RUST_VERSION_INDEX].Trim();
        var matches = await this.DetermineVersion("Rust", Environment.GetRustRuntimeDirectory(), RustVersionRegex(), "rustc", "-Vv");
        if (matches.Count == 0)
            return;
        
        var updatedRustVersion = matches[0].Groups["version"].Value + " (commit " + matches[0].Groups["commit"].Value + ")";
        if(currentRustVersion == updatedRustVersion)
        {
            Console.WriteLine("- Rust version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated Rust version from {currentRustVersion} to {updatedRustVersion}.");
        lines[RUST_VERSION_INDEX] = updatedRustVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }

    private async Task UpdateDotnetVersion()
    {
        const int DOTNET_VERSION_INDEX = 4;
        const int DOTNET_SDK_VERSION_INDEX = 3;
        
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentDotnetVersion = lines[DOTNET_VERSION_INDEX].Trim();
        var currentDotnetSdkVersion = lines[DOTNET_SDK_VERSION_INDEX].Trim();
        
        var matches = await this.DetermineVersion(".NET", Environment.GetAIStudioDirectory(), DotnetVersionRegex(), "dotnet", "--info");
        if (matches.Count == 0)
            return;
        
        var updatedDotnetVersion = matches[0].Groups["hostVersion"].Value + " (commit " + matches[0].Groups["hostCommit"].Value + ")";
        var updatedDotnetSdkVersion = matches[0].Groups["sdkVersion"].Value + " (commit " + matches[0].Groups["sdkCommit"].Value + ")";
        if(currentDotnetVersion == updatedDotnetVersion && currentDotnetSdkVersion == updatedDotnetSdkVersion)
        {
            Console.WriteLine("- .NET version is already up to date.");
            return;
        }
        
        Console.WriteLine($"- Updated .NET SDK version from {currentDotnetSdkVersion} to {updatedDotnetSdkVersion}.");
        Console.WriteLine($"- Updated .NET version from {currentDotnetVersion} to {updatedDotnetVersion}.");

        lines[DOTNET_VERSION_INDEX] = updatedDotnetVersion;
        lines[DOTNET_SDK_VERSION_INDEX] = updatedDotnetSdkVersion;
        
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
    }
    
    private async Task<IList<Match>> DetermineVersion(string name, string workingDirectory,  Regex regex, string program, string command)
    {
        var processInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            FileName = program,
            Arguments = command,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process();
        process.StartInfo = processInfo;
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        var matches = regex.Matches(output);
        if (matches.Count == 0)
        {
            Console.WriteLine($"- Error: Was not able to determine the {name} version.");
            return [];
        }
        
        return matches;
    }
    
    private async Task<string> ReadCommandOutput(string workingDirectory, string program, string command, bool showLiveOutput = false)
    {
        var processInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            FileName = program,
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var sb = new StringBuilder();
        using var process = new Process();
        process.StartInfo = processInfo;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        process.OutputDataReceived += (_, args) =>
        {
            if(!string.IsNullOrWhiteSpace(args.Data))
            {
                if(showLiveOutput)
                    Console.WriteLine(args.Data);
                sb.AppendLine(args.Data);
            }
        };
        
        process.ErrorDataReceived += (_, args) =>
        {
            if(!string.IsNullOrWhiteSpace(args.Data))
            {
                if(showLiveOutput)
                    Console.WriteLine(args.Data);
                sb.AppendLine(args.Data);
            }
        };
        
        await process.WaitForExitAsync();
        return sb.ToString();
    }
    
    private async Task<int> IncreaseBuildNumber()
    {
        const int BUILD_NUMBER_INDEX = 2;
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var buildNumber = int.Parse(lines[BUILD_NUMBER_INDEX]) + 1;

        Console.WriteLine($"- Updating build number from '{lines[BUILD_NUMBER_INDEX]}' to '{buildNumber}'.");
        
        lines[BUILD_NUMBER_INDEX] = buildNumber.ToString();
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
        return buildNumber;
    }
    
    private async Task<string> UpdateBuildTime()
    {
        const int BUILD_TIME_INDEX = 1;
        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        Console.WriteLine($"- Updating build time from '{lines[BUILD_TIME_INDEX]}' to '{buildTime}'.");
        
        lines[BUILD_TIME_INDEX] = buildTime;
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);
        return buildTime;
    }

    [GeneratedRegex("""(?ms).?(NET\s+SDK|SDK\s+\.NET)\s*:\s+Version:\s+(?<sdkVersion>[0-9.]+).+Commit:\s+(?<sdkCommit>[a-zA-Z0-9]+).+Host:\s+Version:\s+(?<hostVersion>[0-9.]+).+Commit:\s+(?<hostCommit>[a-zA-Z0-9]+)""")]
    private static partial Regex DotnetVersionRegex();
    
    [GeneratedRegex("""rustc (?<version>[0-9.]+)(?:-nightly)? \((?<commit>[a-zA-Z0-9]+)""")]
    private static partial Regex RustVersionRegex();
    
    [GeneratedRegex("""MudBlazor\s+(?<version>[0-9.]+)""")]
    private static partial Regex MudBlazorVersionRegex();
    
    [GeneratedRegex("""tauri\s+v(?<version>[0-9.]+)""")]
    private static partial Regex TauriVersionRegex();
    
    [GeneratedRegex("""^\s*Copyright\s+(?<year>[0-9]{4})""")]
    private static partial Regex FindCopyrightRegex();

    [GeneratedRegex("""([0-9]{4})""")]
    private static partial Regex ReplaceCopyrightYearRegex();
    
    [GeneratedRegex("""(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)""")]
    private static partial Regex AppVersionRegex();
}