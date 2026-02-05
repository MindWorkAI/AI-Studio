using AIStudio.Provider.HuggingFace;

namespace AIStudio.Tools.PluginSystem;

public static class ConfigurationExport
{
    public static string ExportProvider(AIStudio.Settings.Provider provider)
    {
        if (provider == Settings.Provider.NONE)
            return string.Empty;

        var hfInferenceProviderLine = provider.HFInferenceProvider is not HFInferenceProvider.NONE
            ? $"            [\"HFInferenceProvider\"] = \"{provider.HFInferenceProvider}\",\n"
            : string.Empty;

        return $$"""
                 CONFIG["LLM_PROVIDERS"][#CONFIG["LLM_PROVIDERS"]+1] = {
                     ["Id"] = "{{EscapeLuaString(NormalizeId(provider.Id))}}",
                     ["InstanceName"] = "{{EscapeLuaString(provider.InstanceName)}}",
                     ["UsedLLMProvider"] = "{{provider.UsedLLMProvider}}",
                 
                     ["Host"] = "{{provider.Host}}",
                     ["Hostname"] = "{{EscapeLuaString(provider.Hostname)}}",
                     {{hfInferenceProviderLine}}
                     ["AdditionalJsonApiParameters"] = "{{EscapeLuaString(provider.AdditionalJsonApiParameters)}}",
                     ["Model"] = {
                         ["Id"] = "{{EscapeLuaString(provider.Model.Id)}}",
                         ["DisplayName"] = "{{EscapeLuaString(provider.Model.DisplayName ?? string.Empty)}}",
                     },
                 }
                 """;
    }

    public static string ExportEmbeddingProvider(Settings.EmbeddingProvider provider)
    {
        if (provider == Settings.EmbeddingProvider.NONE)
            return string.Empty;

        return $$"""
                 CONFIG["EMBEDDING_PROVIDERS"][#CONFIG["EMBEDDING_PROVIDERS"]+1] = {
                     ["Id"] = "{{EscapeLuaString(NormalizeId(provider.Id))}}",
                     ["Name"] = "{{EscapeLuaString(provider.Name)}}",
                     ["UsedLLMProvider"] = "{{provider.UsedLLMProvider}}",
                 
                     ["Host"] = "{{provider.Host}}",
                     ["Hostname"] = "{{EscapeLuaString(provider.Hostname)}}",
                     ["Model"] = {
                         ["Id"] = "{{EscapeLuaString(provider.Model.Id)}}",
                         ["DisplayName"] = "{{EscapeLuaString(provider.Model.DisplayName ?? string.Empty)}}",
                     },
                 }
                 """;
    }

    public static string ExportTranscriptionProvider(Settings.TranscriptionProvider provider)
    {
        if (provider == Settings.TranscriptionProvider.NONE)
            return string.Empty;

        return $$"""
                 CONFIG["TRANSCRIPTION_PROVIDERS"][#CONFIG["TRANSCRIPTION_PROVIDERS"]+1] = {
                     ["Id"] = "{{EscapeLuaString(NormalizeId(provider.Id))}}",
                     ["Name"] = "{{EscapeLuaString(provider.Name)}}",
                     ["UsedLLMProvider"] = "{{provider.UsedLLMProvider}}",
                 
                     ["Host"] = "{{provider.Host}}",
                     ["Hostname"] = "{{EscapeLuaString(provider.Hostname)}}",
                     ["Model"] = {
                         ["Id"] = "{{EscapeLuaString(provider.Model.Id)}}",
                         ["DisplayName"] = "{{EscapeLuaString(provider.Model.DisplayName ?? string.Empty)}}",
                     },
                 }
                 """;
    }

    private static string NormalizeId(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return Guid.NewGuid().ToString();
    }

    private static string EscapeLuaString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}
