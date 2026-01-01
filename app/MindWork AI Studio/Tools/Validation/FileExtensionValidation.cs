using AIStudio.Provider;
using AIStudio.Settings;
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
    /// Defines the use cases for file extension validation.
    /// </summary>
    public enum UseCase
    {
        /// <summary>
        /// No specific use case; general validation.
        /// </summary>
        NONE,
        
        /// <summary>
        /// Validating for directly loading content into the UI. In this state, there might be no provider selected yet.
        /// </summary>
        DIRECTLY_LOADING_CONTENT,
        
        /// <summary>
        /// Validating for attaching content to a message or prompt.
        /// </summary>
        ATTACHING_CONTENT,
    }

    /// <summary>
    /// Validates the file extension and sends appropriate MessageBus notifications when invalid.
    /// </summary>
    /// <param name="useCae">The validation use case.</param>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="validateMediaFileTypes">Whether to validate media file types against provider capabilities.</param>
    /// <param name="provider">The selected provider.</param>
    /// <returns>True if valid, false if invalid (error/warning already sent via MessageBus).</returns>
    public static async Task<bool> IsExtensionValidWithNotifyAsync(UseCase useCae, string filePath, bool validateMediaFileTypes = true, Settings.Provider? provider = null)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        if(FileTypeFilter.Executables.FilterExtensions.Contains(ext))
        {
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.AppBlocking,
                TB("Executables are not allowed")));
            return false;
        }

        var capabilities = provider?.GetModelCapabilities() ?? new();
        if (FileTypeFilter.AllImages.FilterExtensions.Contains(ext))
        {
            switch (useCae)
            {
                // In this use case, we cannot guarantee that a provider is selected yet:
                case UseCase.DIRECTLY_LOADING_CONTENT:
                    await MessageBus.INSTANCE.SendWarning(new(
                        Icons.Material.Filled.ImageNotSupported,
                        TB("Images are not supported at this place")));
                    return false;
                
                // In this use case, we don't validate the provider capabilities:
                case UseCase.ATTACHING_CONTENT when !validateMediaFileTypes:
                    return true;
                
                // In this use case, we can check the provider capabilities:
                case UseCase.ATTACHING_CONTENT when capabilities.Contains(Capability.SINGLE_IMAGE_INPUT) ||
                                                    capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT):
                    return true;
                
                // We know that images are not supported:
                case UseCase.ATTACHING_CONTENT:
                    await MessageBus.INSTANCE.SendWarning(new(
                        Icons.Material.Filled.ImageNotSupported,
                        TB("Images are not supported by the selected provider and model")));
                    return false;
                
                default:
                    await MessageBus.INSTANCE.SendWarning(new(
                        Icons.Material.Filled.ImageNotSupported,
                        TB("Images are not supported yet")));
                    return false;
            }
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
