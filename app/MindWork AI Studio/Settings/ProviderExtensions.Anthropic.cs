using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    public static List<Capability> GetModelCapabilitiesAnthropic(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // Claude 4.x models:
        if(modelName.StartsWith("claude-opus-4") || modelName.StartsWith("claude-sonnet-4"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Claude 3.7 is able to do reasoning:
        if(modelName.StartsWith("claude-3-7"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // All other 3.x models are able to process text and images as input:
        if(modelName.StartsWith("claude-3-"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Any other model is able to process text only:
        return [
            Capability.TEXT_INPUT,
            Capability.TEXT_OUTPUT,
            Capability.FUNCTION_CALLING,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}