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

    /// <summary>
    /// Moves a directory and falls back to copying it when the move crosses a file system boundary.
    /// </summary>
    /// <remarks>
    /// On Unix-like systems, a directory move is a plain rename, which fails as soon as source and
    /// destination live on different file systems. A file move falls back to copy and delete in that
    /// case, a directory move does not. Everything this service moves stays below the data directory,
    /// so the fallback is not expected to run. It keeps installing and deleting plugins working when
    /// a setup spreads the data directory across mounts.<br/><br/>
    /// The fallback only applies when the move failed for that reason: when the destination is
    /// already taken, the caller has to learn about it instead of getting the two directories merged.
    /// <br/><br/>
    /// A failing copy leaves nothing behind: the half-written destination is removed before the
    /// error reaches the caller. Every caller rolls back by asking whether the destination exists,
    /// so a partial copy would look like a completed move and keep the backup from being restored.
    /// </remarks>
    /// <param name="sourceDirectory">The directory to move.</param>
    /// <param name="destinationDirectory">The directory to move it to. It must not exist yet.</param>
    private void MoveDirectory(string sourceDirectory, string destinationDirectory)
    {
        try
        {
            Directory.Move(sourceDirectory, destinationDirectory);
            return;
        }
        catch (IOException e) when (Directory.Exists(sourceDirectory) && !Directory.Exists(destinationDirectory))
        {
            this.logger.LogWarning(e, "Was not able to move the directory '{SourceDirectory}' to '{DestinationDirectory}'. Falling back to copying it.", sourceDirectory, destinationDirectory);
        }

        try
        {
            CopyDirectory(sourceDirectory, destinationDirectory);
        }
        catch
        {
            TryDeleteDirectory(destinationDirectory, "partially copied plugin", this.logger);
            throw;
        }

        Directory.Delete(sourceDirectory, true);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
            File.Copy(filePath, Path.Join(destinationDirectory, Path.GetFileName(filePath)), true);

        foreach (var subDirectory in Directory.EnumerateDirectories(sourceDirectory))
            CopyDirectory(subDirectory, Path.Join(destinationDirectory, Path.GetFileName(subDirectory)));
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