using System.Text;
using System.Text.Json.Serialization;

using AIStudio.Models;
using AIStudio.Provider;

using Lua;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

/// <summary>
/// Optional expert capability overrides for a configured LLM provider.
/// Missing values keep the automatic capability detection result.
/// </summary>
public sealed record ProviderCapabilityOverrides
{
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

    [JsonIgnore]
    public bool HasOverrides =>
        this.AudioInput is not null ||
        this.FunctionCalling is not null ||
        this.MultipleImageInput is not null ||
        this.SpeechInput is not null ||
        this.VideoInput is not null ||
        this.OptionalReasoning is not null ||
        this.AlwaysReasoning is not null ||
        this.ReasoningByDefault is not null;

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
    };

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
    /// This replaces thirty lines which repaired states that could not exist -- a model both always
    /// reasoning and reasoning on request -- by an answer which cannot be in two of them at once.
    /// The expert dialog writes all three words together, so the five combinations it produces are
    /// answered exactly as they are today.
    ///
    /// One thing changes, and it is a defect going away. A word nobody said anything about used to
    /// destroy the answer: a provider carrying any override at all, say tool calling turned off, lost
    /// "reasoning on by default" on the way through, because the old repair took the word away unless
    /// "reasoning on request" stood next to it -- which no rule ever states. Here a "no" only takes
    /// away what it names.
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

    public List<Capability> ApplyTo(IEnumerable<Capability> automaticCapabilities)
    {
        var mergedCapabilities = automaticCapabilities.Distinct().ToList();
        foreach (var capability in SUPPORTED_CAPABILITIES)
        {
            var overrideValue = this.GetOverride(capability);
            if (overrideValue == true && !mergedCapabilities.Contains(capability))
                mergedCapabilities.Add(capability);
            else if (overrideValue == false)
                mergedCapabilities.Remove(capability);
        }

        this.NormalizeReasoningCapabilities(mergedCapabilities);
        return mergedCapabilities;
    }

    private void NormalizeReasoningCapabilities(List<Capability> capabilities)
    {
        if (this.AlwaysReasoning == true ||
            this.AlwaysReasoning is not false &&
            this.OptionalReasoning is not true &&
            this.ReasoningByDefault is not true &&
            capabilities.Contains(Capability.ALWAYS_REASONING))
        {
            capabilities.Remove(Capability.OPTIONAL_REASONING);
            capabilities.Remove(Capability.REASONING_BY_DEFAULT);
            return;
        }

        if (this.AlwaysReasoning == false ||
            this.OptionalReasoning == true ||
            this.ReasoningByDefault == true)
            capabilities.Remove(Capability.ALWAYS_REASONING);

        if (this.OptionalReasoning == false)
        {
            capabilities.Remove(Capability.REASONING_BY_DEFAULT);
            return;
        }

        if (this.ReasoningByDefault == true && !capabilities.Contains(Capability.OPTIONAL_REASONING))
            capabilities.Add(Capability.OPTIONAL_REASONING);

        if (!capabilities.Contains(Capability.OPTIONAL_REASONING))
            capabilities.Remove(Capability.REASONING_BY_DEFAULT);
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

            if (!TryParseSupportedCapability(keyText, out var capability))
            {
                logger.LogWarning("The configured provider {ProviderIndex} contains an unsupported capability override '{CapabilityKey}'. The entry will be ignored. (Plugin ID: {PluginId})", idx, keyText, configPluginId);
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

    private static bool TryParseSupportedCapability(string capabilityKey, out Capability capability)
    {
        capability = Capability.NONE;
        if (!Enum.TryParse(capabilityKey, true, out capability))
            return false;

        return SUPPORTED_CAPABILITIES.Contains(capability);
    }
}
