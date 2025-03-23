using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class PluginLanguage : PluginBase, ILanguagePlugin
{
    private readonly Dictionary<string, string> content = [];
    
    private ILanguagePlugin? baseLanguage;
    
    public PluginLanguage(LuaState state, PluginType type) : base(state, type)
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
        if (this.content.TryGetValue(key, out value!))
            return true;
        
        if(this.baseLanguage is not null && this.baseLanguage.TryGetText(key, out value))
            return true;
        
        value = string.Empty;
        return false;
    }
}