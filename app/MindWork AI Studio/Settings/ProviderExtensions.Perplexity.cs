using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesPerplexity(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        if(modelName.IndexOf("reasoning") is not -1 ||
           modelName.IndexOf("deep-research") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.MULTIPLE_IMAGE_INPUT,
                
                Capability.TEXT_OUTPUT,
                Capability.IMAGE_OUTPUT,
                
                Capability.ALWAYS_REASONING,
                Capability.WEB_SEARCH,
                Capability.CHAT_COMPLETION_API,
            ];
        
        return
        [
            Capability.TEXT_INPUT,
            Capability.MULTIPLE_IMAGE_INPUT,
            
            Capability.TEXT_OUTPUT,
            Capability.IMAGE_OUTPUT,
            
            Capability.WEB_SEARCH,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}