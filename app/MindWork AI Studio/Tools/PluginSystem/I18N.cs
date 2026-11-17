using System.Globalization;

using AIStudio.Settings.DataModel;

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

    /// <summary>
    /// Asks where the form of address comes from, see AddressForm.
    /// </summary>
    private Func<AddressForm> addressFormSource = () => AddressForm.FORMAL;

    /// <summary>
    /// How the texts of TA() address the person, in languages which tell a formal address from an informal one.
    /// </summary>
    /// <remarks>
    /// Like the language, it holds for every text, so that a static class, such as one with the labels of an enum,
    /// addresses the person the same way as a component does. Only texts of TA() have an informal variant; all
    /// other texts stay formal. Program.cs decides where the choice comes from, see UseAddressFormOf.
    /// </remarks>
    public AddressForm AddressForm => this.addressFormSource();

    public static void Init(ILanguagePlugin language)
    {
        I.language = language;
        I.Culture = CommonTools.DeriveActiveCultureOrInvariant(language.IETFTag);
    }

    /// <summary>
    /// Sets where the form of address comes from.
    /// </summary>
    /// <remarks>
    /// The source is asked for every text, so a changed setting shows as soon as a component renders again.
    /// </remarks>
    /// <param name="source">Gets the form of address the person chose.</param>
    public static void UseAddressFormOf(Func<AddressForm> source) => I.addressFormSource = source;

    /// <summary>
    /// Gets a text which addresses the reader, in the form of address the person chose; see AddressForm.
    /// </summary>
    /// <param name="fallbackEN">The text in English (US).</param>
    /// <param name="typeNamespace">The namespace of the type requesting the text, used as part of the key.</param>
    /// <param name="typeName">The name of the type requesting the text, used as part of the key.</param>
    /// <returns>The text from the language plugin or the English text.</returns>
    public string TA(string fallbackEN, string? typeNamespace, string? typeName)
    {
        if(this.language is not null)
            return this.GetText(this.language, fallbackEN, typeNamespace, typeName, this.AddressForm);

        return fallbackEN;
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