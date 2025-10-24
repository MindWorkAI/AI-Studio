using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    public static List<Capability> GetModelCapabilitiesGoogle(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();

        if (modelName.IndexOf("gemini-") is not -1)
        {
            // Reasoning models:
            if (modelName.IndexOf("gemini-2.5") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.AUDIO_INPUT,
                    Capability.SPEECH_INPUT, Capability.VIDEO_INPUT,
                    
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Image generation:
            if(modelName.IndexOf("-2.0-flash-preview-image-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.AUDIO_INPUT,
                    Capability.SPEECH_INPUT, Capability.VIDEO_INPUT,
                    
                    Capability.TEXT_OUTPUT, Capability.IMAGE_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Realtime model:
            if(modelName.IndexOf("-2.0-flash-live-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.AUDIO_INPUT, Capability.SPEECH_INPUT,
                    Capability.VIDEO_INPUT,
                    
                    Capability.TEXT_OUTPUT, Capability.SPEECH_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // The 2.0 flash models cannot call functions:
            if(modelName.IndexOf("-2.0-flash-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.AUDIO_INPUT,
                    Capability.SPEECH_INPUT, Capability.VIDEO_INPUT,
                    
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // The old 1.0 pro vision model:
            if(modelName.IndexOf("pro-vision") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Default to all other Gemini models:
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.AUDIO_INPUT,
                Capability.SPEECH_INPUT, Capability.VIDEO_INPUT,
                
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }
        
        // Default for all other models:
        return
        [
            Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
            
            Capability.TEXT_OUTPUT,
            
            Capability.FUNCTION_CALLING,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}