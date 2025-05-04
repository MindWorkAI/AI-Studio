namespace AIStudio.Tools.PluginSystem;

public class I18N : ILang
{
    public static readonly I18N I = new();
    private static readonly ILogger<I18N> LOG = Program.LOGGER_FACTORY.CreateLogger<I18N>();
    
    private ILanguagePlugin? language = PluginFactory.BaseLanguage;
    
    private I18N()
    {
    }

    public static void Init(ILanguagePlugin language) => I.language = language;

    #region Implementation of ILang

    public string T(string fallbackEN)
    {
        LOG.LogWarning("Using I18N.I.T without namespace and type is probably wrong, because the I18N key collection process of the build system will not find those keys.");
        if(this.language is not null)
            return this.GetText(this.language, fallbackEN);
        
        return fallbackEN;
    }

    public string T(string fallbackEN, string? typeNamespace, string? typeName)
    {
        if(this.language is not null)
            return this.GetText(this.language, fallbackEN, typeNamespace, typeName);
        
        return fallbackEN;
    }

    #endregion
}