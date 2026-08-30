using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesAnthropic(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();

        // Claude Fable 5 and Mythos 5 always use adaptive thinking:
        if(modelName.StartsWith("claude-fable-5") || modelName.StartsWith("claude-mythos-5"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Claude Opus 5 and Sonnet 5 think adaptively unless thinking is turned off:
        if(modelName.StartsWith("claude-opus-5") || modelName.StartsWith("claude-sonnet-5"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        // Claude Haiku 4.5 needs an explicit thinking budget to reason:
        if(modelName.StartsWith("claude-haiku-4-5"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

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
        
        // Any other model. Every current Claude model accepts images, so we assume the
        // same for models we do not know yet:
        return [
            Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
            Capability.TEXT_OUTPUT,
            Capability.FUNCTION_CALLING,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}