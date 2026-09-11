using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesAlibaba(Model model)
    {
        var modelName = NormalizeModelId(model.Id).AsSpan();
        
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
            
            // Check for Qwen 3.5:
            if(modelName.StartsWith("qwen3.5"))
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Check for Qwen 3.6 family:
            if(modelName.StartsWith("qwen3.6"))
                return
                [
                    Capability.TEXT_INPUT, Capability.VIDEO_INPUT,
                    Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Check for the Qwen 3.8 family:
            if(modelName.StartsWith("qwen3.8"))
            {
                // Flash thinks by default, but thinking can be turned off:
                if(modelName.StartsWith("qwen3.8-flash"))
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.VIDEO_INPUT,
                        Capability.TEXT_OUTPUT,

                        Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                // Unlike the open-weight checkpoint, the Max model keeps its vision
                // capabilities when used through Alibaba Cloud:
                if(modelName.StartsWith("qwen3.8-max"))
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.VIDEO_INPUT,
                        Capability.TEXT_OUTPUT,

                        Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                // All other 3.8 models, such as the 27B one:
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            // Check for the 3.0 VL models:
            if(modelName.IndexOf("-vl-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
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
        }
        
        //
        // QwQ models. What Model Studio serves under this name is qwq-plus, a commercial
        // thinking-only model built on Qwen2.5. It is not the same model as the open-weight
        // QwQ-32B, which the rules for open source models cover; the two only share a family
        // name. Neither of them appears in Alibaba's list of models which call functions, and
        // the model card of the open weights does not mention tools at all, which is why this
        // states no such ability. Anybody who knows better can turn it on in the expert settings.
        //
        if (modelName.StartsWith("qwq"))
        {
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING,
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