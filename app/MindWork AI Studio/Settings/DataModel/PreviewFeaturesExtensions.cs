using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class PreviewFeaturesExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PreviewFeaturesExtensions).Namespace, nameof(PreviewFeaturesExtensions));
    
    public static string GetPreviewDescription(this PreviewFeatures feature) => feature switch
    {
        PreviewFeatures.PRE_WRITER_MODE_2024 => TB("Writer Mode: Experiments about how to write long texts using AI"),
        PreviewFeatures.PRE_RAG_2024 => TB("RAG: Preview of our RAG implementation where you can refer your files or integrate enterprise data within your company"),
        
        PreviewFeatures.PRE_PLUGINS_2025 => TB("Plugins: Preview of our plugin system where you can extend the functionality of the app"),
        PreviewFeatures.PRE_READ_PDF_2025 => TB("Read PDF: Preview of our PDF reading system where you can read and extract text from PDF files"),
        
        _ => TB("Unknown preview feature")
    };
    
    /// <summary>
    /// Check if the preview feature is released or not.
    /// </summary>
    /// <remarks>
    /// This metadata is necessary because we cannot remove the enum values from the list.
    /// Otherwise, we could not deserialize old settings files that may contain these values.
    /// </remarks>
    /// <param name="feature">The preview feature to check.</param>
    /// <returns>True when the preview feature is released, false otherwise.</returns>
    public static bool IsReleased(this PreviewFeatures feature) => feature switch
    {
        PreviewFeatures.PRE_READ_PDF_2025 => true,
        PreviewFeatures.PRE_PLUGINS_2025 => true,
        
        _ => false
    };
    
    public static bool IsEnabled(this PreviewFeatures feature, SettingsManager settingsManager)
    {
        if(feature.IsReleased())
            return true;
        
        return settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Contains(feature);
    }
}