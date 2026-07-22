using System.IO.Compression;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed record PluginShareResult(bool Success, string PluginName, string ArchivePath, string Issue);

public sealed class PluginShareService(NativeShareService nativeShareService, ILogger<PluginShareService> logger)
{
    private static PluginShareResult ShareError(IAvailablePlugin plugin, string issue) => new(false, plugin.Name, string.Empty, issue);
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginShareService).Namespace, nameof(PluginShareService));

    public const string PLUGIN_FILE_EXTENSION = ".mwplugin";

    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private const string TEMPORARY_ARCHIVE_DIRECTORY = "mindwork-ai-studio-plugin-shares";
    private const int TEMPORARY_ARCHIVE_RETENTION_HOURS = 24;
    private const int FILE_NAME_PREFIX_MAX_LEN = 80;


    /// <summary>
    /// Creates a shareable plugin archive from a local plugin and opens the native share sheet.
    /// The archive contains the plugin root contents, so <c>plugin.lua</c> is located at the archive root.
    /// </summary>
    /// <param name="plugin">The local plugin to archive and share.</param>
    /// <param name="token">Cancellation token for archive creation.</param>
    /// <returns>The share result, including the retained temporary archive path when successful.</returns>
    public async Task<PluginShareResult> ShareAsync(IAvailablePlugin plugin, CancellationToken token)
    {
        if (plugin.IsInternal)
            return ShareError(plugin, TB("Internal plugins cannot be shared."));

        if (plugin.IsManagedByConfigServer)
            return ShareError(plugin, TB("Config Server managed plugins cannot be shared."));

        if (!TryGetPluginRoot(plugin, out var pluginRoot, out var issue))
            return ShareError(plugin, issue);

        var archiveDirectory = Path.Join(Path.GetTempPath(), TEMPORARY_ARCHIVE_DIRECTORY);
        var archivePath = Path.Join(archiveDirectory, $"{CreateSafeFileNamePrefix(plugin.Name)}-{plugin.Id:N}-{Guid.NewGuid():N}{PLUGIN_FILE_EXTENSION}");

        try
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(archiveDirectory);
            this.CleanUpExpiredArchives(archiveDirectory);

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                ZipFile.CreateFromDirectory(pluginRoot, archivePath, CompressionLevel.Optimal, false);
            }, token);

            token.ThrowIfCancellationRequested();
            if (!await nativeShareService.Share(archivePath))
            {
                this.TryDeleteArchive(archivePath);
                return ShareError(plugin, TB("The native share dialog could not be opened."));
            }

            logger.LogInformation("Created plugin archive '{ArchivePath}' for plugin '{PluginName}' ({PluginId}).", archivePath, plugin.Name, plugin.Id);
            return new(true, plugin.Name, archivePath, string.Empty);
        }
        catch (OperationCanceledException)
        {
            this.TryDeleteArchive(archivePath);
            throw;
        }
        catch (Exception exception)
        {
            this.TryDeleteArchive(archivePath);
            logger.LogError(exception, "Failed to create a share archive for plugin '{PluginName}' ({PluginId}).", plugin.Name, plugin.Id);
            return ShareError(plugin, string.Format(TB("Unexpected error: {0}"), exception.Message));
        }
    }

    private static bool TryGetPluginRoot(IAvailablePlugin plugin, out string pluginRoot, out string issue)
    {
        pluginRoot = string.Empty;
        issue = string.Empty;

        if (string.IsNullOrWhiteSpace(plugin.LocalPath))
        {
            issue = TB("The plugin has no local directory.");
            return false;
        }

        try
        {
            pluginRoot = Path.GetFullPath(plugin.LocalPath);
        }
        catch (Exception exception)
        {
            issue = string.Format(TB("The plugin directory is invalid: {0}"), exception.Message);
            return false;
        }

        if (!Directory.Exists(pluginRoot))
        {
            issue = TB("The plugin directory does not exist.");
            return false;
        }

        var pluginFile = Path.Join(pluginRoot, PLUGIN_FILE_NAME);
        if (!IsPathInsideDirectory(pluginRoot, pluginFile) || !File.Exists(pluginFile))
        {
            issue = TB("The plugin directory does not contain a plugin.lua file.");
            return false;
        }

        return true;
    }

    private void CleanUpExpiredArchives(string archiveDirectory)
    {
        var expiry = DateTime.UtcNow.AddHours(-TEMPORARY_ARCHIVE_RETENTION_HOURS);
        foreach (var archivePath in Directory.EnumerateFiles(archiveDirectory, $"*{PLUGIN_FILE_EXTENSION}", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(archivePath) < expiry)
                    File.Delete(archivePath);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to delete expired plugin archive '{ArchivePath}'.", archivePath);
            }
        }
    }

    private static string CreateSafeFileNamePrefix(string pluginName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var fileName = new string(pluginName
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || ((character is '-' or '_' or '.') && !invalidCharacters.Contains(character))
                ? character
                : '-')
            .ToArray())
            .Trim('-', '.');

        if (fileName.Length > FILE_NAME_PREFIX_MAX_LEN)
            fileName = fileName[..FILE_NAME_PREFIX_MAX_LEN].Trim('-', '.');

        return string.IsNullOrWhiteSpace(fileName) ? "plugin" : fileName;
    }

    private static bool IsPathInsideDirectory(string parentDirectory, string path)
    {
        var parentPath = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var childPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    private void TryDeleteArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
            return;

        try
        {
            File.Delete(archivePath);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to delete temporary plugin archive '{ArchivePath}'.", archivePath);
        }
    }
}
