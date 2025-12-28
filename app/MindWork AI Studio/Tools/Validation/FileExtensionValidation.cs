using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Validation;

/// <summary>
/// Provides centralized validation for file extensions.
/// </summary>
public static class FileExtensionValidation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileExtensionValidation).Namespace, nameof(FileExtensionValidation));

    /// <summary>
    /// Validates the file extension and sends appropriate MessageBus notifications when invalid.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if valid, false if invalid (error/warning already sent via MessageBus).</returns>
    public static async Task<bool> IsExtensionValidWithNotifyAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        if(FileTypeFilter.Executables.FilterExtensions.Contains(ext))
        {
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.AppBlocking,
                TB("Executables are not allowed")));
            return false;
        }

        if(FileTypeFilter.AllImages.FilterExtensions.Contains(ext))
        {
            await MessageBus.INSTANCE.SendWarning(new(
                Icons.Material.Filled.ImageNotSupported,
                TB("Images are not supported yet")));
            return false;
        }

        if(FileTypeFilter.AllVideos.FilterExtensions.Contains(ext))
        {
            await MessageBus.INSTANCE.SendWarning(new(
                Icons.Material.Filled.FeaturedVideo,
                TB("Videos are not supported yet")));
            return false;
        }

        if(FileTypeFilter.AllAudio.FilterExtensions.Contains(ext))
        {
            await MessageBus.INSTANCE.SendWarning(new(
                Icons.Material.Filled.AudioFile,
                TB("Audio files are not supported yet")));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that the file is a supported image format and sends appropriate MessageBus notifications when invalid.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if valid image, false if invalid (error already sent via MessageBus).</returns>
    public static async Task<bool> IsImageExtensionValidWithNotifyAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.');
        if (string.IsNullOrWhiteSpace(ext))
        {
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.ImageNotSupported,
                TB("File has no extension")));
            return false;
        }

        if (!Array.Exists(FileTypeFilter.AllImages.FilterExtensions, x => x.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.ImageNotSupported,
                TB("Unsupported image format")));
            return false;
        }

        return true;
    }
}
