namespace AIStudio.Settings.DataModel;

public static class PreviewFeaturesExtensions
{
    public static string GetPreviewDescription(this PreviewFeatures feature) => feature switch
    {
        PreviewFeatures.PRE_WRITER_MODE_2024 => "Writer Mode: Experiments about how to write long texts using AI",
        PreviewFeatures.PRE_RAG_2024 => "RAG: Preview of our RAG implementation where you can refer your files or integrate enterprise data within your company",
        
        _ => "Unknown preview feature"
    };
    
    public static bool IsEnabled(this PreviewFeatures feature, SettingsManager settingsManager) => settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Contains(feature);
}