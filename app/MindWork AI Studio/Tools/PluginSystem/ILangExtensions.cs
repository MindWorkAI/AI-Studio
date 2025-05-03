using SharedTools;

namespace AIStudio.Tools.PluginSystem;

public static class ILangExtensions
{
    private static readonly ILogger<ILang> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ILang>();
    
    public static string GetText(this ILang lang, ILanguagePlugin plugin, string fallbackEN, string? typeNamespace = null, string? typeName = null)
    {
        var type = lang.GetType();
        typeName ??= type.Name;
        typeNamespace ??= type.Namespace!;
        
        // When the type's name ends with `1 or `2, etc. (i.e., generic classes), remove it:
        if(typeName.Contains('`'))
            typeName = typeName[..typeName.IndexOf('`')];
        
        var ns = $"{typeNamespace}::{typeName}".ToUpperInvariant().Replace(".", "::");
        var key = $"root::{ns}::T{fallbackEN.ToFNV32()}";
        
        if(plugin is NoPluginLanguage)
            return fallbackEN;
        
        if(plugin.TryGetText(key, out var text, logWarning: false))
        {
            if(string.IsNullOrWhiteSpace(text))
                return fallbackEN;
            
            return text;
        }

        LOGGER.LogDebug($"Missing translation key '{key}' for content '{fallbackEN}'.");
        return fallbackEN;
    }
}