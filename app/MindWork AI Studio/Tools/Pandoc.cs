using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AIStudio.Tools;

public static partial class Pandoc
{
    private static readonly Version MINIMUM_REQUIRED_VERSION = new Version(3, 6, 0);

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
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.AppsOutage, $"Pandoc is not installed."));
                return false;
            }
                
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.AppsOutage, $"Pandoc is not installed."));
                return false;
            }

            var versionMatch = PandocRegex().Match(output);
            if (!versionMatch.Success)
            {
                await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.AppsOutage, $"Pandoc is not installed."));
                return false;
            }
            var versions = versionMatch.Groups[1].Value.Split('.');
            var major = int.Parse(versions[0]);
            var minor = int.Parse(versions[1]);
            var patch = int.Parse(versions[2]);
            var installedVersion = new Version(major, minor, patch);
            
            if (installedVersion >= MINIMUM_REQUIRED_VERSION)
                return true;
            
            await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.AppsOutage, $"Pandoc {installedVersion.ToString()} is installed, but it doesn't match the required version ({MINIMUM_REQUIRED_VERSION.ToString()}).\n"));
            return false;

        }
        catch (Exception)
        {
            await MessageBus.INSTANCE.SendError(new (@Icons.Material.Filled.AppsOutage, "An unknown error occured while checking for Pandoc."));
            return false;
        }
    }

    /// <summary>
    /// Returns the name of the pandoc executable based on the running operating system
    /// </summary>
    private static string GetPandocExecutableName() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pandoc.exe" : "pandoc";

    [GeneratedRegex(@"pandoc(?:\.exe)?\s*([0-9]+\.[0-9]+\.[0-9]+)")]
    private static partial Regex PandocRegex();
}