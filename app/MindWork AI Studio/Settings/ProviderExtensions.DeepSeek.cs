using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesDeepSeek(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        if(modelName.IndexOf("reasoner") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.ALWAYS_REASONING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        return
        [
            Capability.TEXT_INPUT,
            Capability.TEXT_OUTPUT,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}