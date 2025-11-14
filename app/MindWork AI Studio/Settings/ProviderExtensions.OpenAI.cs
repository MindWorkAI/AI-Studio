using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    public static List<Capability> GetModelCapabilitiesOpenAI(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
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

        if (modelName.StartsWith("chatgpt-4o-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.RESPONSES_API,
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
        
        if(modelName is "gpt-5.1" || modelName.StartsWith("gpt-5.1-"))
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT, Capability.IMAGE_OUTPUT,
                
                Capability.FUNCTION_CALLING, Capability.OPTIONAL_REASONING,
                Capability.WEB_SEARCH,
                Capability.RESPONSES_API, Capability.CHAT_COMPLETION_API,
            ];
        
        return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.RESPONSES_API,
            ];
    }
}