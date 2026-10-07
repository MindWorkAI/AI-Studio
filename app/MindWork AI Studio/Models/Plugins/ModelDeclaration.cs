using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AIStudio.Models.Matching;
using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Models.Plugins;

/// <summary>
/// What an organization states about a set of model names, read from one of its model plugins.
/// </summary>
/// <remarks>
/// A declaration says exactly what a family in the source says, and it is measured by the same
/// engine: a pattern, what the models matching it can do, and where that was read. What it must not
/// be is half a statement. A declaration replaces what the built-in rules would have answered, so
/// one which named a context window and nothing else would take away every capability the rules
/// knew -- which is why stating the capabilities is not optional here.
///
/// Whoever only wants to correct one number for their own installation has the better tool already:
/// the expert settings of the configured provider, which a configuration plugin writes as well.
/// A model plugin is for the models the built-in rules do not know, or know wrongly.
/// </remarks>
public sealed record ModelDeclaration : ILivePluginContent
{
    /// <summary>
    /// Which model names this declaration answers for.
    /// </summary>
    public required MatchPattern Pattern { get; init; }

    /// <summary>
    /// What it states about them.
    /// </summary>
    public required ModelProfileChange Change { get; init; }

    /// <summary>
    /// Where that was read, and when somebody last looked.
    /// </summary>
    public required ModelSource Source { get; init; }

    /// <summary>
    /// What the rule built from this declaration names as its origin, so a conflict can name both sides.
    /// </summary>
    public required string Origin { get; init; }

    /// <inheritdoc />
    public Guid EnterpriseConfigurationPluginId { get; init; }

    /// <summary>
    /// What identifies this declaration when two plugins collide.
    /// </summary>
    /// <remarks>
    /// The pattern itself, because that is what a collision is here: two declarations claiming
    /// exactly the same names. They would otherwise both enter the index and tie there, and a tie
    /// is something only a person can settle. Two declarations about different names never meet.
    /// </remarks>
    public string Id => this.Pattern.Signature();

    /// <summary>
    /// Turns the declaration into a rule of the matching engine.
    /// </summary>
    /// <remarks>
    /// Always a selector, never a modifier: a plugin states what a model is, not how to adjust
    /// somebody else's answer about it. And never with an explicit rank -- a declaration already
    /// comes before the built-in rules, so within the plugins the computed specificity decides,
    /// exactly as it does in the source.
    /// </remarks>
    /// <returns>The rule.</returns>
    public ModelRule ToRule() => new(this.Pattern, ModelRuleKind.SELECTOR, this.Change, this.Origin);

    /// <summary>
    /// Reads one entry of a model plugin's MODELS table.
    /// </summary>
    /// <remarks>
    /// Anything it cannot read is rejected as a whole rather than read in part. A declaration is
    /// one statement, and half of one would answer for the models it matches just as firmly as a
    /// complete one -- with whatever the unreadable half was supposed to say silently missing.
    /// </remarks>
    /// <param name="index">Which entry of the table this is, so a warning can name it.</param>
    /// <param name="table">The entry.</param>
    /// <param name="pluginId">The plugin which declared it.</param>
    /// <param name="origin">What the resulting rule names as its origin.</param>
    /// <param name="logger">Where to report what could not be read.</param>
    /// <param name="declaration">The declaration, when the entry could be read.</param>
    /// <returns>True, when the entry could be read.</returns>
    public static bool TryParse(int index, LuaTable table, Guid pluginId, string origin, ILogger logger, [NotNullWhen(true)] out ModelDeclaration? declaration)
    {
        declaration = null;

        if (!TryReadText(table, "PATTERN", out var patternText))
        {
            logger.LogWarning("The model declaration {DeclarationIndex} does not name a PATTERN. Every declaration has to say which model names it answers for. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        if (!MatchPattern.IsNormalized(patternText))
        {
            logger.LogWarning("The model declaration {DeclarationIndex} names the PATTERN '{Pattern}', which is not written the way a model name is written and can therefore never match anything. Write it as '{NormalizedPattern}'. (model plugin id: {PluginId})", index, patternText, new ModelId(patternText).Normalized, pluginId);
            return false;
        }

        if (!TryReadEnum<MatchKind>(table, "MATCH", index, pluginId, logger, out var matchKind, MatchKind.SEGMENT))
            return false;

        if (!TryReadNameParts(table, "ALSO_CONTAINS", index, pluginId, logger, out var alsoContains))
            return false;

        if (!TryReadNameParts(table, "NOT_CONTAINS", index, pluginId, logger, out var notContains))
            return false;

        if (!TryReadOptionalEnum<LLMProviders>(table, "ONLY_ON", index, pluginId, logger, out var onlyOn))
            return false;

        if (!TryReadOptionalEnum<ModelVendor>(table, "ONLY_FROM", index, pluginId, logger, out var onlyFrom))
            return false;

        if (!TryReadCapabilities(table, index, pluginId, logger, out var capabilities))
            return false;

        if (!TryReadEnum<ReasoningSupport>(table, "REASONING", index, pluginId, logger, out var reasoning, ReasoningSupport.NONE))
            return false;

        if (!TryReadEnum<ModelKind>(table, "KIND", index, pluginId, logger, out var modelKind, ModelKind.CHAT))
            return false;

        if (!TryReadContextWindow(table, index, pluginId, logger, out var context))
            return false;

        if (!TryReadTokenizer(table, index, pluginId, logger, out var tokenizer))
            return false;

        if (!TryReadImageLimits(table, index, pluginId, logger, out var images))
            return false;

        if (!TryReadSource(table, index, pluginId, logger, out var source))
            return false;

        declaration = new()
        {
            Pattern = new()
            {
                Kind = matchKind,
                Text = patternText,
                AlsoContains = alsoContains,
                NotContains = notContains,
                OnlyOn = onlyOn,
                OnlyFrom = onlyFrom,
            },

            Change = new()
            {
                Adds = capabilities,
                Reasoning = reasoning,
                Kind = modelKind,
                Context = context,
                Tokenizer = tokenizer,
                Images = images,
            },

            Source = source,
            Origin = origin,
            EnterpriseConfigurationPluginId = pluginId,
        };

        return true;
    }

    private static bool TryReadText(LuaTable table, string key, out string text)
    {
        text = string.Empty;
        if (!table.TryGetValue(key, out var value) || !value.TryRead<string>(out var read))
            return false;

        text = read;
        return !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// Reads a key which names one member of an enum, falling back to a default when it is absent.
    /// </summary>
    /// <remarks>
    /// A member is named, never combined and never numbered. Enum.TryParse accepts both of those,
    /// so the check that the value is actually a member of the enum is what rejects them -- writing
    /// two kinds into one key, or a number nobody can read back, would otherwise pass.
    /// </remarks>
    private static bool TryReadEnum<T>(LuaTable table, string key, int index, Guid pluginId, ILogger logger, out T parsed, T fallback) where T : struct, Enum
    {
        parsed = fallback;
        if (!table.TryGetValue(key, out var value))
            return true;

        if (value.TryRead<string>(out var text) && Enum.TryParse(text, true, out parsed) && Enum.IsDefined(parsed))
            return true;

        logger.LogWarning("The model declaration {DeclarationIndex} states an unknown {Key}. Valid values are: {ValidValues}. (model plugin id: {PluginId})", index, key, string.Join(", ", Enum.GetNames<T>()), pluginId);
        return false;
    }

    private static bool TryReadOptionalEnum<T>(LuaTable table, string key, int index, Guid pluginId, ILogger logger, out T? parsed) where T : struct, Enum
    {
        parsed = null;
        if (!table.TryGetValue(key, out var value))
            return true;

        if (value.TryRead<string>(out var text) && Enum.TryParse<T>(text, true, out var read) && Enum.IsDefined(read))
        {
            parsed = read;
            return true;
        }

        logger.LogWarning("The model declaration {DeclarationIndex} states an unknown {Key}. Valid values are: {ValidValues}. (model plugin id: {PluginId})", index, key, string.Join(", ", Enum.GetNames<T>()), pluginId);
        return false;
    }

    private static bool TryReadNameParts(LuaTable table, string key, int index, Guid pluginId, ILogger logger, out string[] nameParts)
    {
        nameParts = [];
        if (!table.TryGetValue(key, out var value))
            return true;

        if (!value.TryRead<LuaTable>(out var partsTable))
        {
            logger.LogWarning("The model declaration {DeclarationIndex} states {Key}, but not as a list of name parts. (model plugin id: {PluginId})", index, key, pluginId);
            return false;
        }

        var read = new string[partsTable.ArrayLength];
        for (var i = 1; i <= partsTable.ArrayLength; i++)
        {
            if (!partsTable[i].TryRead<string>(out var namePart) || !MatchPattern.IsNormalized(namePart))
            {
                logger.LogWarning("The model declaration {DeclarationIndex} states a {Key} entry which is not a name part written the way a model name is written. (model plugin id: {PluginId})", index, key, pluginId);
                return false;
            }

            read[i - 1] = namePart;
        }

        nameParts = read;
        return true;
    }

    /// <summary>
    /// Reads the capabilities, which every declaration has to state.
    /// </summary>
    /// <remarks>
    /// The three reasoning words are rejected rather than dropped. They are the vocabulary of the
    /// expert settings, where a person answers three questions with yes and no; here one key says
    /// how a model reasons, and the three of them together can state answers no model can give.
    /// </remarks>
    private static bool TryReadCapabilities(LuaTable table, int index, Guid pluginId, ILogger logger, out Capability capabilities)
    {
        capabilities = Capability.NONE;
        if (!table.TryGetValue("CAPABILITIES", out var value) || !value.TryRead<LuaTable>(out var capabilitiesTable) || capabilitiesTable.ArrayLength is 0)
        {
            logger.LogWarning("The model declaration {DeclarationIndex} does not state its CAPABILITIES. A declaration replaces what AI Studio would otherwise know about these models, so it has to say what they can do. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        for (var i = 1; i <= capabilitiesTable.ArrayLength; i++)
        {
            if (!capabilitiesTable[i].TryRead<string>(out var capabilityText) || !Enum.TryParse<Capability>(capabilityText, true, out var capability) || !Enum.IsDefined(capability) || capability is Capability.NONE or Capability.UNKNOWN)
            {
                logger.LogWarning("The model declaration {DeclarationIndex} states an unknown capability. Name one capability per entry, e.g. TEXT_INPUT. (model plugin id: {PluginId})", index, pluginId);
                return false;
            }

            if ((capability & ModelProfile.REASONING_VOCABULARY) is not Capability.NONE)
            {
                logger.LogWarning("The model declaration {DeclarationIndex} states the capability {Capability}, which says how a model reasons. Use the REASONING key instead, which takes exactly one of: {ValidValues}. (model plugin id: {PluginId})", index, capability, string.Join(", ", Enum.GetNames<ReasoningSupport>()), pluginId);
                return false;
            }

            capabilities |= capability;
        }

        return true;
    }

    private static bool TryReadContextWindow(LuaTable table, int index, Guid pluginId, ILogger logger, out ContextWindow? context)
    {
        context = null;
        var raisableIsStated = table.TryGetValue("CONTEXT_WINDOW_RAISABLE_TO", out var raisableValue);
        if (!table.TryGetValue("CONTEXT_WINDOW", out var value))
        {
            if (!raisableIsStated)
                return true;

            logger.LogWarning("The model declaration {DeclarationIndex} states CONTEXT_WINDOW_RAISABLE_TO without stating the CONTEXT_WINDOW it can be raised from. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        if (!value.TryRead<int>(out var defaultTokens) || defaultTokens <= 0)
        {
            logger.LogWarning("The model declaration {DeclarationIndex} states a CONTEXT_WINDOW which is not a number of tokens greater than zero. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        int? raisableTo = null;
        if (raisableIsStated)
        {
            if (!raisableValue.TryRead<int>(out var raisable) || raisable < defaultTokens)
            {
                logger.LogWarning("The model declaration {DeclarationIndex} states a CONTEXT_WINDOW_RAISABLE_TO which is not a number of tokens of at least the CONTEXT_WINDOW itself. (model plugin id: {PluginId})", index, pluginId);
                return false;
            }

            raisableTo = raisable;
        }

        context = ContextWindow.Of(defaultTokens, raisableTo);
        return true;
    }

    private static bool TryReadTokenizer(LuaTable table, int index, Guid pluginId, ILogger logger, out TokenizerRef? tokenizer)
    {
        tokenizer = null;
        var kindIsStated = table.TryGetValue("TOKENIZER_KIND", out _);
        var idIsStated = TryReadText(table, "TOKENIZER_ID", out var tokenizerId);
        if (!kindIsStated && !idIsStated)
            return true;

        if (!kindIsStated || !idIsStated)
        {
            logger.LogWarning("The model declaration {DeclarationIndex} states only one half of its tokenizer. A tokenizer reference needs both TOKENIZER_KIND and TOKENIZER_ID, because the kind is what says how the ID would be resolved. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        if (!TryReadEnum<TokenizerKind>(table, "TOKENIZER_KIND", index, pluginId, logger, out var tokenizerKind, TokenizerKind.UNKNOWN))
            return false;

        tokenizer = new(tokenizerKind, tokenizerId);
        return true;
    }

    private static bool TryReadImageLimits(LuaTable table, int index, Guid pluginId, ILogger logger, out ImageLimits? images)
    {
        images = null;
        if (!TryReadImageLimit(table, "MAX_IMAGES_PER_MESSAGE", index, pluginId, logger, out var maxPerMessage))
            return false;

        if (!TryReadImageLimit(table, "MAX_IMAGES_PER_REQUEST", index, pluginId, logger, out var maxPerRequest))
            return false;

        if (maxPerMessage.HasValue || maxPerRequest.HasValue)
            images = new(maxPerMessage, maxPerRequest);

        return true;
    }

    /// <summary>
    /// Reads one of the two image limits.
    /// </summary>
    /// <remarks>
    /// Zero is a real answer, not a way of saying that nobody knows: an engine can be configured to
    /// take no images at all. Unknown is the key being absent.
    /// </remarks>
    private static bool TryReadImageLimit(LuaTable table, string key, int index, Guid pluginId, ILogger logger, out int? limit)
    {
        limit = null;
        if (!table.TryGetValue(key, out var value))
            return true;

        if (!value.TryRead<int>(out var read) || read < 0)
        {
            logger.LogWarning("The model declaration {DeclarationIndex} states a {Key} which is not a number of images of zero or more. (model plugin id: {PluginId})", index, key, pluginId);
            return false;
        }

        limit = read;
        return true;
    }

    /// <summary>
    /// Reads where the declaration was read from, which it has to name.
    /// </summary>
    /// <remarks>
    /// The compiler asks a family in the source for its source, and the same reasoning holds here:
    /// a model card changes without telling anybody, and a statement nobody can check ages into a
    /// defect. An organization's declaration outlives whoever wrote it, so the page and the day are
    /// what lets the next administrator find out whether it still holds.
    /// </remarks>
    private static bool TryReadSource(LuaTable table, int index, Guid pluginId, ILogger logger, out ModelSource source)
    {
        source = new(string.Empty, default, string.Empty);
        if (!TryReadText(table, "SOURCE_URL", out var url))
        {
            logger.LogWarning("The model declaration {DeclarationIndex} does not name a SOURCE_URL. State where these models are described, e.g. a model card or a page of your own documentation. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        if (!TryReadText(table, "SOURCE_CHECKED_ON", out var checkedOnText) || !DateOnly.TryParseExact(checkedOnText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkedOn))
        {
            logger.LogWarning("The model declaration {DeclarationIndex} does not name a SOURCE_CHECKED_ON as a date of the form YYYY-MM-DD. State the day somebody last read that page. (model plugin id: {PluginId})", index, pluginId);
            return false;
        }

        TryReadText(table, "SOURCE_NOTE", out var note);
        source = new(url, checkedOn, note);
        return true;
    }
}