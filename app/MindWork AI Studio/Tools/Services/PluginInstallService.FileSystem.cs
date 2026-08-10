namespace AIStudio.Tools.Services;

public sealed partial class PluginInstallService
{
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