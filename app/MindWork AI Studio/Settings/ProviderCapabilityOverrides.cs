using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

using AIStudio.Models;
using AIStudio.Provider;

using Lua;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

/// <summary>
/// What a person stated about the model of their own provider instance, against what the rules
/// worked out. Anything left unsaid keeps the automatic answer.
/// </summary>
/// <remarks>
/// The name says capabilities because that is all this could hold when it was written, and renaming
/// it now would break every settings file and every rolled-out configuration which spells the word.
/// What it holds is everything a person can say about the model behind their own provider: what it
/// can do, how it reasons, how much it reads, and how many pictures it takes.
///
/// The numbers carry the same key names a model plugin uses for the same questions, down to the
/// spelling. The two surfaces answer different questions -- a plugin describes a model, this
/// describes one installation of it -- but an administrator writing both should not have to learn
/// two vocabularies to say the same thing twice.
/// </remarks>
public sealed record ProviderCapabilityOverrides
{
    /// <summary>
    /// How wide the window of this installation is, in tokens.
    /// </summary>
    private const string CONTEXT_WINDOW_KEY = "CONTEXT_WINDOW";

    /// <summary>
    /// How many images one message may carry here.
    /// </summary>
    private const string MAX_IMAGES_PER_MESSAGE_KEY = "MAX_IMAGES_PER_MESSAGE";

    /// <summary>
    /// How many images one request may carry here.
    /// </summary>
    private const string MAX_IMAGES_PER_REQUEST_KEY = "MAX_IMAGES_PER_REQUEST";

    /// <summary>
    /// The keys which name a number rather than a capability.
    /// </summary>
    /// <remarks>
    /// They share the table with the capability words, so the parser has to ask which sort of key
    /// it is looking at before it asks what the value should be: a number where a switch belongs is
    /// as wrong as a switch where a number belongs, and neither may quietly become the other.
    /// </remarks>
    private static readonly IReadOnlyList<string> NUMERIC_KEYS =
    [
        CONTEXT_WINDOW_KEY,
        MAX_IMAGES_PER_MESSAGE_KEY,
        MAX_IMAGES_PER_REQUEST_KEY,
    ];

    /// <summary>
    /// The capabilities a person switches on or off directly, without the reasoning words.
    /// </summary>
    /// <remarks>
    /// How a model reasons is one answer out of four, not three flags which can contradict each
    /// other, so it is resolved on its own below. The three words stay in the list above because
    /// that is the vocabulary a settings file and a configuration plugin are written in.
    /// </remarks>
    private static readonly IReadOnlyList<Capability> DIRECTLY_SETTABLE_CAPABILITIES =
    [
        Capability.AUDIO_INPUT,
        Capability.FUNCTION_CALLING,
        Capability.MULTIPLE_IMAGE_INPUT,
        Capability.SPEECH_INPUT,
        Capability.VIDEO_INPUT,
    ];

    private static readonly IReadOnlyList<Capability> SUPPORTED_CAPABILITIES =
    [
        Capability.AUDIO_INPUT,
        Capability.FUNCTION_CALLING,
        Capability.MULTIPLE_IMAGE_INPUT,
        Capability.SPEECH_INPUT,
        Capability.VIDEO_INPUT,
        Capability.OPTIONAL_REASONING,
        Capability.ALWAYS_REASONING,
        Capability.REASONING_BY_DEFAULT
    ];

    [JsonPropertyName("AUDIO_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AudioInput { get; init; }

    [JsonPropertyName("FUNCTION_CALLING")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FunctionCalling { get; init; }

    [JsonPropertyName("MULTIPLE_IMAGE_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MultipleImageInput { get; init; }

    [JsonPropertyName("SPEECH_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SpeechInput { get; init; }

    [JsonPropertyName("VIDEO_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? VideoInput { get; init; }

    [JsonPropertyName("OPTIONAL_REASONING")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OptionalReasoning { get; init; }

    [JsonPropertyName("ALWAYS_REASONING")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AlwaysReasoning { get; init; }

    [JsonPropertyName("REASONING_BY_DEFAULT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReasoningByDefault { get; init; }

    /// <summary>
    /// How many tokens this installation reads and writes, or null to keep the automatic answer.
    /// </summary>
    /// <remarks>
    /// One number, where the rules know two. What a model card calls "raisable to" is a statement
    /// about the model: somebody could configure the engine that way. A person filling this in has
    /// already configured it, or has not, and either way says what their installation does today.
    /// Stating a ceiling next to it would be describing a possibility they are the only one able to
    /// realize.
    /// </remarks>
    [JsonPropertyName(CONTEXT_WINDOW_KEY)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ContextWindowTokens { get; init; }

    /// <summary>
    /// How many images one message may carry, or null to keep the automatic answer.
    /// </summary>
    [JsonPropertyName(MAX_IMAGES_PER_MESSAGE_KEY)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxImagesPerMessage { get; init; }

    /// <summary>
    /// How many images one request may carry, or null to keep the automatic answer.
    /// </summary>
    [JsonPropertyName(MAX_IMAGES_PER_REQUEST_KEY)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxImagesPerRequest { get; init; }

    [JsonIgnore]
    public bool HasOverrides =>
        this.AudioInput is not null ||
        this.FunctionCalling is not null ||
        this.MultipleImageInput is not null ||
        this.SpeechInput is not null ||
        this.VideoInput is not null ||
        this.OptionalReasoning is not null ||
        this.AlwaysReasoning is not null ||
        this.ReasoningByDefault is not null ||
        this.ContextWindowTokens is not null ||
        this.MaxImagesPerMessage is not null ||
        this.MaxImagesPerRequest is not null;

    public bool? GetOverride(Capability capability) => capability switch
    {
        Capability.AUDIO_INPUT => this.AudioInput,
        Capability.FUNCTION_CALLING => this.FunctionCalling,
        Capability.MULTIPLE_IMAGE_INPUT => this.MultipleImageInput,
        Capability.SPEECH_INPUT => this.SpeechInput,
        Capability.VIDEO_INPUT => this.VideoInput,
        Capability.OPTIONAL_REASONING => this.OptionalReasoning,
        Capability.ALWAYS_REASONING => this.AlwaysReasoning,
        Capability.REASONING_BY_DEFAULT => this.ReasoningByDefault,
        _ => null
    };

    public ProviderCapabilityOverrides SetOverride(Capability capability, bool? value) => capability switch
    {
        Capability.AUDIO_INPUT => this with { AudioInput = value },
        Capability.FUNCTION_CALLING => this with { FunctionCalling = value },
        Capability.MULTIPLE_IMAGE_INPUT => this with { MultipleImageInput = value },
        Capability.SPEECH_INPUT => this with { SpeechInput = value },
        Capability.VIDEO_INPUT => this with { VideoInput = value },
        Capability.OPTIONAL_REASONING => this with { OptionalReasoning = value },
        Capability.ALWAYS_REASONING => this with { AlwaysReasoning = value },
        Capability.REASONING_BY_DEFAULT => this with { ReasoningByDefault = value },
        _ => this
    };

    /// <summary>
    /// Reads the number a key stands for.
    /// </summary>
    /// <param name="key">One of the numeric keys.</param>
    /// <returns>The number, or null when nobody stated it.</returns>
    private int? GetNumber(string key) => key switch
    {
        CONTEXT_WINDOW_KEY => this.ContextWindowTokens,
        MAX_IMAGES_PER_MESSAGE_KEY => this.MaxImagesPerMessage,
        MAX_IMAGES_PER_REQUEST_KEY => this.MaxImagesPerRequest,

        _ => null,
    };

    /// <summary>
    /// States the number a key stands for.
    /// </summary>
    /// <param name="key">One of the numeric keys.</param>
    /// <param name="value">The number, or null to keep the automatic answer.</param>
    /// <returns>The overrides with that number in them.</returns>
    private ProviderCapabilityOverrides SetNumber(string key, int? value) => key switch
    {
        CONTEXT_WINDOW_KEY => this with { ContextWindowTokens = value },
        MAX_IMAGES_PER_MESSAGE_KEY => this with { MaxImagesPerMessage = value },
        MAX_IMAGES_PER_REQUEST_KEY => this with { MaxImagesPerRequest = value },

        _ => this,
    };

    /// <summary>
    /// Applies what a person said about their own installation to what the rules worked out.
    /// </summary>
    /// <remarks>
    /// The topmost link of the chain: an explicit statement about one's own provider wins over
    /// everything the rules could know, because the person can see the installation and the rules
    /// cannot.
    /// </remarks>
    /// <param name="profile">What the rules worked out.</param>
    /// <returns>The profile as this provider instance was told it is.</returns>
    public ModelProfile ApplyTo(in ModelProfile profile) => profile with
    {
        Capabilities = this.ApplyToCapabilities(profile.Capabilities),
        Reasoning = this.ResolveReasoning(profile.Reasoning),
        Context = this.ResolveContext(profile.Context),
        Images = this.ResolveImages(profile.Images),
    };

    /// <summary>
    /// Works out how wide the window is, out of what the rules say and what a person said.
    /// </summary>
    /// <remarks>
    /// A stated number replaces the window whole, the ceiling included. Keeping "raisable to
    /// 131,072" next to a person's own 16,384 would be reporting a possibility as a property of
    /// their installation, and whoever reads that number is asking what fits, not what could be
    /// made to fit.
    ///
    /// A number which is not a width at all is ignored rather than repaired. Both places a person
    /// can write one refuse it with a message, so one arriving here came out of a settings file
    /// somebody edited by hand, and the honest answer to that is the one nobody made up.
    /// </remarks>
    /// <param name="stated">What the rules worked out.</param>
    /// <returns>The window after the overrides.</returns>
    private ContextWindow ResolveContext(ContextWindow stated) => this.ContextWindowTokens is { } tokens and > 0 ? ContextWindow.Of(tokens) : stated;

    /// <summary>
    /// Works out how many images fit, out of what the rules say and what a person said.
    /// </summary>
    /// <remarks>
    /// Each of the two numbers stands for itself, the way each switch above does: stating one says
    /// nothing about the other, and the one left unsaid keeps whatever the rules worked out. The
    /// smaller of the two still decides what fits into a message, so a person who states the larger
    /// number alone may well see no change -- which is the correct answer, not a bug: they have not
    /// contradicted the limit that is actually in the way.
    /// </remarks>
    /// <param name="stated">What the rules worked out.</param>
    /// <returns>The limits after the overrides.</returns>
    private ImageLimits ResolveImages(ImageLimits stated) => new(CountOfImages(this.MaxImagesPerMessage) ?? stated.MaxPerMessage, CountOfImages(this.MaxImagesPerRequest) ?? stated.MaxPerRequest);

    /// <summary>
    /// Takes a stated image limit, where it is one.
    /// </summary>
    /// <remarks>
    /// Zero is a real limit here: an engine can be configured to take no pictures at all. A
    /// negative number is not a limit at all, and is ignored for the same reason a window of zero
    /// tokens is.
    /// </remarks>
    /// <param name="limit">What was stated.</param>
    /// <returns>The limit, or null when nothing usable was stated.</returns>
    private static int? CountOfImages(int? limit) => limit >= 0 ? limit : null;

    /// <summary>
    /// Switches the plain capabilities on and off.
    /// </summary>
    /// <param name="stated">What the rules worked out.</param>
    /// <returns>The capabilities after the overrides.</returns>
    private Capability ApplyToCapabilities(Capability stated)
    {
        var capabilities = stated;
        foreach (var capability in DIRECTLY_SETTABLE_CAPABILITIES)
            switch (this.GetOverride(capability))
            {
                case true:
                    capabilities |= capability;
                    break;

                case false:
                    capabilities &= ~capability;
                    break;
            }

        return capabilities;
    }

    /// <summary>
    /// Works out how a model reasons, out of what the rules say and what a person said.
    /// </summary>
    /// <remarks>
    /// This replaced thirty lines which repaired states that cannot exist -- a model both always
    /// reasoning and reasoning on request -- by an answer which cannot be in two of them at once.
    /// The expert dialog writes all three words together, and every combination it produces means
    /// exactly what it meant before.
    ///
    /// One thing did change, and it is a defect going away. A word nobody said anything about used
    /// to destroy the answer: a provider carrying any override at all, say tool calling turned off,
    /// lost "reasoning on by default" on the way through, because the repair took the word away
    /// unless "reasoning on request" stood next to it -- which no rule ever states. Here a "no" only
    /// takes away what it names.
    /// </remarks>
    /// <param name="stated">How the rules say the model reasons.</param>
    /// <returns>How it reasons after the overrides.</returns>
    private ReasoningSupport ResolveReasoning(ReasoningSupport stated)
    {
        // A "yes" is the whole answer, whatever else is written next to it:
        if (this.AlwaysReasoning is true)
            return ReasoningSupport.ALWAYS;

        if (this.ReasoningByDefault is true)
            return ReasoningSupport.ON_BY_DEFAULT;

        if (this.OptionalReasoning is true)
            return ReasoningSupport.OPTIONAL;

        // A "no" only contradicts the state it names:
        return stated switch
        {
            ReasoningSupport.ALWAYS => this.AlwaysReasoning is false ? ReasoningSupport.NONE : ReasoningSupport.ALWAYS,
            ReasoningSupport.ON_BY_DEFAULT => this.ReasoningByDefault is false || this.OptionalReasoning is false ? ReasoningSupport.NONE : ReasoningSupport.ON_BY_DEFAULT,
            ReasoningSupport.OPTIONAL => this.OptionalReasoning is false ? ReasoningSupport.NONE : ReasoningSupport.OPTIONAL,

            _ => ReasoningSupport.NONE,
        };
    }

    public string ExportAsLuaTable(string indentation)
    {
        if (!this.HasOverrides)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine($@"{indentation}[""CapabilityOverrides""] = {{");
        foreach (var capability in SUPPORTED_CAPABILITIES)
        {
            var overrideValue = this.GetOverride(capability);
            if (overrideValue is null)
                continue;

            builder.AppendLine($@"{indentation}    [""{capability}""] = {overrideValue.Value.ToString().ToLowerInvariant()},");
        }

        foreach (var key in NUMERIC_KEYS)
        {
            if (this.GetNumber(key) is not { } number)
                continue;

            builder.AppendLine($@"{indentation}    [""{key}""] = {number.ToString(CultureInfo.InvariantCulture)},");
        }

        builder.Append($@"{indentation}}},");
        return builder.ToString();
    }

    public static ProviderCapabilityOverrides? TryParseFromLuaTable(int idx, LuaTable providerTable, Guid configPluginId, ILogger logger)
    {
        if (!providerTable.TryGetValue("CapabilityOverrides", out var capabilityOverridesValue))
            return null;

        if (capabilityOverridesValue.Type is not LuaValueType.Table || !capabilityOverridesValue.TryRead<LuaTable>(out var capabilityOverridesTable))
        {
            logger.LogWarning("The configured provider {ProviderIndex} contains an invalid CapabilityOverrides table. Automatic capability detection will be used instead. (Plugin ID: {PluginId})", idx, configPluginId);
            return null;
        }

        var result = new ProviderCapabilityOverrides();
        var previousKey = LuaValue.Nil;
        while (capabilityOverridesTable.TryGetNext(previousKey, out var pair))
        {
            previousKey = pair.Key;

            if (!pair.Key.TryRead<string>(out var keyText))
            {
                logger.LogWarning("The configured provider {ProviderIndex} contains a CapabilityOverrides entry with a non-string key. The entry will be ignored. (Plugin ID: {PluginId})", idx, configPluginId);
                continue;
            }

            if (TryMatchNumericKey(keyText, out var numericKey))
            {
                if (!TryReadNumber(pair.Value, numericKey, out var number))
                {
                    logger.LogWarning("The configured provider {ProviderIndex} states a '{OverrideKey}' which is not {Expectation}. The automatic answer will be used for it. (Plugin ID: {PluginId})", idx, numericKey, ExpectationOf(numericKey), configPluginId);
                    continue;
                }

                result = result.SetNumber(numericKey, number);
                continue;
            }

            if (!TryParseSupportedCapability(keyText, out var capability))
            {
                logger.LogWarning("The configured provider {ProviderIndex} contains an unsupported override '{OverrideKey}'. The entry will be ignored. (Plugin ID: {PluginId})", idx, keyText, configPluginId);
                continue;
            }

            if (!pair.Value.TryRead<bool>(out var overrideValue))
            {
                logger.LogWarning("The configured provider {ProviderIndex} contains a non-boolean capability override for '{CapabilityKey}'. Automatic capability detection will be used for that capability. (Plugin ID: {PluginId})", idx, keyText, configPluginId);
                continue;
            }

            result = result.SetOverride(capability, overrideValue);
        }

        return result.HasOverrides ? result : null;
    }

    /// <summary>
    /// Recognizes a key which names a number, whichever way it was spelled.
    /// </summary>
    /// <remarks>
    /// Spelled loosely for the same reason the capability words are: a table written by hand is
    /// read by the app, not by a compiler, and rejecting "context_window" over its letters would be
    /// a riddle rather than a message. What comes back is the canonical spelling, so everything
    /// after this point deals with one name per question.
    /// </remarks>
    /// <param name="key">The key as it was written.</param>
    /// <param name="numericKey">The canonical spelling of that key.</param>
    /// <returns>True when the key names a number.</returns>
    private static bool TryMatchNumericKey(string key, out string numericKey)
    {
        foreach (var candidate in NUMERIC_KEYS)
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            {
                numericKey = candidate;
                return true;
            }

        numericKey = string.Empty;
        return false;
    }

    /// <summary>
    /// Reads a number, where it is one this key accepts.
    /// </summary>
    /// <remarks>
    /// A window has to be a width, so zero token is refused: nothing fits into it, and a provider
    /// which can hold nothing is not what anybody meant to state. A picture count of zero is a
    /// different matter and allowed because an engine really can be told to take no pictures.
    /// </remarks>
    /// <param name="value">The value as it stands in the table.</param>
    /// <param name="numericKey">The canonical key it stands under.</param>
    /// <param name="number">The number read.</param>
    /// <returns>True, when the value is a number, this key accepts.</returns>
    private static bool TryReadNumber(LuaValue value, string numericKey, out int number)
    {
        if (!value.TryRead(out number))
            return false;

        return numericKey is CONTEXT_WINDOW_KEY ? number > 0 : number >= 0;
    }

    /// <summary>
    /// What a key accepts, said in the words of a warning.
    /// </summary>
    /// <param name="numericKey">The canonical key.</param>
    /// <returns>The expectation.</returns>
    private static string ExpectationOf(string numericKey) => numericKey is CONTEXT_WINDOW_KEY ? "a number of tokens greater than zero" : "a number of images of zero or more";

    private static bool TryParseSupportedCapability(string capabilityKey, out Capability capability)
    {
        capability = Capability.NONE;
        if (!Enum.TryParse(capabilityKey, true, out capability))
            return false;

        return SUPPORTED_CAPABILITIES.Contains(capability);
    }
}
