namespace AIStudio.Settings.DataModel;

public static class PreviewFeaturesExtensions
{
    public static string GetPreviewDescription(this PreviewFeatures feature) => feature switch
    {
        PreviewFeatures.PRE_WRITER_MODE_2024 => "Writer Mode: Experiments about how to write long texts using AI",
        PreviewFeatures.PRE_RAG_2024 => "RAG: Preview of our RAG implementation where you can refer your files or integrate enterprise data within your company",
        PreviewFeatures.PRE_PLUGINS_2025 => "Plugins: Preview of our plugin system where you can extend the functionality of the app",
        
        _ => "Unknown preview feature"
    };
    
    public static bool IsEnabled(this PreviewFeatures feature, SettingsManager settingsManager) => settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Contains(feature);
}