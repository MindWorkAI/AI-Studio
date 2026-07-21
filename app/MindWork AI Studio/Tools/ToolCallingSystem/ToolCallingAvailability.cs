using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public readonly record struct ToolCallingAvailability(bool IsAvailable, string Message)
{
    public static ToolCallingAvailability Available() => new(true, string.Empty);
}

public static class ToolCallingAvailabilityExtensions
{
    public static ToolCallingAvailability GetToolCallingAvailability(this AIStudio.Settings.Provider provider)
    {
        if (provider == AIStudio.Settings.Provider.NONE || provider.UsedLLMProvider is LLMProviders.NONE)
            return new(false, I18N.I.T("Please select an LLM provider.", typeof(ToolCallingAvailabilityExtensions).Namespace, nameof(ToolCallingAvailabilityExtensions)));

        if (provider.UsedLLMProvider is LLMProviders.ANTHROPIC)
            return new(false, I18N.I.T("Tool calling for this provider is not implemented yet.", typeof(ToolCallingAvailabilityExtensions).Namespace, nameof(ToolCallingAvailabilityExtensions)));

        var modelCapabilities = provider.GetModelCapabilities();
        var supportsRequiredApis =
            modelCapabilities.Contains(Capability.CHAT_COMPLETION_API) ||
            modelCapabilities.Contains(Capability.RESPONSES_API);

        if (!supportsRequiredApis || !modelCapabilities.Contains(Capability.FUNCTION_CALLING))
            return new(false, I18N.I.T("The selected model does not support tool calling.", typeof(ToolCallingAvailabilityExtensions).Namespace, nameof(ToolCallingAvailabilityExtensions)));

        return ToolCallingAvailability.Available();
    }
}
