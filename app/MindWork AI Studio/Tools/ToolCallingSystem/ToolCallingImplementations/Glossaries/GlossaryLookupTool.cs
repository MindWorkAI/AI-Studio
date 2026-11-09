using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Glossaries;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Glossaries;

/// <summary>
/// Looks up the terms of a text in the user's glossaries: their fixed translations and their explanations.
/// </summary>
/// <remarks>
/// The model passes a passage or a few terms, and the tool returns the entries whose term or variant
/// occurs in it, by the same matching the translation assistant uses before every request. The
/// glossaries live in AI Studio, so nothing leaves it beyond the provider of the model. What they hold
/// was typed by the user or imported from a file, whose content passed the prompt injection filter
/// on the way in.<br/><br/>
/// Up to <see cref="MAX_LISTED_GLOSSARIES"/> glossaries, the function lists them with their IDs, types, and
/// descriptions, so the model knows what it may look up and when. With more of them, it searches all
/// glossaries, narrowed down by language at most, and the result describes the glossaries of the entries
/// it found. Without any glossary, the tool offers nothing.
/// </remarks>
public sealed class GlossaryLookupTool(SettingsManager settingsManager, GlossaryStore glossaryStore) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(GlossaryLookupTool).Namespace, nameof(GlossaryLookupTool));

    private const string TEXT_ARGUMENT = "text";
    private const string GLOSSARY_IDS_ARGUMENT = "glossary_ids";
    private const string SOURCE_LANGUAGE_ARGUMENT = "source_language";
    private const string TARGET_LANGUAGE_ARGUMENT = "target_language";
    private const string PAGE_ARGUMENT = "page";

    /// <summary>
    /// Up to this many glossaries, the function lists them; with more, it only names their language pairs.
    /// </summary>
    /// <remarks>
    /// The function goes along with every request of a chat, whether the model looks anything up or not.
    /// A listed glossary costs up to about a hundred tokens, so twenty of them stay bearable even for a
    /// local model with a small context window.
    /// </remarks>
    internal const int MAX_LISTED_GLOSSARIES = 20;

    private const int MAX_TEXT_CHARACTERS = 50_000;
    private const int MAX_DESCRIPTION_CHARACTERS = 300;
    private const int ENTRIES_PER_PAGE = 50;
    private const int MAX_PAGE = 20;

    private const string FUNCTION_DESCRIPTION = "Looks up terms in the user's glossaries. Returns the entries whose term or one of its variants occurs in the given text: fixed translations from translation glossaries and explanations from reference glossaries.";

    public string ImplementationKey => ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID;

    public ToolDefinition GetDefinition() => new()
    {
        Id = ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID,
        ImplementationKey = ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID,

        //
        // The translation assistant matches the glossaries the user chose for a translation itself and
        // shows the entries it sent. The tool would search all glossaries besides, also those the user
        // left out or which translate into another language, and its entries would not show up there:
        //
        VisibleIn = new()
        {
            DeniedComponents = [Components.TRANSLATION_ASSISTANT],
        },

        SystemPromptInstructions =
            """
            Use `glossary_lookup` when a text contains terms which may be specific to the user, such as technical terms of the organization, product names, or invented words, and before you translate a text.
            - Pass the passage in question or the terms themselves as `text`; the tool finds every glossary term inside it.
            - Entries of type `translation` are binding: translate their terms exactly with the given translation, adapting only grammatical forms.
            - Entries of type `reference` explain a term. Use them to understand and answer correctly, but do not quote them unasked.
            - The result describes the glossary of each entry. When a term has entries in several glossaries, use these descriptions and the conversation to tell which one applies.
            - Look up the same text only once. When the tool finds nothing, the glossaries hold nothing about it.
            """,
        Function = CreateFunction([]),
    };

    public string Icon => Icons.Material.Filled.MenuBook;

    // The glossaries live in AI Studio; nothing goes anywhere beyond the provider of the model:
    public ToolOutboundData OutboundData => ToolOutboundData.NONE;

    // The text comes from the chat, which the user sees anyway; it holds no secret:
    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => TB("Glossary Lookup");

    public string GetDescription() => TB("Look up the fixed translations and explanations of your glossaries.");

    /// <inheritdoc />
    public ValueTask<ToolFunctionDefinition?> ResolveFunctionAsync(ToolDefinition definition, ToolResolutionContext context, CancellationToken token = default)
    {
        var glossaries = settingsManager.ConfigurationData.Glossaries;
        if (glossaries.Count == 0)
            return ValueTask.FromResult<ToolFunctionDefinition?>(null);

        return ValueTask.FromResult<ToolFunctionDefinition?>(DescribeGlossaries(definition.Function, glossaries));
    }

    /// <summary>
    /// Tailors the function to the glossaries: lists a few of them with their IDs, or names the language
    /// pairs of many of them.
    /// </summary>
    /// <remarks>
    /// A list lets the model choose the glossaries to search by their names and descriptions, but it grows
    /// with every glossary, and so does every request of a chat. Beyond <see cref="MAX_LISTED_GLOSSARIES"/>,
    /// the lookup therefore searches all glossaries, and the model narrows it down by language only; the
    /// result describes the glossaries of the entries it found, cf. <see cref="BuildResult"/>.<br/><br/>
    /// The glossaries keep the order of the settings, so the function stays the same while they do.
    /// The providers cache a request from its beginning, and the tools are part of that beginning. For
    /// the same reason, the function without a list leaves out the number of glossaries.
    /// </remarks>
    internal static ToolFunctionDefinition DescribeGlossaries(ToolFunctionDefinition function, IReadOnlyList<Glossary> glossaries)
    {
        if (glossaries.Count > MAX_LISTED_GLOSSARIES)
        {
            var sourceLanguages = GetLanguages(glossaries, glossary => LanguageName(glossary.SourceLanguage, glossary.CustomSourceLanguage));
            var targetLanguages = GetLanguages(glossaries, glossary => LanguageName(glossary.TargetLanguage, glossary.CustomTargetLanguage));
            var languagePairs = glossaries
                .Where(glossary => glossary.Type is GlossaryType.TRANSLATION)
                .Select(DescribeType)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var summary = languagePairs.Count == 0
                ? "The user has too many glossaries to list them here, all of them reference glossaries. The lookup searches all of them."
                : $"The user has too many glossaries to list them here. The lookup searches all of them; to leave out translation glossaries of other languages, pass {SOURCE_LANGUAGE_ARGUMENT} or {TARGET_LANGUAGE_ARGUMENT}. Reference glossaries are always searched. Translation glossaries:{Environment.NewLine}{string.Join(Environment.NewLine, languagePairs.Select(pair => $"- {pair}"))}";

            return function with
            {
                DescriptionForLLM = $"{FUNCTION_DESCRIPTION}{Environment.NewLine}{Environment.NewLine}{summary}",
                Parameters = CreateFunction([], sourceLanguages, targetLanguages).Parameters,
            };
        }

        var lines = glossaries.Select(glossary =>
        {
            var line = $"- `{glossary.Id}`: {SingleLine(glossary.Name)} ({DescribeType(glossary)})";
            var description = ShortDescription(glossary);
            return description is null ? line : $"{line}: {description}";
        });

        var tailored = CreateFunction(glossaries.Select(glossary => glossary.Id).ToArray());
        return function with
        {
            DescriptionForLLM = $"{FUNCTION_DESCRIPTION}{Environment.NewLine}{Environment.NewLine}Glossaries:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}",
            Parameters = tailored.Parameters,
        };
    }

    /// <summary>
    /// The languages of the translation glossaries on one side, as the model may pass them to narrow the lookup.
    /// </summary>
    private static string[] GetLanguages(IEnumerable<Glossary> glossaries, Func<Glossary, string> language) => glossaries
        .Where(glossary => glossary.Type is GlossaryType.TRANSLATION)
        .Select(language)
        .Where(name => name.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Whether a glossary has to be searched for the languages the model asked for.
    /// </summary>
    /// <remarks>
    /// A reference glossary explains terms in any text, so the languages never leave it out. Regional
    /// variants count as the same language, as in the translation assistant: a glossary into en-US serves
    /// a translation into en-GB as well.
    /// </remarks>
    internal static bool SuitsLanguages(Glossary glossary, string? sourceLanguage, string? targetLanguage)
    {
        if (glossary.Type is not GlossaryType.TRANSLATION)
            return true;

        return (sourceLanguage is null || IsLanguage(glossary.SourceLanguage, glossary.CustomSourceLanguage, sourceLanguage))
               && (targetLanguage is null || IsLanguage(glossary.TargetLanguage, glossary.CustomTargetLanguage, targetLanguage));
    }

    private static bool IsLanguage(CommonLanguages language, string customLanguage, string wantedLanguage) => language is CommonLanguages.OTHER
        ? string.Equals(SingleLine(customLanguage), wantedLanguage, StringComparison.OrdinalIgnoreCase)
        : language.IETFTag is { Length: > 0 } tag && string.Equals(tag.Split('-')[0], wantedLanguage.Split('-')[0], StringComparison.OrdinalIgnoreCase);

    private static string? ShortDescription(Glossary glossary)
    {
        if (string.IsNullOrWhiteSpace(glossary.Description))
            return null;

        var description = SingleLine(glossary.Description);
        return description.Length > MAX_DESCRIPTION_CHARACTERS
            ? description[..MAX_DESCRIPTION_CHARACTERS] + "…"
            : description;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var text = ToolArgumentReader.ReadRequiredString(arguments, TEXT_ARGUMENT);
        if (text.Length > MAX_TEXT_CHARACTERS)
            throw new ArgumentException($"Argument '{TEXT_ARGUMENT}' holds {text.Length} characters, but at most {MAX_TEXT_CHARACTERS} are allowed. Pass a shorter passage or the terms themselves.");

        //
        // The glossaries may have changed since the function was offered, so the IDs are checked
        // against those which exist now:
        //
        var glossaries = settingsManager.ConfigurationData.Glossaries.ToList();
        var allIds = glossaries.Select(glossary => glossary.Id).ToList();
        var chosenIds = ToolArgumentReader.ReadOptionalChoices(arguments, GLOSSARY_IDS_ARGUMENT, allIds, "to search all glossaries");
        var sourceLanguage = ToolArgumentReader.ReadOptionalChoice(arguments, SOURCE_LANGUAGE_ARGUMENT, GetLanguages(glossaries, glossary => LanguageName(glossary.SourceLanguage, glossary.CustomSourceLanguage)), "to search translation glossaries from any language");
        var targetLanguage = ToolArgumentReader.ReadOptionalChoice(arguments, TARGET_LANGUAGE_ARGUMENT, GetLanguages(glossaries, glossary => LanguageName(glossary.TargetLanguage, glossary.CustomTargetLanguage)), "to search translation glossaries into any language");
        var page = ToolArgumentReader.ReadOptionalPositiveInt(arguments, PAGE_ARGUMENT, "for the first page") ?? 1;
        if (page > MAX_PAGE)
            throw new ArgumentException($"Argument '{PAGE_ARGUMENT}' was {page}, but at most {MAX_PAGE} pages are available. Pass a shorter passage to narrow the lookup.");

        var matches = new List<GlossaryMatches>();
        foreach (var glossary in glossaries.Where(glossary => (chosenIds is null || chosenIds.Contains(glossary.Id)) && SuitsLanguages(glossary, sourceLanguage, targetLanguage)))
        {
            var entries = await glossaryStore.TryLoadEntriesAsync(glossary.Id, token);
            matches.Add(entries is null
                ? new(glossary, [], LoadFailed: true)
                : new(glossary, GlossaryMatcher.For(entries).FindEntries(text)));
        }

        return new ToolExecutionResult
        {
            JsonContent = BuildResult(matches, page),
            RequiredProviderConfidence = matches.Any(match => match.Entries.Count > 0) ? this.GetRequiredProviderConfidence() : ConfidenceLevel.NONE,
        };
    }

    /// <summary>
    /// The confidence a provider needs for the rest of the chat once glossary entries reached it.
    /// </summary>
    /// <remarks>
    /// An organization which raises the minimum of this tool keeps its glossaries from less trusted
    /// providers. The registry only checks that minimum before the lookup, though; what the lookup
    /// returned stays in the chat, so the chat has to keep the same requirement afterward. The tool
    /// forms a collection of its own, so its ID is the only one the settings know it by.
    /// </remarks>
    private ConfidenceLevel GetRequiredProviderConfidence() => settingsManager
        .GetMinimumProviderConfidenceResolution([ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID], this.GetDefinition().MinimumProviderConfidence)
        .ConfidenceLevel;

    /// <summary>
    /// Writes one page of the matching entries, all glossaries taken together in their order.
    /// </summary>
    internal static JsonObject BuildResult(IReadOnlyList<GlossaryMatches> matches, int page)
    {
        var allEntries = matches
            .SelectMany(match => match.Entries.Select(entry => (match.Glossary, Entry: entry)))
            .ToList();

        var pageItems = allEntries.Skip((page - 1) * ENTRIES_PER_PAGE).Take(ENTRIES_PER_PAGE).ToList();
        var pageEntries = new JsonArray();
        foreach (var (glossary, entry) in pageItems)
        {
            var item = new JsonObject
            {
                ["glossary_id"] = glossary.Id,
                ["glossary"] = glossary.Name,
                ["type"] = glossary.Type is GlossaryType.TRANSLATION ? "translation" : "reference",
            };

            if (glossary.Type is GlossaryType.TRANSLATION)
            {
                item["source_language"] = LanguageName(glossary.SourceLanguage, glossary.CustomSourceLanguage);
                item["target_language"] = LanguageName(glossary.TargetLanguage, glossary.CustomTargetLanguage);
            }

            item["term"] = entry.Term;
            if (entry.Aliases is { Count: > 0 } aliases)
                item["variants"] = new JsonArray([..aliases.Select(alias => JsonValue.Create(alias))]);

            item[glossary.Type is GlossaryType.TRANSLATION ? "translation" : "explanation"] = entry.Value;
            if (!string.IsNullOrWhiteSpace(entry.Note))
                item["note"] = entry.Note;

            pageEntries.Add(item);
        }

        //
        // The function lists no glossaries once there are many of them, and the names alone hardly tell
        // which meaning of a term applies. Every glossary with an entry on this page is described once:
        //
        var pageGlossaries = new JsonArray();
        foreach (var glossary in pageItems.Select(item => item.Glossary).DistinctBy(glossary => glossary.Id))
        {
            var item = new JsonObject
            {
                ["glossary_id"] = glossary.Id,
                ["glossary"] = glossary.Name,
                ["type"] = glossary.Type is GlossaryType.TRANSLATION ? "translation" : "reference",
            };

            if (ShortDescription(glossary) is { } description)
                item["description"] = description;

            pageGlossaries.Add(item);
        }

        var result = new JsonObject
        {
            ["page"] = page,
            ["has_more"] = allEntries.Count > page * ENTRIES_PER_PAGE,
            ["glossaries"] = pageGlossaries,
            ["entries"] = pageEntries,
        };

        //
        // A glossary which could not be read is no glossary without matches: its terms are unknown,
        // and the model has to tell the user rather than assume that none of them occurs:
        //
        var unavailableGlossaries = matches.Where(match => match.LoadFailed).ToList();
        if (unavailableGlossaries.Count > 0)
        {
            result["unavailable_glossaries"] = new JsonArray([..unavailableGlossaries.Select(match => new JsonObject
            {
                ["glossary_id"] = match.Glossary.Id,
                ["glossary"] = match.Glossary.Name,
            })]);
            result["message"] = "Some glossaries could not be read, so their terms are unknown. Tell the user about it.";
        }
        else if (allEntries.Count == 0)
            result["message"] = "No glossary term occurs in the text.";

        return result;
    }

    /// <summary>
    /// Creates the function, either with the IDs of listed glossaries or with the languages of many of them.
    /// </summary>
    /// <param name="glossaryIds">The IDs the model may choose from, when the glossaries are listed.</param>
    /// <param name="sourceLanguages">The source languages the model may narrow the lookup to; null when the glossaries are listed.</param>
    /// <param name="targetLanguages">The target languages the model may narrow the lookup to; null when the glossaries are listed.</param>
    private static ToolFunctionDefinition CreateFunction(string[] glossaryIds, string[]? sourceLanguages = null, string[]? targetLanguages = null)
    {
        var parameters = ToolParameterSchemaBuilder.Create()
            .RequiredString(TEXT_ARGUMENT, "The passage or the terms to look up. Every glossary term which occurs in it is found, also inside compound words.");

        if (sourceLanguages is null || targetLanguages is null)
            parameters.OptionalStringArray(GLOSSARY_IDS_ARGUMENT, "Optional IDs of the glossaries to search. Leave it out to search all glossaries.", glossaryIds);
        else
        {
            if (sourceLanguages.Length > 0)
                parameters.OptionalEnum(SOURCE_LANGUAGE_ARGUMENT, "Optional language of the terms: searches only the translation glossaries from this language, besides all reference glossaries. Leave it out to search translation glossaries from any language.", sourceLanguages);

            if (targetLanguages.Length > 0)
                parameters.OptionalEnum(TARGET_LANGUAGE_ARGUMENT, "Optional language of the translations: searches only the translation glossaries into this language, besides all reference glossaries. Leave it out to search translation glossaries into any language.", targetLanguages);
        }

        return new()
        {
            Name = ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID,
            DescriptionForLLM = FUNCTION_DESCRIPTION,
            Parameters = parameters
                .OptionalInteger(PAGE_ARGUMENT, $"Optional page of the results, starting at 1, with {ENTRIES_PER_PAGE} entries each. Pass it only when the previous result reported has_more.")
                .Build(),
        };
    }

    private static string DescribeType(Glossary glossary) => glossary.Type is GlossaryType.TRANSLATION
        ? $"translation, {LanguageName(glossary.SourceLanguage, glossary.CustomSourceLanguage)} -> {LanguageName(glossary.TargetLanguage, glossary.CustomTargetLanguage)}"
        : "reference";

    private static string LanguageName(CommonLanguages language, string customLanguage) => language is CommonLanguages.OTHER ? SingleLine(customLanguage) : language.IETFTag;

    private static string SingleLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
