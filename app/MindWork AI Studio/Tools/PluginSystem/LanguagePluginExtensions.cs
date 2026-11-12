using SharedTools;

namespace AIStudio.Tools.PluginSystem;

public static class LanguagePluginExtensions
{
    /// <param name="plugin">The language plugin.</param>
    extension(ILanguagePlugin plugin)
    {
        /// <summary>
        /// Checks whether the language tells a formal address from an informal one, so that a choice between both
        /// makes sense.
        /// </summary>
        /// <remarks>
        /// The plugin decides by its texts, not by a list of languages: it needs an informal variant of a text of
        /// TA() which reads differently than the formal one. English addresses everybody alike, so its plugin holds
        /// the informal variants in the same words. A language translated before it got informal variants offers
        /// no choice either, since it would change nothing.
        /// </remarks>
        /// <returns>True, when at least one informal variant differs from its formal one.</returns>
        public bool HasInformalAddress()
        {
            var content = plugin.Content;
            foreach (var (key, text) in content)
            {
                if (I18NAddressVariant.TryGetFormalKey(key, out var formalKey) && content.TryGetValue(formalKey, out var formalText) && !string.Equals(text, formalText, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}