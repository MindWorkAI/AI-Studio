using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

using AIStudio.Tools.Metadata;
using AIStudio.Tools.Services;

using SharedTools;

namespace AIStudio.Tools;

public static partial class Pandoc
{
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataArchitectureAttribute META_DATA_ARCH = ASSEMBLY.GetCustomAttribute<MetaDataArchitectureAttribute>()!;
    private static readonly RID CPU_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();
    
    private const string DOWNLOAD_URL = "https://github.com/jgm/pandoc/releases/download";
    private const string LATEST_URL = "https://github.com/jgm/pandoc/releases/latest";
    
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("Pandoc");
    private static readonly Version MINIMUM_REQUIRED_VERSION = new (3, 7);
    private static readonly Version FALLBACK_VERSION = new (3, 7, 0, 2);

    /// <summary>
    /// Prepares a ProcessStartInfo for running pandoc with the given parameters.
    /// </summary>
    /// <remarks>
    /// Any local installation of pandoc will be preferred over the system-wide installation.
    /// </remarks>
    /// <param name="rustService">The global rust service to access file system and data dir.</param>
    /// <param name="inputFile">The input file to convert.</param>
    /// <param name="outputFile">The output file to write the converted content to.</param>
    /// <param name="inputFormat">The format of the input file (e.g., markdown, html, etc.).</param>
    /// <param name="outputFormat">The format of the output file (e.g., pdf, docx, etc.).</param>
    /// <param name="additionalArgs">Additional arguments to pass to the pandoc command (optional).</param>
    /// <returns>The ProcessStartInfo object configured to run pandoc with the specified parameters.</returns>
    public static async Task<ProcessStartInfo> PreparePandocProcess(RustService rustService, string inputFile, string outputFile, string inputFormat, string outputFormat, string? additionalArgs = null) => new()
    {
        FileName = await PandocExecutablePath(rustService),
        Arguments = $"{inputFile} -f {inputFormat} -t {outputFormat} {additionalArgs ?? string.Empty} -o {outputFile}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    /// <summary>
    /// Checks if pandoc is available on the system and can be started as a process or is present in AI Studio's data dir.
    /// </summary>
    /// <param name="rustService">Global rust service to access file system and data dir.</param>
    /// <param name="showMessages">Controls if snackbars are shown to the user.</param>
    /// <returns>True, if pandoc is available and the minimum required version is met, else false.</returns>
    public static async Task<bool> CheckAvailabilityAsync(RustService rustService, bool showMessages = true)
    {
        try
        {
            var startInfo = await PreparePandocProcess(rustService, string.Empty, string.Empty, string.Empty, string.Empty);
            startInfo.Arguments = "--version";
            
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Help, "The pandoc process could not be started."));
                
                LOG.LogInformation("The pandoc process was not started, it was null");
                return false;
            }
                
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, "The pandoc process exited unexpectedly."));
                
                LOG.LogError("The pandoc process was exited with code {ProcessExitCode}", process.ExitCode);
                return false;
            }

            var versionMatch = PandocCmdRegex().Match(output);
            if (!versionMatch.Success)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Terminal, "pandoc --version returned an invalid format."));
                
                LOG.LogError("pandoc --version returned an invalid format:\n {Output}", output);
                return false;
            }
            var versions = versionMatch.Groups[1].Value;
            var installedVersion = Version.Parse(versions);
            
            if (installedVersion >= MINIMUM_REQUIRED_VERSION)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, $"Pandoc {installedVersion.ToString()} is installed."));
                
                return true;
            }
            
            if (showMessages)
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Build, $"Pandoc {installedVersion.ToString()} is installed, but it doesn't match the required version ({MINIMUM_REQUIRED_VERSION.ToString()})."));
            
            LOG.LogInformation("Pandoc {Installed} is installed, but it does not match the required version ({Requirement})", installedVersion.ToString(), MINIMUM_REQUIRED_VERSION.ToString());
            return false;

        }
        catch (Exception e)
        {
            if (showMessages)
                await MessageBus.INSTANCE.SendError(new (@Icons.Material.Filled.AppsOutage, "Pandoc is not installed."));
            
            LOG.LogError("Pandoc is not installed and threw an exception: {Message}", e.Message);
            return false;
        }
    }

    /// <summary>
    /// Automatically decompresses the latest pandoc archive into AiStudio's data directory
    /// </summary>
    /// <param name="rustService">Global rust service to access file system and data dir</param>
    /// <returns>None</returns>
    public static async Task InstallAsync(RustService rustService)
    {
        var installDir = await GetPandocDataFolder(rustService);
        ClearFolder(installDir);
        
        try
        {
            if (!Directory.Exists(installDir))
                Directory.CreateDirectory(installDir);

            using var client = new HttpClient();
            var uri = await GenerateArchiveUriAsync();
            
            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, "Pandoc was not installed successfully, because the archive was not found."));
                LOG.LogError("Pandoc was not installed, the release archive was not found (status code {StatusCode}): url='{Uri}', message='{Message}'", response.StatusCode, uri, response.RequestMessage);
                return;
            }
            
            var fileBytes = await response.Content.ReadAsByteArrayAsync();

            if (uri.Contains(".zip"))
            {
                var tempZipPath = Path.Join(Path.GetTempPath(), "pandoc.zip");
                await File.WriteAllBytesAsync(tempZipPath, fileBytes);
                ZipFile.ExtractToDirectory(tempZipPath, installDir);
                File.Delete(tempZipPath);
            }
            else if (uri.Contains(".tar.gz"))
            {
                var tempTarPath = Path.Join(Path.GetTempPath(), "pandoc.tar.gz");
                await File.WriteAllBytesAsync(tempTarPath, fileBytes);
                ZipFile.ExtractToDirectory(tempTarPath, installDir);
                File.Delete(tempTarPath);
            }
            else
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, "Pandoc was not installed successfully, because the archive type is unknown."));
                LOG.LogError("Pandoc was not installed, the archive is unknown: url='{Uri}'", uri);
                return;
            }

            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, $"Pandoc {await FetchLatestVersionAsync()} was installed successfully."));
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
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, $"The latest pandoc version was not found, installing version {FALLBACK_VERSION.ToString()} instead."));
            return FALLBACK_VERSION.ToString();
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var versionMatch = LatestVersionRegex().Match(htmlContent);
        if (!versionMatch.Success)
        {
            LOG.LogError("The latest version regex returned nothing: {Value}", versionMatch.Groups.ToString());
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, $"The latest pandoc version was not found, installing version {FALLBACK_VERSION.ToString()} instead."));
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
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Terminal, $"Installers are not available on {CPU_ARCHITECTURE} systems."));
                return string.Empty;
        }
    }

    /// <summary>
    /// Reads the os platform to determine the used executable name.
    /// </summary>
    private static string PandocExecutableName => CPU_ARCHITECTURE is RID.WIN_ARM64 or RID.WIN_X64 ? "pandoc.exe" : "pandoc";
    
    /// <summary>
    /// Returns the path to the pandoc executable.
    /// </summary>
    /// <remarks>
    /// Any local installation of pandoc will be preferred over the system-wide installation.
    /// When a local installation is found, its absolute path will be returned. In case no local
    /// installation is found, the name of the pandoc executable will be returned.
    /// </remarks>
    /// <param name="rustService">Global rust service to access file system and data dir.</param>
    /// <returns>Path to the pandoc executable.</returns>
    private static async Task<string> PandocExecutablePath(RustService rustService)
    {
        //
        // First, we try to find the pandoc executable in the data directory.
        // Any local installation should be preferred over the system-wide installation.
        //
        var localInstallationRootDirectory = await GetPandocDataFolder(rustService);
        try
        {
            var executableName = PandocExecutableName;
            var subdirectories = Directory.GetDirectories(localInstallationRootDirectory);
            foreach (var subdirectory in subdirectories)
            {
                var pandocPath = Path.Combine(subdirectory, executableName);
                if (File.Exists(pandocPath))
                    return pandocPath;
            }
        }
        catch
        {
            // ignored
        }
        
        //
        // When no local installation was found, we assume that the pandoc executable is in the system PATH.
        //
        return PandocExecutableName;
    }

    private static async Task<string> GetPandocDataFolder(RustService rustService) => Path.Join(await rustService.GetDataDirectory(), "pandoc");
    
    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?(?:\.[0-9]+)?)")]
    private static partial Regex PandocCmdRegex();
    
    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?(?:\.[0-9]+)?)")]
    private static partial Regex LatestVersionRegex();
}