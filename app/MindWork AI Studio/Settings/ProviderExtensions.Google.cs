using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesGoogle(Model model)
    {
        var modelName = NormalizeModelId(model.Id).AsSpan();

        if (modelName.IndexOf("gemini-") is not -1)
        {
            //
            // Image generation models. They carry a version number like every other model and
            // have to be asked about first, or gemini-3-pro-image would be read as a chat model
            // of the 3.x line and be promised function calling. No image model of the family
            // offers that; what they do offer, and the chat models do not, is writing images.
            //
            if (modelName.IndexOf("-image") is not -1)
            {
                // Of the image models, only the 3.1 Flash ones read video. They think about
                // complex prompts, and, as with the 3.x chat models, thinking cannot be
                // switched off:
                if (modelName.IndexOf("gemini-3.1-flash") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.VIDEO_INPUT,

                        Capability.TEXT_OUTPUT, Capability.IMAGE_OUTPUT,

                        Capability.ALWAYS_REASONING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                // Every other Gemini 3 image model thinks as well, it just does not read video:
                if (modelName.IndexOf("gemini-3") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,

                        Capability.TEXT_OUTPUT, Capability.IMAGE_OUTPUT,

                        Capability.ALWAYS_REASONING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                // The older image models, such as the 2.5 Flash one, do not think:
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,

                    Capability.TEXT_OUTPUT, Capability.IMAGE_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            // Chat-compatible Gemini 3.x reasoning models. We match the entire 3.x line
            // so that new releases are covered as well: they all reason, and the
            // thinking level can only be lowered, never turned off. That holds for the
            // Flash Lite models of this line too, which is what sets them apart from
            // Gemini 2.5 Flash Lite below: there, thinking is off until it is asked for,
            // while here the lowest level still thinks. The two rolling aliases carry no
            // version number and are listed separately:
            if (modelName.IndexOf("gemini-3") is not -1 ||
                modelName is "gemini-flash-latest" ||
                modelName is "gemini-pro-latest")
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
            
            //
            // There used to be a branch here which withheld function calling from the 2.0 Flash
            // models. It said the wrong thing about them, and it only ever caught the dated IDs
            // because it asked for a trailing hyphen: the plain gemini-2.0-flash alias walked
            // past it and got a different answer than gemini-2.0-flash-001, which is the same
            // model. Both questions are moot now, because Google shut the 2.0 Flash chat models
            // down on 1 June 2026. Anything still asking for one of those names gets the default
            // below. The live model above keeps its branch: it belongs to a different API whose
            // retirement Google announces separately.
            //

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