using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class NoPluginLanguage : PluginBase, ILanguagePlugin
{
    public static readonly NoPluginLanguage INSTANCE = new();
    
    private NoPluginLanguage() : base(true, LuaState.Create(), PluginType.LANGUAGE, string.Empty)
    {
    }

    #region Implementation of ILanguagePlugin

    public bool TryGetText(string key, out string value)
    {
        value = string.Empty;
        return true;
    }

    public string this[string key] => string.Empty;

    public string IETFTag => string.Empty;
    
    public string LangName => string.Empty;

    #endregion
}