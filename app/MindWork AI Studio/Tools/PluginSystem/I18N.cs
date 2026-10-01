using System.Globalization;

namespace AIStudio.Tools.PluginSystem;

public class I18N : ILang
{
    public static readonly I18N I = new();
    private static readonly ILogger<I18N> LOG = Program.LOGGER_FACTORY.CreateLogger<I18N>();

    private ILanguagePlugin? language;

    private I18N()
    {
    }

    /// <summary>
    /// How the language in use writes its numbers, or the invariant culture while none is loaded.
    /// </summary>
    /// <remarks>
    /// A number standing inside a translated sentence has to be written the way that language
    /// writes numbers. AI Studio's language is chosen in its own settings and never moves the
    /// thread's culture along with it, so a number formatted from the thread comes out with English
    /// separators inside a German sentence. It lives here because it is the same decision as the
    /// texts: whoever picked the language picked how its numbers look.
    ///
    /// Components which already hold the active plugin may keep deriving it themselves. This is for
    /// the code which has no plugin to ask -- a provider building an error message, say.
    /// </remarks>
    public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

    public static void Init(ILanguagePlugin language)
    {
        I.language = language;
        I.Culture = CommonTools.DeriveActiveCultureOrInvariant(language.IETFTag);
    }

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