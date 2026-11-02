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
/// The function lists the glossaries with their IDs, types, and descriptions, so the model knows
/// what it may look up and when. Without any glossary, the tool offers nothing.
/// </remarks>
public sealed class GlossaryLookupTool(SettingsManager settingsManager, GlossaryStore glossaryStore) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(GlossaryLookupTool).Namespace, nameof(GlossaryLookupTool));

    private const string TEXT_ARGUMENT = "text";
    private const string GLOSSARY_IDS_ARGUMENT = "glossary_ids";
    private const string PAGE_ARGUMENT = "page";

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
        SystemPromptInstructions =
            """
            Use `glossary_lookup` when a text contains terms which may be specific to the user, such as technical terms of the organization, product names, or invented words, and before you translate a text.
            - Pass the passage in question or the terms themselves as `text`; the tool finds every glossary term inside it.
            - Entries of type `translation` are binding: translate their terms exactly with the given translation, adapting only grammatical forms.
            - Entries of type `reference` explain a term. Use them to understand and answer correctly, but do not quote them unasked.
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
    /// Tailors the function to the glossaries: lists them in its description, and allows exactly their IDs.
    /// </summary>
    /// <remarks>
    /// The glossaries keep the order of the settings, so the function stays the same while they do.
    /// The providers cache a request from its beginning, and the tools are part of that beginning.
    /// </remarks>
    internal static ToolFunctionDefinition DescribeGlossaries(ToolFunctionDefinition function, IReadOnlyList<Glossary> glossaries)
    {
        var lines = glossaries.Select(glossary =>
        {
            var line = $"- `{glossary.Id}`: {SingleLine(glossary.Name)} ({DescribeType(glossary)})";
            if (string.IsNullOrWhiteSpace(glossary.Description))
                return line;

            var description = SingleLine(glossary.Description);
            if (description.Length > MAX_DESCRIPTION_CHARACTERS)
                description = description[..MAX_DESCRIPTION_CHARACTERS] + "…";

            return $"{line}: {description}";
        });

        var tailored = CreateFunction(glossaries.Select(glossary => glossary.Id).ToArray());
        return function with
        {
            DescriptionForLLM = $"{FUNCTION_DESCRIPTION}{Environment.NewLine}{Environment.NewLine}Glossaries:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}",
            Parameters = tailored.Parameters,
        };
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
        var page = ToolArgumentReader.ReadOptionalPositiveInt(arguments, PAGE_ARGUMENT, "for the first page") ?? 1;
        if (page > MAX_PAGE)
            throw new ArgumentException($"Argument '{PAGE_ARGUMENT}' was {page}, but at most {MAX_PAGE} pages are available. Pass a shorter passage to narrow the lookup.");

        var matches = new List<GlossaryMatches>();
        foreach (var glossary in glossaries.Where(glossary => chosenIds is null || chosenIds.Contains(glossary.Id)))
        {
            var entries = await glossaryStore.LoadEntriesAsync(glossary.Id, token);
            matches.Add(new(glossary, GlossaryMatcher.For(entries).FindEntries(text)));
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

        var pageEntries = new JsonArray();
        foreach (var (glossary, entry) in allEntries.Skip((page - 1) * ENTRIES_PER_PAGE).Take(ENTRIES_PER_PAGE))
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

        var result = new JsonObject
        {
            ["page"] = page,
            ["has_more"] = allEntries.Count > page * ENTRIES_PER_PAGE,
            ["entries"] = pageEntries,
        };

        if (allEntries.Count == 0)
            result["message"] = "No glossary term occurs in the text.";

        return result;
    }

    private static ToolFunctionDefinition CreateFunction(string[] glossaryIds) => new()
    {
        Name = ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID,
        DescriptionForLLM = FUNCTION_DESCRIPTION,
        Parameters = ToolParameterSchemaBuilder.Create()
            .RequiredString(TEXT_ARGUMENT, "The passage or the terms to look up. Every glossary term which occurs in it is found, also inside compound words.")
            .OptionalStringArray(GLOSSARY_IDS_ARGUMENT, "Optional IDs of the glossaries to search. Leave it out to search all glossaries.", glossaryIds)
            .OptionalInteger(PAGE_ARGUMENT, $"Optional page of the results, starting at 1, with {ENTRIES_PER_PAGE} entries each. Pass it only when the previous result reported has_more.")
            .Build(),
    };

    private static string DescribeType(Glossary glossary) => glossary.Type is GlossaryType.TRANSLATION
        ? $"translation, {LanguageName(glossary.SourceLanguage, glossary.CustomSourceLanguage)} -> {LanguageName(glossary.TargetLanguage, glossary.CustomTargetLanguage)}"
        : "reference";

    private static string LanguageName(CommonLanguages language, string customLanguage) => language is CommonLanguages.OTHER ? SingleLine(customLanguage) : language.ToIETFTag();

    private static string SingleLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}