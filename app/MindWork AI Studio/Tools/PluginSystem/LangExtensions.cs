using AIStudio.Settings.DataModel;

using SharedTools;

namespace AIStudio.Tools.PluginSystem;

public static class LangExtensions
{
    private static readonly ILogger<ILang> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ILang>();

    extension(ILang lang)
    {
        /// <summary>
        /// Gets a text from a language plugin.
        /// </summary>
        /// <remarks>
        /// A text of TA() asks for the form of address the person chose. Its informal variant counts only when the
        /// language in use has it: the base language would answer with English, while the formal variant of the
        /// language in use is the better fallback. Missing both, the text stays English.
        /// </remarks>
        /// <param name="plugin">The language plugin in use.</param>
        /// <param name="fallbackEN">The text in English (US), which also makes its key.</param>
        /// <param name="typeNamespace">The namespace of the type requesting the text, or null for the type of the receiver.</param>
        /// <param name="typeName">The name of the type requesting the text, or null for the type of the receiver.</param>
        /// <param name="addressForm">How the text addresses the reader; only texts of TA() have an informal variant.</param>
        /// <returns>The text in the language of the plugin, or the English text.</returns>
        public string GetText(ILanguagePlugin plugin, string fallbackEN, string? typeNamespace = null, string? typeName = null, AddressForm addressForm = AddressForm.FORMAL)
        {
            var type = lang.GetType();
            typeName ??= type.Name;
            typeNamespace ??= type.Namespace!;

            // When the type's name ends with `1 or `2, etc. (i.e., generic classes), remove it:
            if(typeName.Contains('`'))
                typeName = typeName[..typeName.IndexOf('`')];

            var ns = $"{typeNamespace}::{typeName}".ToUpperInvariant().Replace(".", "::");
            var key = $"root::{ns}::T{I18NKeyHash.Of(fallbackEN)}";

            if(plugin is NoPluginLanguage)
                return fallbackEN;

            if(addressForm is AddressForm.INFORMAL && plugin.Content.TryGetValue(I18NAddressVariant.InformalKeyOf(key), out var informalText) && !string.IsNullOrWhiteSpace(informalText))
                return informalText;

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
}