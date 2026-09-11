using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesOpenAI(Model model)
    {
        var modelName = NormalizeModelId(model.Id).AsSpan();
        
        if (modelName is "gpt-4o-search-preview")
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.WEB_SEARCH,
                    Capability.CHAT_COMPLETION_API,
                ];
        
        if (modelName is "gpt-4o-mini-search-preview")
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.WEB_SEARCH,
                    Capability.CHAT_COMPLETION_API,
                ];
        
        if (modelName.StartsWith("o1-mini"))
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];
        
        if(modelName is "gpt-3.5-turbo")
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.RESPONSES_API,
            ];
        
        if(modelName.StartsWith("gpt-3.5"))
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];

        if (modelName.StartsWith("o3-mini"))
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.RESPONSES_API,
                ];
        
        if (modelName.StartsWith("o4-mini") || modelName.StartsWith("o3"))
            return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.WEB_SEARCH,
                    Capability.RESPONSES_API,
                ];
        
        if (modelName.StartsWith("o1"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                    
                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.RESPONSES_API,
            ];
        
        if(modelName.StartsWith("gpt-4-turbo"))
            return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.RESPONSES_API,
                ];
        
        if(modelName is "gpt-4" || modelName.StartsWith("gpt-4-"))
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.RESPONSES_API,
                ];
        
        if(modelName.StartsWith("gpt-5-nano"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING, Capability.ALWAYS_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API,
            ];
        
        if(modelName is "gpt-5" || modelName.StartsWith("gpt-5-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING, Capability.ALWAYS_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API,
            ];
        
        //
        // None of the GPT-5 models writes images itself. They can ask for one through the
        // image generation tool, which is a tool call like any other and produces a picture
        // from a separate model. That is a different thing from an output modality, and we
        // must not report it as one: the chat would then offer to receive images which never
        // arrive.
        //
        if(modelName is "gpt-5.1" || modelName.StartsWith("gpt-5.1-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING, Capability.OPTIONAL_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];
        
        if(modelName is "gpt-5.2" || modelName.StartsWith("gpt-5.2-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING, Capability.OPTIONAL_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];
        
        if(modelName is "gpt-5.3" || modelName.StartsWith("gpt-5.3-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING, Capability.OPTIONAL_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];
        
        if(modelName is "gpt-5.4" || modelName.StartsWith("gpt-5.4-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING, Capability.OPTIONAL_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];

        if(modelName is "gpt-5.5" || modelName.StartsWith("gpt-5.5-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING, Capability.REASONING_BY_DEFAULT,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];

        if(modelName is "gpt-5.6" || modelName.StartsWith("gpt-5.6-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING, Capability.REASONING_BY_DEFAULT,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];

        //
        // GPT-6 Astra. Unlike the 5.5 and 5.6 models, it reasons on every request: the effort
        // reaches from low to max, and there is no setting which switches thinking off.
        //
        if(modelName is "gpt-6-astra" || modelName.StartsWith("gpt-6-astra-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING, Capability.ALWAYS_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];

        return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.RESPONSES_API,
                Capability.WEB_SEARCH,
            ];
    }
}