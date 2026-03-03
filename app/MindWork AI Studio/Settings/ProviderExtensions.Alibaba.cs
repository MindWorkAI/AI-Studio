using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesAlibaba(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // Qwen models:
        if (modelName.StartsWith("qwen"))
        {
            // Check for omni models:
            if (modelName.IndexOf("omni") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.AUDIO_INPUT, Capability.SPEECH_INPUT,
                    Capability.VIDEO_INPUT,

                    Capability.TEXT_OUTPUT, Capability.SPEECH_OUTPUT,
                    
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Check for Qwen 3:
            if(modelName.StartsWith("qwen3"))
                return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if(modelName.IndexOf("-vl-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.CHAT_COMPLETION_API,
                ];
        }
        
        // QwQ models:
        if (modelName.StartsWith("qwq"))
        {
            return
            [
                Capability.TEXT_INPUT, 
                Capability.TEXT_OUTPUT,
                
                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }
        
        // QVQ models:
        if (modelName.StartsWith("qvq"))
        {
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.ALWAYS_REASONING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        // Default to text input and output:
        return
        [
            Capability.TEXT_INPUT,
            Capability.TEXT_OUTPUT,
            
            Capability.FUNCTION_CALLING,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}