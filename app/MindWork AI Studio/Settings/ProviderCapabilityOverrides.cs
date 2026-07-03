using System.Text;
using System.Text.Json.Serialization;

using AIStudio.Provider;

using Lua;

using Microsoft.Extensions.Logging;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

/// <summary>
/// Optional expert capability overrides for a configured LLM provider.
/// Missing values keep the automatic capability detection result.
/// </summary>
public sealed record ProviderCapabilityOverrides
{
    public static readonly IReadOnlyList<Capability> SUPPORTED_CAPABILITIES =
    [
        Capability.TEXT_INPUT,
        Capability.AUDIO_INPUT,
        Capability.MULTIPLE_IMAGE_INPUT,
        Capability.SPEECH_INPUT,
        Capability.VIDEO_INPUT,
        Capability.ALWAYS_REASONING
    ];

    [JsonPropertyName("TEXT_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TextInput { get; init; }

    [JsonPropertyName("AUDIO_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AudioInput { get; init; }

    [JsonPropertyName("MULTIPLE_IMAGE_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MultipleImageInput { get; init; }

    [JsonPropertyName("SPEECH_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SpeechInput { get; init; }

    [JsonPropertyName("VIDEO_INPUT")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? VideoInput { get; init; }

    [JsonPropertyName("ALWAYS_REASONING")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AlwaysReasoning { get; init; }

    [JsonIgnore]
    public bool HasOverrides =>
        this.TextInput is not null ||
        this.AudioInput is not null ||
        this.MultipleImageInput is not null ||
        this.SpeechInput is not null ||
        this.VideoInput is not null ||
        this.AlwaysReasoning is not null;

    public bool? GetOverride(Capability capability) => capability switch
    {
        Capability.TEXT_INPUT => this.TextInput,
        Capability.AUDIO_INPUT => this.AudioInput,
        Capability.MULTIPLE_IMAGE_INPUT => this.MultipleImageInput,
        Capability.SPEECH_INPUT => this.SpeechInput,
        Capability.VIDEO_INPUT => this.VideoInput,
        Capability.ALWAYS_REASONING => this.AlwaysReasoning,
        _ => null
    };

    public ProviderCapabilityOverrides SetOverride(Capability capability, bool? value) => capability switch
    {
        Capability.TEXT_INPUT => this with { TextInput = value },
        Capability.AUDIO_INPUT => this with { AudioInput = value },
        Capability.MULTIPLE_IMAGE_INPUT => this with { MultipleImageInput = value },
        Capability.SPEECH_INPUT => this with { SpeechInput = value },
        Capability.VIDEO_INPUT => this with { VideoInput = value },
        Capability.ALWAYS_REASONING => this with { AlwaysReasoning = value },
        _ => this
    };

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

        return mergedCapabilities;
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
