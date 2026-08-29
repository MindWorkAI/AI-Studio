using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesMistral(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();

        //
        // Only the "latest" aliases are specific to Mistral's own API. The models behind them are
        // also served by other providers under their versioned name, and the shared open-source
        // table knows those versions in more detail than the generic prefixes below. Whenever a
        // model carries its version in the name, we hand it over to that table. Without this, the
        // generic prefixes would swallow the newer versions and take away their image input and
        // reasoning:
        //
        if (modelName.IndexOf("mistral-large-3") is not -1 ||
            modelName.IndexOf("mistral-medium-3.5") is not -1 ||
            modelName.IndexOf("mistral-small-4") is not -1)
            return GetModelCapabilitiesOpenSource(model);

        // Pixtral models are able to do process images:
        if (modelName.IndexOf("pixtral") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral large latest:
        if (modelName.IndexOf("mistral-large-latest") is not -1)                 
            return
            [
                Capability.TEXT_INPUT, 
                Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral large:
        if (modelName.IndexOf("mistral-large-") is not -1)
            return
            [
                Capability.TEXT_INPUT, 
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral medium latest:
        if (modelName.IndexOf("mistral-medium-latest") is not -1)           
            return
            [
                Capability.TEXT_INPUT, 
                Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral medium:
        if (modelName.IndexOf("mistral-medium-") is not -1)
            return
            [
                Capability.TEXT_INPUT, 
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral small latest:
        if (modelName.IndexOf("mistral-small-latest") is not -1)        
            return
            [
                Capability.TEXT_INPUT, 
                Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING,
                
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        // Mistral small:
        if (modelName.IndexOf("mistral-small-") is not -1)
            return
            [
                Capability.TEXT_INPUT, 
                Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING,
                
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