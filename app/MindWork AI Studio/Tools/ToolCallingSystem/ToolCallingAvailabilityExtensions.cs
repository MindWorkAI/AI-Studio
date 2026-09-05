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

        var modelCapabilities = provider.GetModelCapabilities();
        var supportsRequiredApis =
            modelCapabilities.Contains(Capability.CHAT_COMPLETION_API) ||
            modelCapabilities.Contains(Capability.RESPONSES_API);

        if (!supportsRequiredApis || !modelCapabilities.Contains(Capability.FUNCTION_CALLING))
            return new(false, TB("Tool calling support is not enabled by default for this model, but you can enable this capability in the expert settings of the provider if you are sure the model supports it."));

        return ToolCallingAvailability.Available();
    }
}
