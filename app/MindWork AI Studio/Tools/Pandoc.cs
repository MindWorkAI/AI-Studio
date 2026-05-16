using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

using AIStudio.Tools.Metadata;
using AIStudio.Tools.Services;

using SharedTools;

namespace AIStudio.Tools;

public static partial class Pandoc
{
    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(Pandoc).Namespace, nameof(Pandoc));

    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataArchitectureAttribute META_DATA_ARCH = ASSEMBLY.GetCustomAttribute<MetaDataArchitectureAttribute>()!;

    // Use runtime detection instead of metadata to ensure correct RID on dev machines:
    private static readonly RID CPU_ARCHITECTURE = RIDExtensions.GetCurrentRID();
    private static readonly RID METADATA_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();

    private const string DOWNLOAD_URL = "https://github.com/jgm/pandoc/releases/download";
    private const string LATEST_URL = "https://github.com/jgm/pandoc/releases/latest";

    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(Pandoc));
    private static readonly Version MINIMUM_REQUIRED_VERSION = new (3, 7, 0, 2);
    private static readonly Version FALLBACK_VERSION = new (3, 7, 0, 2);

    /// <summary>
    /// Tracks whether the first availability check log has been written to avoid log spam on repeated calls.
    /// </summary>
    private static bool HAS_LOGGED_AVAILABILITY_CHECK_ONCE;

    private static readonly HttpClient WEB_CLIENT = new();
    private static readonly SemaphoreSlim INSTALLATION_LOCK = new(1, 1);

    /// <summary>
    /// Prepares a Pandoc process by using the Pandoc process builder.
    /// </summary>
    /// <returns>The Pandoc process builder with default settings.</returns>
    private static PandocProcessBuilder PreparePandocProcess() => PandocProcessBuilder.Create();

    /// <summary>
    /// Checks if pandoc is available on the system and can be started as a process or is present in AI Studio's data dir.
    /// </summary>
    /// <param name="rustService">Global rust service to access file system and data dir.</param>
    /// <param name="showMessages">Controls if snackbars are shown to the user.</param>
    /// <param name="showSuccessMessage">Controls if a success snackbar is shown to the user.</param>
    /// <returns>True, if pandoc is available and the minimum required version is met, else false.</returns>
    public static async Task<PandocInstallation> CheckAvailabilityAsync(RustService rustService, bool showMessages = true, bool showSuccessMessage = true)
    {
        //
        // Determine if we should log (only on the first call):
        //
        var shouldLog = !HAS_LOGGED_AVAILABILITY_CHECK_ONCE;

        try
        {
            //
            // Log a warning if the runtime-detected RID differs from the metadata RID.
            // This can happen on dev machines where the metadata.txt contains stale values.
            // We always use the runtime-detected RID for correct behavior.
            //
            if (shouldLog && CPU_ARCHITECTURE != METADATA_ARCHITECTURE)
            {
                LOG.LogWarning(
                    "Runtime-detected RID '{RuntimeRID}' differs from metadata RID '{MetadataRID}'. Using runtime-detected RID. This is expected on dev machines where metadata.txt may be outdated.",
                    CPU_ARCHITECTURE.ToUserFriendlyName(),
                    METADATA_ARCHITECTURE.ToUserFriendlyName());
            }

            var preparedProcess = await PreparePandocProcess().AddArgument("--version").BuildAsync(rustService);
            if (shouldLog)
                LOG.LogInformation("Checking Pandoc availability using executable: '{Executable}' (IsLocal: {IsLocal}).", preparedProcess.StartInfo.FileName, preparedProcess.IsLocal);

            using var process = Process.Start(preparedProcess.StartInfo);
            if (process == null)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Help, TB("Was not able to check the Pandoc installation.")));

                if (shouldLog)
                    LOG.LogError("The Pandoc process was not started, it was null. Executable path: '{Executable}'.", preparedProcess.StartInfo.FileName);
                
                return new(false, TB("Was not able to check the Pandoc installation."), false, string.Empty, preparedProcess.IsLocal);
            }

            // Read output streams asynchronously while the process runs (prevents deadlock):
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait for the process to exit AND for streams to be fully read:
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, TB("Pandoc is not available on the system or the process had issues.")));

                if (shouldLog)
                    LOG.LogError("The Pandoc process exited with code {ProcessExitCode}. Error output: '{ErrorText}'", process.ExitCode, error);
                
                return new(false, TB("Pandoc is not available on the system or the process had issues."), false, string.Empty, preparedProcess.IsLocal);
            }

            var versionMatch = PandocCmdRegex().Match(output);
            if (!versionMatch.Success)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Terminal, TB("Was not able to validate the Pandoc installation.")));

                if (shouldLog)
                    LOG.LogError("Pandoc --version returned an invalid format: '{Output}'.", output);
                
                return new(false, TB("Was not able to validate the Pandoc installation."), false, string.Empty, preparedProcess.IsLocal);
            }

            var versions = versionMatch.Groups[1].Value;
            var installedVersion = Version.Parse(versions);
            var installedVersionString = installedVersion.ToString();

            if (installedVersion >= MINIMUM_REQUIRED_VERSION)
            {
                if (showMessages && showSuccessMessage)
                    await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, string.Format(TB("Pandoc v{0} is installed."), installedVersionString)));

                if (shouldLog)
                    LOG.LogInformation("Pandoc v{0} is installed and matches the required version (v{1}).", installedVersionString, MINIMUM_REQUIRED_VERSION.ToString());

                return new(true, string.Empty, true, installedVersionString, preparedProcess.IsLocal);
            }

            if (showMessages)
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Build, string.Format(TB("Pandoc v{0} is installed, but it doesn't match the required version (v{1})."), installedVersionString, MINIMUM_REQUIRED_VERSION.ToString())));

            if (shouldLog)
                LOG.LogWarning("Pandoc v{0} is installed, but it does not match the required version (v{1}).", installedVersionString, MINIMUM_REQUIRED_VERSION.ToString());
            
            return new(true, string.Format(TB("Pandoc v{0} is installed, but it does not match the required version (v{1})."), installedVersionString, MINIMUM_REQUIRED_VERSION.ToString()), false, installedVersionString, preparedProcess.IsLocal);
        }
        catch (Exception e)
        {
            if (showMessages)
                await MessageBus.INSTANCE.SendError(new(@Icons.Material.Filled.AppsOutage, TB("Pandoc doesn't seem to be installed.")));

            if(shouldLog)
                LOG.LogError(e, "Pandoc availability check failed. This usually means Pandoc is not installed or not in the system PATH.");
            
            return new(false, TB("Pandoc doesn't seem to be installed."), false, string.Empty, false);
        }
        finally
        {
            HAS_LOGGED_AVAILABILITY_CHECK_ONCE = true;
        }
    }

    /// <summary>
    /// Automatically decompresses the latest pandoc archive into AiStudio's data directory
    /// </summary>
    /// <param name="rustService">Global rust service to access file system and data dir</param>
    /// <returns>None</returns>
    public static async Task InstallAsync(RustService rustService)
    {
        await INSTALLATION_LOCK.WaitAsync();

        var latestVersion = await FetchLatestVersionAsync();
        var installDir = await GetPandocDataFolder(rustService);
        var installParentDir = Path.GetDirectoryName(installDir) ?? Path.GetTempPath();
        var stagingDir = Path.Combine(installParentDir, $"pandoc-install-{Guid.NewGuid():N}");
        var pandocTempDownloadFile = Path.GetTempFileName();
        
        LOG.LogInformation("Trying to install Pandoc v{0} to '{1}'...", latestVersion, installDir);
        
        try
        {
            if (!Directory.Exists(installParentDir))
                Directory.CreateDirectory(installParentDir);
            
            //
            // Download the latest Pandoc archive from GitHub:
            //
            var uri = GenerateArchiveUri(latestVersion);
            if (string.IsNullOrWhiteSpace(uri))
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, TB("AI Studio couldn't install Pandoc because the archive type is unknown.")));
                LOG.LogError("Pandoc was not installed, no archive is available for architecture '{Architecture}'.", CPU_ARCHITECTURE.ToUserFriendlyName());
                return;
            }

            using var response = await WEB_CLIENT.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, TB("AI Studio couldn't install Pandoc because the archive was not found.")));
                LOG.LogError("Pandoc was not installed successfully, because the archive was not found (status code {0}): url='{1}', message='{2}'", response.StatusCode, uri, response.RequestMessage);
                return;
            }

            // Download the archive to the temporary file:
            await using (var tempFileStream = File.Create(pandocTempDownloadFile))
            {
                await response.Content.CopyToAsync(tempFileStream);
                await tempFileStream.FlushAsync();
            }

            Directory.CreateDirectory(stagingDir);
            if (uri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await RunWithRetriesAsync(
                    () =>
                    {
                        ZipFile.ExtractToDirectory(pandocTempDownloadFile, stagingDir, true);
                        return Task.CompletedTask;
                    },
                    "extracting the Pandoc ZIP archive");
            }
            else if (uri.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                await RunWithRetriesAsync(
                    async () =>
                    {
                        await using var tgzStream = File.Open(pandocTempDownloadFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await using var uncompressedStream = new GZipStream(tgzStream, CompressionMode.Decompress);
                        await TarFile.ExtractToDirectoryAsync(uncompressedStream, stagingDir, true);
                    },
                    "extracting the Pandoc TAR archive");
            }
            else
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, TB("AI Studio couldn't install Pandoc because the archive type is unknown.")));
                LOG.LogError("Pandoc was not installed, the archive is unknown: url='{0}'", uri);
                return;
            }

            var stagedPandocExecutable = FindExecutableInDirectory(stagingDir, PandocProcessBuilder.PandocExecutableName);
            if (string.IsNullOrWhiteSpace(stagedPandocExecutable))
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, TB("AI Studio couldn't install Pandoc because the executable was not found in the archive.")));
                LOG.LogError("Pandoc was not installed, the executable was not found in the extracted archive: '{StagingDir}'.", stagingDir);
                return;
            }

            LOG.LogInformation("Found Pandoc executable in downloaded archive: '{Executable}'.", stagedPandocExecutable);

            await ReplaceInstallationDirectoryAsync(stagingDir, installDir);
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, string.Format(TB("Pandoc v{0} was installed successfully."), latestVersion)));
            LOG.LogInformation("Pandoc v{0} was installed successfully.", latestVersion);
        }
        catch (Exception ex)
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, TB("AI Studio couldn't install Pandoc.")));
            LOG.LogError(ex, "An error occurred while installing Pandoc.");
        }
        finally
        {
            TryDeleteFile(pandocTempDownloadFile);

            if (Directory.Exists(stagingDir))
                await TryDeleteFolderAsync(stagingDir);

            INSTALLATION_LOCK.Release();
        }
    }
    
    private static async Task ReplaceInstallationDirectoryAsync(string stagingDir, string installDir)
    {
        var backupDir = $"{installDir}.backup-{Guid.NewGuid():N}";
        var hasBackup = false;
        var stagingWasMoved = false;

        try
        {
            if (Directory.Exists(installDir))
            {
                await MoveDirectoryWithRetriesAsync(installDir, backupDir, "moving the previous Pandoc installation to backup");
                hasBackup = true;
            }

            await MoveDirectoryWithRetriesAsync(stagingDir, installDir, "moving the new Pandoc installation into place");
            stagingWasMoved = true;
        }
        catch (Exception ex)
        {
            if (hasBackup && !stagingWasMoved && !Directory.Exists(installDir) && Directory.Exists(backupDir))
            {
                try
                {
                    await MoveDirectoryWithRetriesAsync(backupDir, installDir, "restoring the previous Pandoc installation");
                    hasBackup = false;
                }
                catch (Exception rollbackEx)
                {
                    LOG.LogError(rollbackEx, "Error restoring previous Pandoc installation directory. Keeping backup directory at: '{BackupDir}'.", backupDir);
                }
            }

            LOG.LogError(ex, "Error replacing pandoc installation directory.");
            throw;
        }
        finally
        {
            if (hasBackup && stagingWasMoved && Directory.Exists(backupDir))
                await TryDeleteFolderAsync(backupDir);
        }
    }

    private static string FindExecutableInDirectory(string rootDirectory, string executableName)
    {
        if (!Directory.Exists(rootDirectory))
            return string.Empty;

        var rootExecutablePath = Path.Combine(rootDirectory, executableName);
        if (File.Exists(rootExecutablePath))
            return rootExecutablePath;

        foreach (var subdirectory in Directory.GetDirectories(rootDirectory, "*", SearchOption.AllDirectories))
        {
            var pandocPath = Path.Combine(subdirectory, executableName);
            if (File.Exists(pandocPath))
                return pandocPath;
        }

        return string.Empty;
    }

    private static async Task MoveDirectoryWithRetriesAsync(string sourceDir, string destinationDir, string operationName)
    {
        await RunWithRetriesAsync(
            () =>
            {
                Directory.Move(sourceDir, destinationDir);
                return Task.CompletedTask;
            },
            operationName,
            maxAttempts: 8);
    }

    private static async Task RunWithRetriesAsync(Func<Task> operation, string operationName, int maxAttempts = 4)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is IOException or UnauthorizedAccessException)
            {
                LOG.LogWarning(ex, "Error while {OperationName}; retrying attempt {Attempt}/{MaxAttempts}.", operationName, attempt + 1, maxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            LOG.LogWarning(ex, "Was not able to delete temporary Pandoc archive: '{Path}'.", path);
        }
    }

    private static async Task TryDeleteFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        try
        {
            await RunWithRetriesAsync(
                () =>
                {
                    Directory.Delete(path, true);
                    return Task.CompletedTask;
                },
                $"deleting temporary Pandoc directory '{path}'",
                maxAttempts: 3);
        }
        catch (Exception ex)
        {
            LOG.LogWarning(ex, "Was not able to delete temporary Pandoc directory: '{Path}'.", path);
        }
    }
    
    /// <summary>
    /// Asynchronously fetch the content from Pandoc's latest release page and extract the latest version number
    /// </summary>
    /// <remarks>Version numbers can have the following formats: x.x, x.x.x or x.x.x.x</remarks>
    /// <returns>Latest Pandoc version number</returns>
    public static async Task<string> FetchLatestVersionAsync() {
        var response = await WEB_CLIENT.GetAsync(LATEST_URL);
        if (!response.IsSuccessStatusCode)
        {
            LOG.LogError("Code {StatusCode}: Could not fetch Pandoc's latest page: {Response}", response.StatusCode, response.RequestMessage);
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, string.Format(TB("AI Studio couldn't find the latest Pandoc version and will install version {0} instead."), FALLBACK_VERSION.ToString())));
            return FALLBACK_VERSION.ToString();
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var versionMatch = LatestVersionRegex().Match(htmlContent);
        if (!versionMatch.Success)
        {
            LOG.LogError("The latest version regex returned nothing: {0}", versionMatch.Groups.ToString());
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, string.Format(TB("AI Studio couldn't find the latest Pandoc version and will install version {0} instead."), FALLBACK_VERSION.ToString())));
            return FALLBACK_VERSION.ToString();
        }
        
        var version = versionMatch.Groups[1].Value;
        return version;
    }

    /// <summary>
    /// Reads the systems architecture to find the correct archive.
    /// </summary>
    /// <returns>Full URI to the right archive in Pandoc's repository.</returns>
    public static async Task<string> GenerateArchiveUriAsync()
    {
        var version = await FetchLatestVersionAsync();
        return GenerateArchiveUri(version);
    }

    private static string GenerateArchiveUri(string version)
    {
        var baseUri = $"{DOWNLOAD_URL}/{version}/pandoc-{version}-";
        return CPU_ARCHITECTURE switch
        {
            //
            // Unfortunately, pandoc is not yet available for ARM64 Windows systems,
            // so we have to use the x86_64 version for now. ARM Windows contains
            // an x86_64 emulation layer, so it should work fine for now.
            //
            // Pandoc would be available for ARM64 Windows, but the Haskell compiler
            // does not support ARM64 Windows yet. Here are the related issues:
            //
            // - Haskell compiler: https://gitlab.haskell.org/ghc/ghc/-/issues/24603
            // - Haskell ARM MR: https://gitlab.haskell.org/ghc/ghc/-/merge_requests/13856
            // - Pandoc ARM64: https://github.com/jgm/pandoc/issues/10095
            //
            RID.WIN_X64 or RID.WIN_ARM64 => $"{baseUri}windows-x86_64.zip",
            
            RID.OSX_X64 => $"{baseUri}x86_64-macOS.zip",
            RID.OSX_ARM64 => $"{baseUri}arm64-macOS.zip",
            
            RID.LINUX_X64 => $"{baseUri}linux-amd64.tar.gz",
            RID.LINUX_ARM64 => $"{baseUri}linux-arm64.tar.gz",
            
            _ => string.Empty,
        };
    }
    
    /// <summary>
    /// Reads the systems architecture to find the correct Pandoc installer
    /// </summary>
    /// <returns>Full URI to the right installer in Pandoc's repo</returns>
    public static async Task<string> GenerateInstallerUriAsync()
    {
        var version = await FetchLatestVersionAsync();
        var baseUri = $"{DOWNLOAD_URL}/{version}/pandoc-{version}-";

        switch (CPU_ARCHITECTURE)
        {
            //
            // Unfortunately, pandoc is not yet available for ARM64 Windows systems,
            // so we have to use the x86_64 version for now. ARM Windows contains
            // an x86_64 emulation layer, so it should work fine for now.
            //
            // Pandoc would be available for ARM64 Windows, but the Haskell compiler
            // does not support ARM64 Windows yet. Here are the related issues:
            //
            // - Haskell compiler: https://gitlab.haskell.org/ghc/ghc/-/issues/24603
            // - Haskell ARM MR: https://gitlab.haskell.org/ghc/ghc/-/merge_requests/13856
            // - Pandoc ARM64: https://github.com/jgm/pandoc/issues/10095
            //
            case RID.WIN_X64 or RID.WIN_ARM64:
                return $"{baseUri}windows-x86_64.msi";
            
            case RID.OSX_X64:
                return $"{baseUri}x86_64-macOS.pkg";
            
            case RID.OSX_ARM64:
                return $"{baseUri}arm64-macOS.pkg";
            
            default:
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Terminal, string.Format(TB("Installers are not available on {0} systems."), CPU_ARCHITECTURE.ToUserFriendlyName())));
                return string.Empty;
        }
    }

    public static async Task<string> GetPandocDataFolder(RustService rustService) => Path.Join(await rustService.GetDataDirectory(), "pandoc");
    
    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?(?:\.[0-9]+)?)")]
    private static partial Regex PandocCmdRegex();
    
    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?(?:\.[0-9]+)?)")]
    private static partial Regex LatestVersionRegex();
}