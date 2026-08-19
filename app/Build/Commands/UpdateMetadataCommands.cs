using System.Diagnostics;
using System.Globalization;
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
    public async Task Release(
        [Option("action", ['a'], Description = "The release action: patch, minor, or major")] PrepareAction action = PrepareAction.NONE,
        [Option("version", ['v'], Description = "Set a specific version directly, e.g., 26.1.2")] string? version = null,
        [Option("offline", Description = "Skip downloads and use locally available build dependencies")] bool offline = false)
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        // Validate parameters: either action or version must be specified, but not both:
        if (action == PrepareAction.NONE && string.IsNullOrWhiteSpace(version))
        {
            Console.WriteLine("- Error: You must specify either --action (-a) or --version (-v).");
            return;
        }

        if (action != PrepareAction.NONE && !string.IsNullOrWhiteSpace(version))
        {
            Console.WriteLine("- Error: You cannot specify both --action and --version. Please use only one.");
            return;
        }

        // If version is specified, use SET action:
        if (!string.IsNullOrWhiteSpace(version))
            action = PrepareAction.SET;

        // Prepare the metadata for the next release:
        await this.PerformPrepare(action, true, version);

        await this.BuildPreparedRelease(offline);
    }

    [Command("rebuild-release", Description = "Prepare & build a new build of the current release")]
    public async Task RebuildRelease(
        [Option("offline", Description = "Skip downloads and use locally available build dependencies")] bool offline = false)
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("==============================");
        Console.WriteLine("- Prepare a new build of the current release ...");

        RebuildReleaseState releaseState;
        try
        {
            releaseState = await this.ValidateRebuildReleaseState();
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"- Error: {exception.Message}");
            return;
        }

        await this.ApplyRebuildReleaseState(releaseState, DateTime.UtcNow);
        await this.UpdateReleaseDependenciesAndLicence();
        Console.WriteLine();

        await this.BuildPreparedRelease(offline);
    }

    private async Task BuildPreparedRelease(bool offline)
    {
        // Build once to allow the Rust compiler to read the changed metadata
        // and to update all .NET artifacts:
        await this.Build(offline);
        
        // Now, we update the web assets (which may were updated by the first build):
        new UpdateWebAssetsCommand().UpdateWebAssets();

        // Collect the I18N keys from the source code. This step yields a I18N file
        // that must be part of the final release:
        await new CollectI18NKeysCommand().CollectI18NKeys();
        
        // Build the final release, where Rust knows the updated metadata, the .NET
        // artifacts are already in place, and .NET knows the updated web assets, etc.:
        await this.Build(offline);
    }

    [Command("update-metainfo", Description = "Update the AppStream metainfo entry of one release from its changelog")]
    public async Task UpdateMetainfo(
        [Option("version", ['v'], Description = "The release version, e.g., 26.1.2. Defaults to the version from the metadata")] string? version = null,
        [Option("date", ['d'], Description = "The release date as yyyy-MM-dd. Defaults to the build time from the metadata")] string? date = null)
    {
        const int APP_VERSION_INDEX = 0;
        const int BUILD_TIME_INDEX = 1;

        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("==============================");

        try
        {
            var metadataLines = SplitLines(await File.ReadAllTextAsync(Environment.GetMetadataPath(), Encoding.UTF8));
            var appVersion = string.IsNullOrWhiteSpace(version) ? metadataLines[APP_VERSION_INDEX].Trim() : version.Trim();
            if (!ExactAppVersionRegex().IsMatch(appVersion))
                throw new InvalidOperationException($"The version '{appVersion}' is not a valid app version.");

            DateTime releaseTime;
            if (string.IsNullOrWhiteSpace(date))
                releaseTime = ParseMetadataBuildTime(metadataLines[BUILD_TIME_INDEX]);
            else if (!DateTime.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out releaseTime))
                throw new InvalidOperationException($"The release date '{date}' is not a valid date in the yyyy-MM-dd format.");

            await WriteMetainfoRelease(appVersion, releaseTime);
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"- Error: {exception.Message}");
        }
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
        await this.UpdateVectorStoreVersion();
    }
    
    [Command("prepare", Description = "Prepare the metadata for the next release")]
    public async Task Prepare(
        [Option("action", ['a'], Description = "The release action: patch, minor, or major")] PrepareAction action = PrepareAction.NONE,
        [Option("version", ['v'], Description = "Set a specific version directly, e.g., 26.1.2")] string? version = null)
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        // Validate parameters: either action or version must be specified, but not both:
        if (action == PrepareAction.NONE && string.IsNullOrWhiteSpace(version))
        {
            Console.WriteLine("- Error: You must specify either --action (-a) or --version (-v).");
            return;
        }

        if (action != PrepareAction.NONE && !string.IsNullOrWhiteSpace(version))
        {
            Console.WriteLine("- Error: You cannot specify both --action and --version. Please use only one.");
            return;
        }

        // If version is specified, use SET action:
        if (!string.IsNullOrWhiteSpace(version))
            action = PrepareAction.SET;

        Console.WriteLine("==============================");
        Console.Write("- Are you trying to prepare a new release? (y/n) ");
        var userAnswer = Console.ReadLine();
        if (userAnswer?.ToLowerInvariant() == "y")
        {
            Console.WriteLine("- Please use the 'release' command instead");
            return;
        }

        await this.PerformPrepare(action, false, version);
    }

    private async Task PerformPrepare(PrepareAction action, bool internalCall, string? version = null)
    {
        if(internalCall)
            Console.WriteLine("==============================");

        Console.WriteLine("- Prepare the metadata for the next release ...");

        var appVersion = await this.UpdateAppVersion(action, version);
        if (!string.IsNullOrWhiteSpace(appVersion.VersionText))
        {
            // The changelog is the source for the AppStream description. Check it before we write
            // any further metadata, so that a missing changelog cannot leave a half-prepared release:
            var changelogPath = GetChangelogPath(appVersion.VersionText);
            if (!File.Exists(changelogPath))
            {
                Console.WriteLine($"- Error: The changelog file '{Path.GetFileName(changelogPath)}' does not exist.");
                return;
            }

            var buildNumber = await this.IncreaseBuildNumber();
            var buildTime = await this.UpdateBuildTime();
            await this.UpdateChangelog(buildNumber, appVersion.VersionText, buildTime);
            await this.CreateNextChangelog(buildNumber, appVersion);
            await WriteMetainfoRelease(appVersion.VersionText, ParseMetadataBuildTime(buildTime));
            await this.UpdateProjectCommitHash();
            await this.UpdateReleaseDependenciesAndLicence();
            Console.WriteLine();
        }
    }

    private async Task UpdateReleaseDependenciesAndLicence()
    {
        await this.UpdateDotnetVersion();
        await this.UpdateRustVersion();
        await this.UpdateMudBlazorVersion();
        await this.UpdateTauriVersion();
        await this.UpdateVectorStoreVersion();
        await this.UpdateLicenceYear(Path.GetFullPath(Path.Combine(Environment.GetAIStudioDirectory(), "..", "..", "LICENSE.md")));
        await this.UpdateLicenceYear(Path.GetFullPath(Path.Combine(Environment.GetAIStudioDirectory(), "Pages", "Information.razor.cs")));
    }
    
    [Command("build", Description = "Build MindWork AI Studio")]
    public async Task Build(
        [Option("offline", Description = "Skip downloads and use locally available build dependencies")] bool offline = false)
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
        await this.UpdateTauriVersion();
        await this.UpdateVectorStoreVersion();
        
        var pdfiumVersion = await this.ReadPdfiumVersion();
        await Pdfium.InstallAsync(rid, pdfiumVersion, Environment.IsOfflineBuildRequested(offline));
        
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
        var rustBuildOutput = await this.ReadCommandOutput(pathRuntime, "cargo", "tauri build --no-bundle", true);
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
        // Regarding the next build time: We assume that the next release will take place in one week from now.
        // Thus, we check how many days this month has left. In the end, we want to predict the year and month
        // for the next build. Day, hour, minute and second are all set to x.
        //
        var nextBuildMonth = (DateTime.Today + TimeSpan.FromDays(7)).Month;
        var nextBuildYear = (DateTime.Today + TimeSpan.FromDays(7)).Year;
        var nextBuildTimeString = $"{nextBuildYear}-{nextBuildMonth:00}-xx xx:xx UTC";

        //
        // We assume that most of the time, there will be patch releases:
        //
        // skipping the first 2 digits for major version
        var nextBuildYearShort = nextBuildYear - 2000;
        var nextMajor = nextBuildYearShort;
        var nextMinor = nextBuildMonth;
        var nextPatch = currentAppVersion.Major != nextBuildYearShort || currentAppVersion.Minor != nextBuildMonth ? 1 : currentAppVersion.Patch + 1;
        
        var nextAppVersion = $"{nextMajor}.{nextMinor}.{nextPatch}";
        var nextChangelogFilename = $"v{nextAppVersion}.md";
        var nextChangelogFilePath = Path.Combine(pathChangelogs, nextChangelogFilename);
        
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

    private async Task<RebuildReleaseState> ValidateRebuildReleaseState()
    {
        const int APP_VERSION_INDEX = 0;
        const int BUILD_TIME_INDEX = 1;
        const int BUILD_NUMBER_INDEX = 2;

        var metadataPath = Environment.GetMetadataPath();
        var metadataContent = await File.ReadAllTextAsync(metadataPath, Encoding.UTF8);
        var metadataLines = SplitLines(metadataContent);
        if (metadataLines.Length <= 8)
            throw new InvalidOperationException("The metadata file does not contain all required release fields.");

        var appVersion = metadataLines[APP_VERSION_INDEX].Trim();
        if (!ExactAppVersionRegex().IsMatch(appVersion))
            throw new InvalidOperationException($"The metadata version '{appVersion}' is not a valid app version.");

        var buildTime = ParseMetadataBuildTime(metadataLines[BUILD_TIME_INDEX]);
        if (!int.TryParse(metadataLines[BUILD_NUMBER_INDEX].Trim(), out var buildNumber))
            throw new InvalidOperationException($"The metadata build number '{metadataLines[BUILD_NUMBER_INDEX]}' is not a number.");

        var changelogDirectory = Path.Combine(Environment.GetAIStudioDirectory(), "wwwroot", "changelog");
        var changelogFilename = $"v{appVersion}.md";
        var changelogPath = Path.Combine(changelogDirectory, changelogFilename);
        if (!File.Exists(changelogPath))
            throw new InvalidOperationException($"The current changelog file '{changelogFilename}' does not exist.");

        var changelogContent = await File.ReadAllTextAsync(changelogPath, Encoding.UTF8);
        var changelogHeader = FormatChangelogHeader(appVersion, buildNumber, buildTime);
        if (GetFirstLine(changelogContent) != changelogHeader)
            throw new InvalidOperationException($"The current changelog header does not match v{appVersion}, build {buildNumber}, and the metadata build time.");

        var changelogCodePath = Path.Combine(Environment.GetAIStudioDirectory(), "Components", "Changelog.Logs.cs");
        var changelogCode = await File.ReadAllTextAsync(changelogCodePath, Encoding.UTF8);
        var changelogLogEntry = FormatChangelogLogEntry(appVersion, buildNumber, buildTime, changelogFilename);
        if (CountOccurrences(changelogCode, changelogLogEntry) != 1)
            throw new InvalidOperationException($"The in-app changelog list must contain exactly one matching entry for v{appVersion}, build {buildNumber}.");

        var nextChangelogBuildNumber = buildNumber + 1;
        var nextChangelogPattern = new Regex($"^# v(?<version>[0-9]+\\.[0-9]+\\.[0-9]+), build {nextChangelogBuildNumber} \\(20[0-9]{{2}}-[0-9]{{2}}-xx xx:xx UTC\\)$");
        var nextChangelogCandidates = new List<(string Path, string Content, string Header, string Version)>();
        foreach (var candidatePath in Directory.GetFiles(changelogDirectory, "v*.md"))
        {
            if (candidatePath == changelogPath)
                continue;

            var candidateContent = await File.ReadAllTextAsync(candidatePath, Encoding.UTF8);
            var candidateHeader = GetFirstLine(candidateContent);
            var candidateMatch = nextChangelogPattern.Match(candidateHeader);
            if (candidateMatch.Success)
                nextChangelogCandidates.Add((candidatePath, candidateContent, candidateHeader, candidateMatch.Groups["version"].Value));
        }

        if (nextChangelogCandidates.Count != 1)
            throw new InvalidOperationException($"Expected exactly one future changelog reserving build {nextChangelogBuildNumber}, but found {nextChangelogCandidates.Count}.");

        var nextChangelog = nextChangelogCandidates[0];

        // The release entry itself is written by ApplyRebuildReleaseState, which adds it when it is
        // missing and moves it to the top otherwise. Here, we only ensure that there is a file to write to:
        var metainfoPath = GetMetainfoPath();
        if (!File.Exists(metainfoPath))
            throw new InvalidOperationException("The AppStream metainfo file does not exist.");

        if (!ReleasesStartRegex().IsMatch(await File.ReadAllTextAsync(metainfoPath, Encoding.UTF8)))
            throw new InvalidOperationException("The AppStream metainfo does not contain a <releases> element.");

        var headCommitHash = (await this.ReadCommandOutput(Environment.GetAIStudioDirectory(), "git", "rev-parse HEAD")).Trim();
        if (!GitCommitHashRegex().IsMatch(headCommitHash))
            throw new InvalidOperationException("The current Git commit hash could not be determined.");

        return new(
            metadataPath,
            metadataContent,
            metadataLines,
            appVersion,
            buildNumber,
            changelogPath,
            changelogContent,
            changelogHeader,
            changelogCodePath,
            changelogCode,
            changelogLogEntry,
            nextChangelog.Path,
            nextChangelog.Content,
            nextChangelog.Header,
            nextChangelog.Version,
            headCommitHash[..11]);
    }

    private async Task ApplyRebuildReleaseState(RebuildReleaseState releaseState, DateTime buildTime)
    {
        const int BUILD_TIME_INDEX = 1;
        const int BUILD_NUMBER_INDEX = 2;
        const int COMMIT_HASH_INDEX = 8;

        buildTime = buildTime.ToUniversalTime();
        var buildNumber = releaseState.BuildNumber + 1;
        var buildTimeString = buildTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

        Console.WriteLine($"- Updating build number from '{releaseState.BuildNumber}' to '{buildNumber}'.");
        Console.WriteLine($"- Updating build time to '{buildTimeString}'.");

        releaseState.MetadataLines[BUILD_TIME_INDEX] = buildTimeString;
        releaseState.MetadataLines[BUILD_NUMBER_INDEX] = buildNumber.ToString(CultureInfo.InvariantCulture);
        releaseState.MetadataLines[COMMIT_HASH_INDEX] = $"{releaseState.HeadCommitHash}, release";
        var updatedMetadata = JoinLines(releaseState.MetadataContent, releaseState.MetadataLines);
        await File.WriteAllTextAsync(releaseState.MetadataPath, updatedMetadata, Environment.UTF8_NO_BOM);

        var updatedChangelogHeader = FormatChangelogHeader(releaseState.AppVersion, buildNumber, buildTime);
        var updatedChangelog = ReplaceExactlyOnce(releaseState.ChangelogContent, releaseState.ChangelogHeader, updatedChangelogHeader);
        await File.WriteAllTextAsync(releaseState.ChangelogPath, updatedChangelog, Environment.UTF8_NO_BOM);
        Console.WriteLine($"- Updated the header of '{Path.GetFileName(releaseState.ChangelogPath)}'.");

        var changelogFilename = Path.GetFileName(releaseState.ChangelogPath);
        var updatedChangelogLogEntry = FormatChangelogLogEntry(releaseState.AppVersion, buildNumber, buildTime, changelogFilename);
        var updatedChangelogCode = ReplaceExactlyOnce(releaseState.ChangelogCode, releaseState.ChangelogLogEntry, updatedChangelogLogEntry);
        await File.WriteAllTextAsync(releaseState.ChangelogCodePath, updatedChangelogCode, Environment.UTF8_NO_BOM);
        Console.WriteLine("- Updated the existing in-app changelog entry.");

        var updatedNextChangelogHeader = $"# v{releaseState.NextChangelogVersion}, build {buildNumber + 1} ({GetPlaceholderBuildTime(releaseState.NextChangelogHeader)})";
        var updatedNextChangelog = ReplaceExactlyOnce(releaseState.NextChangelogContent, releaseState.NextChangelogHeader, updatedNextChangelogHeader);
        await File.WriteAllTextAsync(releaseState.NextChangelogPath, updatedNextChangelog, Environment.UTF8_NO_BOM);
        Console.WriteLine($"- Reserved build {buildNumber + 1} for '{Path.GetFileName(releaseState.NextChangelogPath)}'.");

        await WriteMetainfoRelease(releaseState.AppVersion, buildTime);
    }

    private static string GetMetainfoPath() => Path.Combine(Environment.GetRustRuntimeDirectory(), "packaging", "linux", "org.mindworkai.AIStudio.metainfo.xml");

    private static string GetChangelogPath(string appVersion) => Path.Combine(Environment.GetAIStudioDirectory(), "wwwroot", "changelog", $"v{appVersion}.md");

    /// <summary>
    /// Writes the AppStream release entry for the given version, using the changelog of that version as its description.
    /// </summary>
    /// <remarks>
    /// The entry always becomes the first release, and any earlier entry of the same version is replaced. This is what
    /// the Flatpak pipeline validates through 'update-metainfo.py --check' before it syncs a release. The release date
    /// is derived from the build time, because the pipeline reads it from the second line of the metadata file.
    /// </remarks>
    private static async Task WriteMetainfoRelease(string appVersion, DateTime releaseTime)
    {
        const string RELEASE_INDENT = "    ";

        var metainfoPath = GetMetainfoPath();
        if (!File.Exists(metainfoPath))
            throw new InvalidOperationException("The AppStream metainfo file does not exist.");

        var metainfo = await File.ReadAllTextAsync(metainfoPath, Encoding.UTF8);
        if (!ReleasesStartRegex().IsMatch(metainfo))
            throw new InvalidOperationException("The AppStream metainfo does not contain a <releases> element.");

        var changelogEntries = await ReadChangelogEntries(appVersion);

        // Drop any earlier entry of this version, so that the version stays unique and moves to the top.
        // We remove from the back, so that the index of the remaining matches stays valid:
        foreach (var previousRelease in ReleaseBlockRegex().Matches(metainfo).Cast<Match>().Where(match => ReleaseTagHasVersion(match.Value, appVersion)).Reverse())
            metainfo = metainfo.Remove(previousRelease.Index, previousRelease.Length);

        var lineEnding = metainfo.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var releaseDate = releaseTime.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var releaseBlock = new StringBuilder();
        releaseBlock.Append($"{RELEASE_INDENT}<release type=\"stable\" version=\"{appVersion}\" date=\"{releaseDate}\">{lineEnding}");
        releaseBlock.Append($"{RELEASE_INDENT}  <description>{lineEnding}");
        releaseBlock.Append($"{RELEASE_INDENT}    <ul>{lineEnding}");

        foreach (var changelogEntry in changelogEntries)
            releaseBlock.Append($"{RELEASE_INDENT}      <li>{changelogEntry}</li>{lineEnding}");

        releaseBlock.Append($"{RELEASE_INDENT}    </ul>{lineEnding}");
        releaseBlock.Append($"{RELEASE_INDENT}  </description>{lineEnding}");
        releaseBlock.Append($"{RELEASE_INDENT}</release>{lineEnding}");

        var releasesStart = ReleasesStartRegex().Match(metainfo);
        var insertionPoint = releasesStart.Index + releasesStart.Length;
        if (metainfo.AsSpan(insertionPoint).StartsWith(lineEnding))
            insertionPoint += lineEnding.Length;
        else
            releaseBlock.Insert(0, lineEnding);

        metainfo = metainfo.Insert(insertionPoint, releaseBlock.ToString());
        await File.WriteAllTextAsync(metainfoPath, metainfo, Environment.UTF8_NO_BOM);
        Console.WriteLine($"- Updated the AppStream metainfo for v{appVersion}, released on {releaseDate}, with {changelogEntries.Count} changelog entries.");
    }

    private static async Task<IReadOnlyList<string>> ReadChangelogEntries(string appVersion)
    {
        var changelogPath = GetChangelogPath(appVersion);
        if (!File.Exists(changelogPath))
            throw new InvalidOperationException($"The changelog file '{Path.GetFileName(changelogPath)}' does not exist.");

        // The first line is the changelog header, every other non-empty line must be a changelog entry:
        var changelogLines = SplitLines(await File.ReadAllTextAsync(changelogPath, Encoding.UTF8));
        var changelogEntries = new List<string>();
        foreach (var changelogLine in changelogLines.Skip(1))
        {
            var changelogEntry = changelogLine.Trim();
            if (changelogEntry.Length is 0)
                continue;

            if (!changelogEntry.StartsWith("- ", StringComparison.Ordinal))
                throw new InvalidOperationException($"The changelog '{Path.GetFileName(changelogPath)}' contains a line which is no changelog entry: '{changelogEntry}'.");

            changelogEntries.Add(ConvertChangelogEntryToAppStream(changelogEntry[2..].Trim()));
        }

        if (changelogEntries.Count is 0)
            throw new InvalidOperationException($"The changelog '{Path.GetFileName(changelogPath)}' does not contain any entry.");

        return changelogEntries;
    }

    private static string ConvertChangelogEntryToAppStream(string changelogEntry)
    {
        var escapedEntry = changelogEntry
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

        // Markdown code spans become AppStream code elements. Every second segment is inside a code span,
        // which requires an even number of markers and therefore an odd number of segments:
        var codeSpans = escapedEntry.Split('`');
        if (codeSpans.Length % 2 is 0)
            throw new InvalidOperationException($"The changelog entry contains an unbalanced code marker: '{changelogEntry}'.");

        var convertedEntry = new StringBuilder();
        for (var index = 0; index < codeSpans.Length; index++)
            convertedEntry.Append(index % 2 is 0 ? codeSpans[index] : $"<code>{codeSpans[index]}</code>");

        return convertedEntry.ToString();
    }

    private static DateTime ParseMetadataBuildTime(string buildTime)
    {
        if (!DateTime.TryParseExact(buildTime.Trim(), "yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedBuildTime))
            throw new InvalidOperationException($"The metadata build time '{buildTime}' is not a valid UTC build time.");

        return parsedBuildTime;
    }

    private static string FormatChangelogHeader(string appVersion, int buildNumber, DateTime buildTime)
    {
        return $"# v{appVersion}, build {buildNumber} ({buildTime.ToUniversalTime():yyyy-MM-dd HH:mm} UTC)";
    }

    private static string FormatChangelogLogEntry(string appVersion, int buildNumber, DateTime buildTime, string changelogFilename)
    {
        return $"new ({buildNumber}, \"v{appVersion}, build {buildNumber} ({buildTime.ToUniversalTime():yyyy-MM-dd HH:mm} UTC)\", \"{changelogFilename}\"),";
    }

    private static string GetFirstLine(string content)
    {
        var lineEnd = content.IndexOf('\n');
        return (lineEnd < 0 ? content : content[..lineEnd]).TrimEnd('\r');
    }

    private static string GetPlaceholderBuildTime(string changelogHeader)
    {
        var start = changelogHeader.LastIndexOf('(') + 1;
        return changelogHeader[start..^1];
    }

    private static bool ReleaseTagHasVersion(string releaseTag, string appVersion)
    {
        return Regex.IsMatch(releaseTag, $"\\bversion=\"{Regex.Escape(appVersion)}\"");
    }

    private static int CountOccurrences(string content, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ReplaceExactlyOnce(string content, string oldValue, string newValue)
    {
        if (CountOccurrences(content, oldValue) != 1)
            throw new InvalidOperationException("A previously validated release value is no longer unique.");

        return content.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static string[] SplitLines(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    private static string JoinLines(string originalContent, string[] lines)
    {
        var lineEnding = originalContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return string.Join(lineEnding, lines);
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
    
    [Command("update-project-hash", Description = "Update the project commit hash")]
    public async Task UpdateProjectCommitHash()
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

    private async Task<AppVersion> UpdateAppVersion(PrepareAction action, string? version = null)
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

        int newMajor, newMinor, newPatch;
        if (action == PrepareAction.SET && !string.IsNullOrWhiteSpace(version))
        {
            // Parse the provided version string:
            var versionMatch = AppVersionRegex().Match(version);
            if (!versionMatch.Success)
            {
                Console.WriteLine($"- Error: Invalid version format '{version}'. Expected format: major.minor.patch (e.g., 26.1.2)");
                return new(string.Empty, 0, 0, 0);
            }

            newMajor = int.Parse(versionMatch.Groups["major"].Value);
            newMinor = int.Parse(versionMatch.Groups["minor"].Value);
            newPatch = int.Parse(versionMatch.Groups["patch"].Value);
        }
        else
        {
            // Parse current version and increment based on action:
            var currentAppVersion = AppVersionRegex().Match(currentAppVersionLine);
            newPatch = int.Parse(currentAppVersion.Groups["patch"].Value);
            newMinor = int.Parse(currentAppVersion.Groups["minor"].Value);
            newMajor = int.Parse(currentAppVersion.Groups["major"].Value);

            switch (action)
            {
                case PrepareAction.BUILD:
                    newPatch++;
                    break;

                case PrepareAction.MONTH:
                    newPatch = 1;
                    newMinor++;
                    break;

                case PrepareAction.YEAR:
                    newPatch = 1;
                    newMinor = 1;
                    newMajor++;
                    break;
            }
        }

        var updatedAppVersion = $"{newMajor}.{newMinor}.{newPatch}";
        Console.WriteLine($"- Updating app version from '{currentAppVersionLine}' to '{updatedAppVersion}'.");

        lines[APP_VERSION_INDEX] = updatedAppVersion;
        await File.WriteAllLinesAsync(pathMetadata, lines, Environment.UTF8_NO_BOM);

        return new(updatedAppVersion, newMajor, newMinor, newPatch);
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

    private async Task UpdateVectorStoreVersion()
    {
        const int VECTOR_STORE_VERSION_INDEX = 11;

        var pathMetadata = Environment.GetMetadataPath();
        var lines = await File.ReadAllLinesAsync(pathMetadata, Encoding.UTF8);
        var currentVectorStoreVersion = lines[VECTOR_STORE_VERSION_INDEX].Trim();

        var matches = await this.DetermineVersion("Qdrant Edge", Environment.GetRustRuntimeDirectory(), QdrantEdgeVersionRegex(), "cargo", "tree --depth 1");
        if (matches.Count == 0)
            return;

        var updatedVectorStoreVersion = matches[0].Groups["version"].Value;
        if(currentVectorStoreVersion == updatedVectorStoreVersion)
        {
            Console.WriteLine("- The vector store version is already up to date.");
            return;
        }

        Console.WriteLine($"- Updated vector store version from {currentVectorStoreVersion} to {updatedVectorStoreVersion}.");
        lines[VECTOR_STORE_VERSION_INDEX] = updatedVectorStoreVersion;

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

    private sealed record RebuildReleaseState(
        string MetadataPath,
        string MetadataContent,
        string[] MetadataLines,
        string AppVersion,
        int BuildNumber,
        string ChangelogPath,
        string ChangelogContent,
        string ChangelogHeader,
        string ChangelogCodePath,
        string ChangelogCode,
        string ChangelogLogEntry,
        string NextChangelogPath,
        string NextChangelogContent,
        string NextChangelogHeader,
        string NextChangelogVersion,
        string HeadCommitHash);

    [GeneratedRegex("""(?ms).?(NET\s+SDK|SDK\s+\.NET)\s*:\s+Version:\s+(?<sdkVersion>[0-9.]+).+Commit:\s+(?<sdkCommit>[a-zA-Z0-9]+).+Host:\s+Version:\s+(?<hostVersion>[0-9.]+).+Commit:\s+(?<hostCommit>[a-zA-Z0-9]+)""")]
    private static partial Regex DotnetVersionRegex();
    
    [GeneratedRegex("""rustc (?<version>[0-9.]+)(?:-nightly)? \((?<commit>[a-zA-Z0-9]+)""")]
    private static partial Regex RustVersionRegex();
    
    [GeneratedRegex("""MudBlazor\s+(?<version>[0-9.]+)""")]
    private static partial Regex MudBlazorVersionRegex();
    
    [GeneratedRegex("""qdrant-edge\s+v(?<version>[0-9.]+)""")]
    private static partial Regex QdrantEdgeVersionRegex();

    [GeneratedRegex("""tauri\s+v(?<version>[0-9.]+)""")]
    private static partial Regex TauriVersionRegex();
    
    [GeneratedRegex("""^\s*Copyright\s+(?<year>[0-9]{4})""")]
    private static partial Regex FindCopyrightRegex();

    [GeneratedRegex("([0-9]{4})")]
    private static partial Regex ReplaceCopyrightYearRegex();
    
    [GeneratedRegex("""(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)""")]
    private static partial Regex AppVersionRegex();

    [GeneratedRegex("""^[0-9]+\.[0-9]+\.[0-9]+$""")]
    private static partial Regex ExactAppVersionRegex();

    [GeneratedRegex("""<releases\b[^>]*>""")]
    private static partial Regex ReleasesStartRegex();

    // Matches one entire release element, including its indentation and its trailing line break. The
    // self-closing form comes first, so that it is never mistaken for the start of a longer element:
    [GeneratedRegex("""(?ms)^[ \t]*<release\b[^>]*/>[ \t]*\r?\n?|^[ \t]*<release\b[^>]*>.*?</release>[ \t]*\r?\n?""")]
    private static partial Regex ReleaseBlockRegex();

    [GeneratedRegex("^[0-9a-fA-F]{40,64}$")]
    private static partial Regex GitCommitHashRegex();
}
