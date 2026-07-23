using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem;

public static class ToolCallingAvailabilityExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ToolCallingAvailabilityExtensions).Namespace, nameof(ToolCallingAvailabilityExtensions));

    public static ToolCallingAvailability GetToolCallingAvailability(this AIStudio.Settings.Provider provider)
    {
        if (provider == AIStudio.Settings.Provider.NONE || provider.UsedLLMProvider is LLMProviders.NONE)
            return new(false, TB("Please select an LLM provider."));

        if (provider.UsedLLMProvider is LLMProviders.ANTHROPIC)
            return new(false, TB("Tool calling for this provider is not implemented yet."));

        var modelCapabilities = provider.GetModelCapabilities();
        var supportsRequiredApis =
            modelCapabilities.Contains(Capability.CHAT_COMPLETION_API) ||
            modelCapabilities.Contains(Capability.RESPONSES_API);

        if (!supportsRequiredApis || !modelCapabilities.Contains(Capability.FUNCTION_CALLING))
            return new(false, TB("The selected model does not support tool calling."));

        return ToolCallingAvailability.Available();
    }
}
