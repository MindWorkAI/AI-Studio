using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Lists of settings options the app already knows, so a tool definition can point at one
/// instead of spelling it out.
/// </summary>
/// <remarks>
/// A tool setting may declare a fixed list of values through its enum field. That works for a
/// handful of values, but not for lists the app maintains elsewhere: repeating every language in
/// every tool definition would mean the list exists twice and drifts apart. It also leaves the
/// user with raw values in the dropdown, because a plain enum entry carries no readable name.
/// An option source solves both — the values come from one place in the code, together with the
/// translated names.
/// </remarks>
public static class ToolSettingsOptionSources
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolSettingsOptionSources).Namespace, nameof(ToolSettingsOptionSources));

    /// <summary>
    /// The languages a search or translation setting can be set to, as IETF language tags.
    /// </summary>
    public const string COMMON_LANGUAGES = "common_languages";

    /// <summary>
    /// The safe search policies of a search engine.
    /// </summary>
    public const string SAFE_SEARCH = "safe_search";

    /// <summary>
    /// The value asking a search engine not to restrict results to one language.
    /// </summary>
    /// <remarks>
    /// This is SearXNG's own wording for it, and the reason the language list here is not simply
    /// the common languages: those offer "do not change" and "other", which a search engine
    /// cannot act on.<br/><br/>
    /// A search backend whose service words it differently, or which cannot search without a
    /// language at all, recognizes the value by this constant and says in its result what it
    /// did instead.
    /// </remarks>
    public const string ANY_LANGUAGE = "all";

    public static bool IsKnown(string optionSource) => optionSource is COMMON_LANGUAGES or SAFE_SEARCH;

    /// <summary>
    /// Resolves one option source to its current values and names.
    /// </summary>
    /// <remarks>
    /// The names are translated, so this must be called when the dialog renders, not cached.
    /// </remarks>
    public static IReadOnlyList<ToolSettingsOption> Resolve(string optionSource) => optionSource switch
    {
        COMMON_LANGUAGES => BuildLanguageOptions(),
        SAFE_SEARCH =>
        [
            new(nameof(SafeSearchPolicy.OFF), TB("Off")),
            new(nameof(SafeSearchPolicy.MODERATE), TB("Moderate")),
            new(nameof(SafeSearchPolicy.STRICT), TB("Strict")),
        ],

        _ => [],
    };

    /// <summary>
    /// The values an option source accepts, for validating what was stored.
    /// </summary>
    public static IReadOnlySet<string> GetValues(string optionSource) => Resolve(optionSource)
        .Select(option => option.Value)
        .ToHashSet(StringComparer.Ordinal);

    private static List<ToolSettingsOption> BuildLanguageOptions()
    {
        List<ToolSettingsOption> options = [new(ANY_LANGUAGE, TB("Any language"))];
        foreach (var language in Enum.GetValues<CommonLanguages>())
        {
            //
            // Only languages with a real tag: AS_IS and OTHER exist for the assistants, where the
            // user may keep a text as it is or type a language of their own. A search engine needs
            // a concrete tag, and ANY_LANGUAGE above already covers "no preference".
            //
            var tag = language.ToIETFTag();
            if (!string.IsNullOrWhiteSpace(tag))
                options.Add(new(tag, language.Name()));
        }

        return options;
    }
}