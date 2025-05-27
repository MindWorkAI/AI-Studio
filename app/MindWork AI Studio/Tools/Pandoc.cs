using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AIStudio.Components;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

public static partial class Pandoc
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("PluginFactory");
    private static readonly string DOWNLOAD_URL = "https://github.com/jgm/pandoc/releases/download";
    private static readonly string LATEST_URL = "https://github.com/jgm/pandoc/releases/latest";
    private static readonly Version MINIMUM_REQUIRED_VERSION = new (3, 6);
    private static readonly Version FALLBACK_VERSION = new (3, 6, 4);
    private static readonly string CPU_ARCHITECTURE = "win-x64";
    
    /// <summary>
    /// Checks if pandoc is available on the system and can be started as a process
    /// </summary>
    /// <returns>True, if pandoc is available and the minimum required version is met, else False.</returns>
    public static async Task<bool> CheckAvailabilityAsync(RustService rustService, bool showMessages = true)
    {
        var installDir = await GetPandocDataFolder(rustService);
        var subdirectories = Directory.GetDirectories(installDir);

        if (subdirectories.Length > 1)
        {
            await InstallAsync(rustService);
            return true;
        }

        var hasPandoc = false;
        foreach (var subdirectory in subdirectories)
        {
            if (subdirectory.Contains("pandoc"))
                hasPandoc = true;
        }
        
        if (hasPandoc)
            return true;
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetPandocExecutableName(),
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
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
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, $"The pandoc process exited unexpectedly."));
                LOG.LogError("The pandoc process was exited with code {ProcessExitCode}", process.ExitCode);
                return false;
            }

            var versionMatch = PandocRegex().Match(output);
            if (!versionMatch.Success)
            {
                if (showMessages)
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Terminal, $"pandoc --version returned an invalid format."));
                LOG.LogError("pandoc --version returned an invalid format:\n {Output}", output);
                return false;
            }
            var versions = versionMatch.Groups[1].Value.Split('.');
            var major = int.Parse(versions[0]);
            var minor = int.Parse(versions[1]);
            var installedVersion = new Version(major, minor);

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
            LOG.LogError("Pandoc is not installed and threw an exception:\n {Message}", e.Message);
            return false;
        }
    }

    public static async Task InstallAsync(RustService rustService)
    {
        var installDir = await GetPandocDataFolder(rustService);
        ClearFolder(installDir);
        
        try
        {
            if (!Directory.Exists(installDir))
                Directory.CreateDirectory(installDir);

            using var client = new HttpClient();
            var uri = await GenerateUriAsync();
            
            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, $"Pandoc was not installed successfully, because the download archive was not found."));
                LOG.LogError("Pandoc was not installed, the release archive was not found (Status Code {StatusCode}):\n{Uri}\n{Message}", response.StatusCode, uri, response.RequestMessage);
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
                Console.WriteLine("is zip");
            }
            else
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, $"Pandoc was not installed successfully, because the download archive type is unknown."));
                LOG.LogError("Pandoc was not installed, the download archive is unknown:\n {Uri}", uri);
                return;
            }

            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle,
                $"Pandoc {await FetchLatestVersionAsync()} was installed successfully."));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler: {ex.Message}");
        }
    }
    
    private static void ClearFolder(string path)
    {
        if (!Directory.Exists(path)) return;
        
        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                Directory.Delete(dir, true);
            }
        }
        catch (Exception ex)
        {
            LOG.LogError(ex, "Error clearing pandoc folder.");
        }
    }
    
    public static async Task<string> FetchLatestVersionAsync() {
        using var client = new HttpClient();
        var response = await client.GetAsync(LATEST_URL);

        if (!response.IsSuccessStatusCode)
        {
            LOG.LogError("Code {StatusCode}: Could not fetch pandocs latest page:\n {Response}", response.StatusCode, response.RequestMessage);
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, $"The latest pandoc version was not found, installing version {FALLBACK_VERSION.ToString()} instead."));
            return FALLBACK_VERSION.ToString();
        }

        var htmlContent = await response.Content.ReadAsStringAsync();

        var versionMatch = VersionRegex().Match(htmlContent);
        if (!versionMatch.Success)
        {
            LOG.LogError("The latest version regex returned nothing:\n {Value}", versionMatch.Groups.ToString());
            await MessageBus.INSTANCE.SendWarning(new (Icons.Material.Filled.Warning, $"The latest pandoc version was not found, installing version {FALLBACK_VERSION.ToString()} instead."));
            return FALLBACK_VERSION.ToString();
        }
        
        var version = versionMatch.Groups[1].Value;
        return version;
    }

    // win arm not available
    public static async Task<string> GenerateUriAsync()
    {
        var version = await FetchLatestVersionAsync();
        var baseUri = $"{DOWNLOAD_URL}/{version}/pandoc-{version}-";
        return CPU_ARCHITECTURE switch
        {
            "win-x64" => $"{baseUri}windows-x86_64.zip",
            "osx-x64" => $"{baseUri}x86_64-macOS.zip",
            "osx-arm64" => $"{baseUri}arm64-macOS.zip",
            "linux-x64" => $"{baseUri}linux-amd64.tar.gz",
            "linux-arm" => $"{baseUri}linux-arm64.tar.gz",
            _ => string.Empty,
        };
    }
    
    public static async Task<string> GenerateInstallerUriAsync()
    {
        var version = await FetchLatestVersionAsync();
        var baseUri = $"{DOWNLOAD_URL}/{version}/pandoc-{version}-";

        switch (CPU_ARCHITECTURE)
        {
            case "win-x64":
                return $"{baseUri}windows-x86_64.msi";
            case "osx-x64":
                return $"{baseUri}x86_64-macOS.pkg";
            case "osx-arm64":
                return $"{baseUri}arm64-macOS.pkg\n";
            default:
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Terminal, $"Installers are not available on {CPU_ARCHITECTURE} systems."));
                return string.Empty;
        }
    }

    /// <summary>
    /// Returns the name of the pandoc executable based on the running operating system
    /// </summary>
    private static string GetPandocExecutableName() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pandoc.exe" : "pandoc";

    private static async Task<string> GetPandocDataFolder(RustService rustService) => Path.Join(await rustService.GetDataDirectory(), "pandoc");
    
    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+)")]
    private static partial Regex PandocRegex();
    
    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)")]
    private static partial Regex VersionRegex();
}