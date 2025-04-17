using SharedTools;

namespace AIStudio.Tools.PluginSystem;

public static class ILangExtensions
{
    private static readonly ILogger<ILang> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ILang>();
    
    public static string GetText(this ILang lang, ILanguagePlugin plugin, string fallbackEN)
    {
        var type = lang.GetType();
        var ns = $"{type.Namespace!}::{type.Name}".ToUpperInvariant().Replace(".", "::");
        var key = $"root::{ns}::T{fallbackEN.ToFNV32()}";
        
        if(plugin is NoPluginLanguage)
            return fallbackEN;
        
        if(plugin.TryGetText(key, out var text, logWarning: false))
        {
            if(string.IsNullOrWhiteSpace(text))
                return fallbackEN;
            
            return text;
        }

        LOGGER.LogWarning($"Missing translation key '{key}' for content '{fallbackEN}'.");
        return fallbackEN;
    }
}