using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    public static List<Capability> GetModelCapabilitiesMistral(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // Pixtral models are able to do process images:
        if (modelName.IndexOf("pixtral") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral medium:
        if (modelName.IndexOf("mistral-medium-") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral small:
        if (modelName.IndexOf("mistral-small-") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral saba:
        if (modelName.IndexOf("mistral-saba-") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Default:
        return GetModelCapabilitiesOpenSource(model);
    }
}