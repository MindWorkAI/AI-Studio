using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class NoPluginLanguage : PluginBase, ILanguagePlugin
{
    public static readonly NoPluginLanguage INSTANCE = new();
    
    private NoPluginLanguage() : base(true, LuaState.Create(), PluginType.LANGUAGE, string.Empty)
    {
    }

    #region Implementation of ILanguagePlugin

    public bool TryGetText(string key, out string value, bool logWarning = false)
    {
        value = string.Empty;
        return true;
    }

    public string IETFTag => string.Empty;
    
    public string LangName => string.Empty;

    public IReadOnlyDictionary<string, string> Content => new Dictionary<string, string>();

    #endregion
}