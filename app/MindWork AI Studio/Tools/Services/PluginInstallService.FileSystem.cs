using AIStudio.Settings;

namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
    /// <summary>
    /// Creates a staging directory for a plugin that is about to be installed.
    /// </summary>
    /// <remarks>
    /// The staging directory lives below the data directory, never below the temporary directory of
    /// the operating system. Installing means moving the staged plugin into the plugins directory,
    /// and a directory move cannot cross a file system boundary. Flatpak is the case where this
    /// always applies: its temporary directory is a tmpfs inside the sandbox, while the data
    /// directory lives in the home directory of the user.<br/><br/>
    /// It lives next to the plugins directory, not inside it: the plugin loader searches the plugins
    /// directory recursively, so a half-written plugin there would be loaded while it is still being
    /// staged.
    /// </remarks>
    /// <param name="prefix">The prefix of the staging directory name, naming the caller.</param>
    /// <param name="stagingDirectory">The created staging directory.</param>
    /// <param name="issue">A user-facing issue when the staging directory could not be created.</param>
    /// <returns>True when the staging directory exists, false otherwise.</returns>
    private bool TryCreateStagingDirectory(string prefix, out string stagingDirectory, out string issue)
    {
        stagingDirectory = string.Empty;
        issue = string.Empty;

        var dataDirectory = SettingsManager.DataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            issue = TB("The AI Studio data directory is not initialized yet.");
            return false;
        }

        var stagingRoot = Path.Join(dataDirectory, STAGING_DIRECTORY);
        try
        {
            Directory.CreateDirectory(stagingRoot);
            this.CleanUpExpiredStagingDirectories(stagingRoot);

            stagingDirectory = Path.Join(stagingRoot, $"{prefix}.staging-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            return true;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to create the plugin staging directory below '{StagingRoot}'.", stagingRoot);
            stagingDirectory = string.Empty;
            issue = string.Format(TB("Unexpected error: {0}"), e.Message);
            return false;
        }
    }

    /// <summary>
    /// Removes staging directories which an earlier installation left behind, e.g. after a crash.
    /// </summary>
    private void CleanUpExpiredStagingDirectories(string stagingRoot)
    {
        var expiry = DateTime.UtcNow.AddHours(-STAGING_RETENTION_HOURS);
        foreach (var leftOverDirectory in Directory.EnumerateDirectories(stagingRoot, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(leftOverDirectory) < expiry)
                    Directory.Delete(leftOverDirectory, true);
            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "Failed to delete the left-over plugin staging directory '{StagingDirectory}'.", leftOverDirectory);
            }
        }
    }

    private static bool IsPathInsideDirectory(string parentDirectory, string path)
    {
        var parentPath = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var childPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameDirectory(string firstDirectory, string secondDirectory)
    {
        var firstPath = Path.GetFullPath(firstDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var secondPath = Path.GetFullPath(secondDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
    }

    private void TryDeleteStagingDirectory(string stagingDirectory) => TryDeleteDirectory(stagingDirectory, "assistant plugin staging", this.logger);

    private static void TryDeleteDirectory(string directory, string directoryDescription, ILogger logger)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to delete {directoryDescription} directory '{directory}'.");
        }
    }

    private void TryDeleteFile(string filePath, string fileDescription)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            File.Delete(filePath);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to delete {fileDescription} '{filePath}'.");
        }
    }
}