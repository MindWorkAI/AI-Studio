using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesDeepSeek(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // The reasoner alias points to the thinking mode of the current flash model:
        if(modelName.IndexOf("reasoner") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        // The chat alias points to the non-thinking mode of the same model:
        if(modelName.IndexOf("chat") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        // DeepSeek publishes its models as open weights and offers them under the same
        // names here. Instead of maintaining a second copy of those rules, we reuse the
        // ones for open source models:
        return GetModelCapabilitiesOpenSource(model);
    }
}