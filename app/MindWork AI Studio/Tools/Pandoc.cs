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
    private static readonly RID CPU_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();
    
    private const string DOWNLOAD_URL = "https://github.com/jgm/pandoc/releases/download";
    private const string LATEST_URL = "https://github.com/jgm/pandoc/releases/latest";
    
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("Pandoc");
    private static readonly Version MINIMUM_REQUIRED_VERSION = new (3, 7, 0, 2);
    private static readonly Version FALLBACK_VERSION = new (3, 7, 0, 2);

    /// <summary>
    /// Prepares a Pandoc process by using the Pandoc process builder.
    /// </summary>
    /// <returns>The Pandoc process builder with default settings.</returns>
    public static PandocProcessBuilder PreparePandocProcess() => PandocProcessBuilder.Create();

    /// <summary>
    /// Checks if pandoc is available on the system and can be started as a process or is present in AI Studio's data dir.
    /// </summary>
    /// <param name="rustService">Global rust service to access file system and data dir.</param>
    /// <param name="showMessages">Controls if snackbars are shown to the user.</param>
    /// <returns>True, if pandoc is available and the minimum required version is met, else false.</returns>
    public static async Task<PandocInstallation> CheckAvailabilityAsync(RustService rustService, bool showMessages = true)
    {
        try
        {
            var preparedProcess = await PreparePandocProcess().AddArgument("--version").BuildAsync(rustService);
            using var process = Process.Start(preparedProcess.StartInfo);
            if (process == null)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Help, TB("Was not able to check the Pandoc installation.")));
                
                LOG.LogInformation("The Pandoc process was not started, it was null");
                return new(false, TB("Was not able to check the Pandoc installation."), false, string.Empty, preparedProcess.IsLocal);
            }
                
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, TB("Pandoc is not available on the system or the process had issues.")));
                
                LOG.LogError("The Pandoc process was exited with code {ProcessExitCode}", process.ExitCode);
                return new(false, TB("Pandoc is not available on the system or the process had issues."), false, string.Empty, preparedProcess.IsLocal);
            }

            var versionMatch = PandocCmdRegex().Match(output);
            if (!versionMatch.Success)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Terminal, TB("Was not able to validate the Pandoc installation.")));
                
                LOG.LogError("Pandoc --version returned an invalid format: {Output}", output);
                return new(false, TB("Was not able to validate the Pandoc installation."), false, string.Empty, preparedProcess.IsLocal);
            }
            
            var versions = versionMatch.Groups[1].Value;
            var installedVersion = Version.Parse(versions);
            var installedVersionString = installedVersion.ToString();
            
            if (installedVersion >= MINIMUM_REQUIRED_VERSION)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, string.Format(TB("Pandoc v{0} is installed."), installedVersionString)));
                
                LOG.LogInformation("Pandoc v{0} is installed and matches the required version (v{1})", installedVersionString, MINIMUM_REQUIRED_VERSION.ToString());
                return new(true, string.Empty, true, installedVersionString, preparedProcess.IsLocal);
            }
            
            if (showMessages)
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Build, string.Format(TB("Pandoc v{0} is installed, but it doesn't match the required version (v{1})."), installedVersionString, MINIMUM_REQUIRED_VERSION.ToString())));
            
            LOG.LogWarning("Pandoc v{0} is installed, but it does not match the required version (v{1})", installedVersionString, MINIMUM_REQUIRED_VERSION.ToString());
            return new(true, string.Format(TB("Pandoc v{0} is installed, but it does not match the required version (v{1})."), installedVersionString, MINIMUM_REQUIRED_VERSION.ToString()), false, installedVersionString, preparedProcess.IsLocal);
        }
        catch (Exception e)
        {
            if (showMessages)
                await MessageBus.INSTANCE.SendError(new (@Icons.Material.Filled.AppsOutage, TB("It seems that Pandoc is not installed.")));
            
            LOG.LogError("Pandoc is not installed and threw an exception: {0}", e.Message);
            return new(false, TB("It seems that Pandoc is not installed."), false, string.Empty, false);
        }
    }

    /// <summary>
    /// Automatically decompresses the latest pandoc archive into AiStudio's data directory
    /// </summary>
    /// <param name="rustService">Global rust service to access file system and data dir</param>
    /// <returns>None</returns>
    public static async Task InstallAsync(RustService rustService)
    {
        var latestVersion = await FetchLatestVersionAsync();
        var installDir = await GetPandocDataFolder(rustService);
        ClearFolder(installDir);
        
        LOG.LogInformation("Trying to install Pandoc v{0} to '{1}'...", latestVersion, installDir);
        
        try
        {
            if (!Directory.Exists(installDir))
                Directory.CreateDirectory(installDir);
            
            // Create a temporary file to download the archive to:
            var pandocTempDownloadFile = Path.GetTempFileName();
            
            //
            // Download the latest Pandoc archive from GitHub:
            //
            var uri = await GenerateArchiveUriAsync();
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                {
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Error, TB("Pandoc was not installed successfully, because the archive was not found.")));
                    LOG.LogError("Pandoc was not installed successfully, because the archive was not found (status code {0}): url='{1}', message='{2}'", response.StatusCode, uri, response.RequestMessage);
                    return;
                }

                // Download the archive to the temporary file:
                await using var tempFileStream = File.Create(pandocTempDownloadFile);
                await response.Content.CopyToAsync(tempFileStream);
            }

            if (uri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(pandocTempDownloadFile, installDir);
            }
            else if (uri.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                await using var tgzStream = File.Open(pandocTempDownloadFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var uncompressedStream = new GZipStream(tgzStream, CompressionMode.Decompress);
                await TarFile.ExtractToDirectoryAsync(uncompressedStream, installDir, true);
            }
            else
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, TB("Pandoc was not installed successfully, because the archive type is unknown.")));
                LOG.LogError("Pandoc was not installed, the archive is unknown: url='{0}'", uri);
                return;
            }

            File.Delete(pandocTempDownloadFile);
            
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, string.Format(TB("Pandoc v{0} was installed successfully."), latestVersion)));
            LOG.LogInformation("Pandoc v{0} was installed successfully.", latestVersion);
        }
        catch (Exception ex)
        {
            LOG.LogError(ex, "An error occurred while installing Pandoc.");
        }
    }
    
    private static void ClearFolder(string path)
    {
        if (!Directory.Exists(path))
            return;
        
        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            LOG.LogError(ex, "Error clearing pandoc installation directory.");
        }
    }
    
    /// <summary>
    /// Asynchronously fetch the content from Pandoc's latest release page and extract the latest version number
    /// </summary>
    /// <remarks>Version numbers can have the following formats: x.x, x.x.x or x.x.x.x</remarks>
    /// <returns>Latest Pandoc version number</returns>
    public static async Task<string> FetchLatestVersionAsync() {
        using var client = new HttpClient();
        var response = await client.GetAsync(LATEST_URL);

        if (!response.IsSuccessStatusCode)
        {
            LOG.LogError("Code {StatusCode}: Could not fetch Pandoc's latest page: {Response}", response.StatusCode, response.RequestMessage);
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, string.Format(TB("The latest Pandoc version was not found, installing version {0} instead."), FALLBACK_VERSION.ToString())));
            return FALLBACK_VERSION.ToString();
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var versionMatch = LatestVersionRegex().Match(htmlContent);
        if (!versionMatch.Success)
        {
            LOG.LogError("The latest version regex returned nothing: {0}", versionMatch.Groups.ToString());
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, string.Format(TB("The latest Pandoc version was not found, installing version {0} instead."), FALLBACK_VERSION.ToString())));
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