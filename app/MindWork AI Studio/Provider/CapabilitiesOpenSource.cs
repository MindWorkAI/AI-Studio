namespace AIStudio.Provider;

public static class CapabilitiesOpenSource
{
    public static IReadOnlyCollection<Capability> GetCapabilities(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        //
        // Checking for names in the case of open source models is a hard task.
        // Let's assume we want to check for the llama 3.1 405b model.
        //
        // Here is a not complete list of how providers name this model:
        // - Fireworks: accounts/fireworks/models/llama-v3p1-405b-instruct
        // - Hugging Face -> Nebius AI Studio: meta-llama/Meta-Llama-3.1-405B-Instruct
        // - Groq: llama-3.1-405b-instruct
        // - LM Studio: llama-3.1-405b-instruct
        // - Helmholtz Blablador: 1 - Llama3 405 the best general model
        // - GWDG: Llama 3.1 405B Instruct
        //
        
        //
        // Meta llama models:
        //
        if (modelName.IndexOf("llama") is not -1)
        {
            if (modelName.IndexOf("llama4") is not -1 ||
                modelName.IndexOf("llama 4") is not -1 ||
                modelName.IndexOf("llama-4") is not -1 ||
                modelName.IndexOf("llama-v4") is not -1)
                return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.TEXT_OUTPUT,
                        
                        Capability.FUNCTION_CALLING,
                    ];

            // The old vision models cannot do function calling:
            if (modelName.IndexOf("vision") is not -1)
                return [Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.TEXT_OUTPUT];
            
            //
            // All models >= 3.1 are able to do function calling:
            //
            if (modelName.IndexOf("llama3.") is not -1 ||
                modelName.IndexOf("llama 3.") is not -1 ||
                modelName.IndexOf("llama-3.") is not -1 ||
                modelName.IndexOf("llama-v3p") is not -1)
                return
                    [
                        Capability.TEXT_INPUT,
                        Capability.TEXT_OUTPUT,
                        
                        Capability.FUNCTION_CALLING,
                    ];

            // All other llama models can only do text input and output:
            return [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT];
        }

        //
        // DeepSeek models:
        //
        if (modelName.IndexOf("deepseek") is not -1)
        {
            if(modelName.IndexOf("deepseek-r1") is not -1 ||
               modelName.IndexOf("deepseek r1") is not -1)
                return [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT, Capability.ALWAYS_REASONING];
            
            return [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT]; 
        }
        
        //
        // Qwen models:
        //
        if (modelName.IndexOf("qwen") is not -1 || modelName.IndexOf("qwq") is not -1)
        {
            if (modelName.IndexOf("qwq") is not -1)
                return [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT, Capability.ALWAYS_REASONING];
            
            return [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT];
        }
        
        //
        // Mistral models:
        //
        if (modelName.IndexOf("mistral") is not -1 ||
            modelName.IndexOf("pixtral") is not -1)
        {
            if(modelName.IndexOf("pixtral") is not -1)
                return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.TEXT_OUTPUT,
                        Capability.FUNCTION_CALLING
                    ];
            
            if (modelName.IndexOf("3.1") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING
                ];
            
            // Default:
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING
                ];
        }
        
        //
        // Grok models:
        //
        if (modelName.IndexOf("grok") is not -1)
        {
            if(modelName.IndexOf("-vision-") is not -1)
                return 
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                ];
        
            if(modelName.StartsWith("grok-3-mini"))
                return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                ];
        
            if(modelName.StartsWith("grok-3"))
                return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                ];
        }
        
        // Default:
        return [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT];
    }
}