using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// A language plugin with texts of its own and a base language behind it, as PluginLanguage has.
/// </summary>
/// <param name="content">The texts of the language itself.</param>
/// <param name="baseContent">The texts of the base language, which answer for the keys the language lacks.</param>
public sealed class FakeLanguagePlugin(IReadOnlyDictionary<string, string> content, IReadOnlyDictionary<string, string>? baseContent = null) : ILanguagePlugin
{
    /// <inheritdoc />
    public bool TryGetText(string key, out string value, bool logWarning = false)
    {
        if (content.TryGetValue(key, out var text) || baseContent is not null && baseContent.TryGetValue(key, out text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public string IETFTag => "de-DE";

    /// <inheritdoc />
    public string LangName => "Deutsch (Deutschland)";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Content => content;
}