using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class PluginLanguage : PluginBase, ILanguagePlugin
{
    private readonly Dictionary<string, string> content = [];
    private readonly List<ILanguagePlugin> otherLanguagePlugins = [];
    
    private ILanguagePlugin? baseLanguage;
    
    public PluginLanguage(bool isInternal, LuaState state, PluginType type) : base(isInternal, state, type)
    {
        if (this.TryInitUITextContent(out var issue, out var readContent))
            this.content = readContent;
        else
            this.pluginIssues.Add(issue);
    }
    
    /// <summary>
    /// Sets the base language plugin. This plugin will be used to fill in missing keys.
    /// </summary>
    /// <param name="baseLanguagePlugin">The base language plugin to use.</param>
    public void SetBaseLanguage(ILanguagePlugin baseLanguagePlugin) => this.baseLanguage = baseLanguagePlugin;

    /// <summary>
    /// Add another language plugin. This plugin will be used to fill in missing keys.
    /// </summary>
    /// <remarks>
    /// Use this method to add (i.e., register) an assistant plugin as a language plugin.
    /// This is necessary because the assistant plugins need to serve their own texts.
    /// </remarks>
    /// <param name="languagePlugin">The language plugin to add.</param>
    public void AddOtherLanguagePlugin(ILanguagePlugin languagePlugin) => this.otherLanguagePlugins.Add(languagePlugin);

    /// <summary>
    /// Tries to get a text from the language plugin.
    /// </summary>
    /// <remarks>
    /// When the key neither in the base language nor in this language exist,
    /// the value will be an empty string. Please note that the key is case-sensitive.
    /// Furthermore, the keys are in the format "root::key". That means that
    /// the keys are hierarchical and separated by "::".
    /// </remarks>
    /// <param name="key">The key to use to get the text.</param>
    /// <param name="value">The desired text.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    public bool TryGetText(string key, out string value)
    {
        // First, we check if the key is part of the main language pack:
        if (this.content.TryGetValue(key, out value!))
            return true;
        
        // Second, we check if the key is part of the other language packs, such as the assistant plugins:
        foreach (var otherLanguagePlugin in this.otherLanguagePlugins)
            if(otherLanguagePlugin.TryGetText(key, out value))
                return true;
        
        // Finally, we check if the key is part of the base language pack. This is the case,
        // when a language plugin does not cover all keys. In this case, the base language plugin
        // will be used to fill in the missing keys:
        if(this.baseLanguage is not null && this.baseLanguage.TryGetText(key, out value))
            return true;
        
        value = string.Empty;
        return false;
    }
}