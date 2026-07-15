using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesGoogle(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();

        if (modelName.IndexOf("gemini-") is not -1)
        {
            // Chat-compatible Gemini 3.x reasoning models:
            if (modelName is "gemini-3.5-flash" ||
                modelName is "gemini-flash-latest" ||
                modelName is "gemini-3.1-flash-lite" ||
                modelName is "gemini-3-flash-preview" ||
                modelName is "gemini-pro-latest" ||
                modelName is "gemini-3.1-pro-preview" ||
                modelName is "gemini-3.1-pro-preview-customtools")
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.AUDIO_INPUT,
                    Capability.SPEECH_INPUT, Capability.VIDEO_INPUT,

                    Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Gemini 2.5 Flash Lite supports thinking, but the default is off:
            if (modelName.IndexOf("gemini-2.5-flash-lite") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.AUDIO_INPUT,
                    Capability.SPEECH_INPUT, Capability.VIDEO_INPUT,

                    Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

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