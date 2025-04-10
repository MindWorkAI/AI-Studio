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
    private static readonly string DOWNLOAD_URL = "https://github.com/jgm/pandoc/releases/download/3.6.4/pandoc-3.6.4-windows-x86_64.zip";
    private static readonly Version MINIMUM_REQUIRED_VERSION = new Version(3, 6);
    
    /// <summary>
    /// Checks if pandoc is available on the system and can be started as a process
    /// </summary>
    /// <returns>True, if pandoc is available and the minimum required version is met, else False.</returns>
    public static async Task<bool> IsPandocAvailableAsync()
    {
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
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Help, "The pandoc process could not be started."));
                LOG.LogInformation("The pandoc process was not started, it was null");
                return false;
            }
                
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Error, $"The pandoc process exited unexpectedly."));
                LOG.LogError("The pandoc process was exited with code {ProcessExitCode}", process.ExitCode);
                return false;
            }

            var versionMatch = PandocRegex().Match(output);
            if (!versionMatch.Success)
            {
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
                await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, $"Pandoc {installedVersion.ToString()} is installed."));
                return true;
            }
            
            await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Build, $"Pandoc {installedVersion.ToString()} is installed, but it doesn't match the required version ({MINIMUM_REQUIRED_VERSION.ToString()})."));
            LOG.LogInformation("Pandoc {Installed} is installed, but it does not match the required version ({Requirement})", installedVersion.ToString(), MINIMUM_REQUIRED_VERSION.ToString());
            return false;

        }
        catch (Exception e)
        {
            await MessageBus.INSTANCE.SendError(new (@Icons.Material.Filled.AppsOutage, "Pandoc is not installed."));
            LOG.LogError("Pandoc is not installed and threw an exception:\n {Message}", e.Message);
            return false;
        }
    }

    public static async Task InstallPandocAsync(RustService rustService)
    {
        var dataDir = await rustService.GetDataDirectory();
        await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.Help, $"{dataDir}"));
        var installDir = Path.Join(dataDir, "pandoc");
        
        try
        {
            if (!Directory.Exists(installDir))
                Directory.CreateDirectory(installDir);

            using var client = new HttpClient();
            var response = await client.GetAsync(DOWNLOAD_URL);
            if (response.IsSuccessStatusCode)
            {
                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var tempZipPath = Path.Join(Path.GetTempPath(), "pandoc.zip");
                await File.WriteAllBytesAsync(tempZipPath, fileBytes);
                ZipFile.ExtractToDirectory(tempZipPath, installDir);
                File.Delete(tempZipPath);
                
                var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                var pandocDir = Path.Join(currentPath, "pandoc-3.6.4");
                if (currentPath != null && !currentPath.Contains(pandocDir))
                {
                    Environment.SetEnvironmentVariable(
                        "PATH",
                        $"{currentPath};{pandocDir}",
                        EnvironmentVariableTarget.Machine);
                    Console.WriteLine("Pandoc-Verzeichnis zum PATH hinzugefügt.");
                }
                else
                {
                    Console.WriteLine("Pandoc-Verzeichnis ist bereits im PATH.");
                }
                
                await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, $"Pandoc {MINIMUM_REQUIRED_VERSION.ToString()} was installed successfully."));
            }
            else
            {
                Console.WriteLine("Fehler beim Herunterladen von Pandoc.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the name of the pandoc executable based on the running operating system
    /// </summary>
    private static string GetPandocExecutableName() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pandoc.exe" : "pandoc";

    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+)")]
    private static partial Regex PandocRegex();
}