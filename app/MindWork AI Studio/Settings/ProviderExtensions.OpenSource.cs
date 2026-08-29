using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesOpenSource(Model model)
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
                        Capability.CHAT_COMPLETION_API,
                    ];

            // The old vision models cannot do function calling:
            if (modelName.IndexOf("vision") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
            
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
                        Capability.CHAT_COMPLETION_API,
                    ];

            // All other llama models can only do text input and output:
            return [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Meta Muse models. They need their own block because their names do not
        // contain "llama". Muse Glimmer always reasons: its chat template opens the
        // thinking channel unconditionally, only the reasoning strength can be lowered.
        //
        if (modelName.IndexOf("muse-glimmer") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // DeepSeek models:
        //
        if (modelName.IndexOf("deepseek") is not -1)
        {
            if ((modelName.IndexOf("deepseek-v4-flash") is not -1 ||
                 modelName.IndexOf("deepseek-v4-pro") is not -1) &&
                modelName.IndexOf("-base") is -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if(modelName.IndexOf("deepseek-r1") is not -1 ||
               modelName.IndexOf("deepseek r1") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            return [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ]; 
        }
        
        //
        // Qwen models:
        //
        if (modelName.IndexOf("qwen") is not -1 || modelName.IndexOf("qwq") is not -1)
        {
            if (modelName.IndexOf("qwq") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Check for the open-weight Qwen 3.8 checkpoint:
            if(modelName.IndexOf("qwen3.8-2.4t-a95b") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Check for the Qwen 3.8 Flash models. The open weights are published as
            // Flash-Next, while Flash without the suffix is the production model. Both
            // share the same capabilities, so one check covers them:
            if(modelName.IndexOf("qwen3.8-flash") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.VIDEO_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Check for the multimodal Qwen 3.8 27B checkpoint:
            if(modelName.IndexOf("qwen3.8-27b") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.VIDEO_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Check for Qwen 3.5:
            if(modelName.IndexOf("qwen3.5") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Check for Qwen 3.6 family:
            if(modelName.IndexOf("qwen3.6") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if(modelName.IndexOf("-vl-") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            return [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Moonshot AI / Kimi models:
        //
        if (modelName.IndexOf("kimi-k3") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT, Capability.VIDEO_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        if (modelName.IndexOf("kimi-k2.7-code") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        
        //
        // Mistral models:
        //
        if (modelName.IndexOf("mistral") is not -1 ||
            modelName.IndexOf("magistral") is not -1 ||
            modelName.IndexOf("voxtral") is not -1 ||
            modelName.IndexOf("pixtral") is not -1)
        {
            if(modelName.IndexOf("pixtral") is not -1)
                return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.TEXT_OUTPUT,
                        
                        Capability.FUNCTION_CALLING,
                        Capability.CHAT_COMPLETION_API,
                    ];
            
            
            // Mistral medium 3.5:
            if (modelName.IndexOf("mistral-medium-3.5") is not -1)           
                return
                [
                    Capability.TEXT_INPUT, 
                    Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                
                    Capability.OPTIONAL_REASONING,
                
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            
            if (modelName.IndexOf("mistral-3") is not -1 ||
                modelName.IndexOf("mistral-large-3") is not -1)
                return
                [
                    Capability.TEXT_INPUT, 
                    Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                
                    Capability.OPTIONAL_REASONING,
                
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if (modelName.IndexOf("mistral-small-4") is not -1)
                return
                [
                    Capability.TEXT_INPUT, 
                    Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING,
                
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if (modelName.IndexOf("mistral-small-3") is not -1 ||
                modelName.IndexOf("mistral-small-4") is not -1)
                return
                [
                    Capability.TEXT_INPUT, 
                    Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING,
                
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if (modelName.IndexOf("mistral-small-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, 
                    Capability.TEXT_OUTPUT,
                
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if (modelName.IndexOf("voxtral-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.SPEECH_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Magistral models:
            if (modelName.IndexOf("magistral-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                
                    Capability.FUNCTION_CALLING,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if (modelName.IndexOf("3.1") is not -1 ||
                modelName.IndexOf("3.2") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            // Default:
            return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
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
                    Capability.CHAT_COMPLETION_API,
                ];
        
            if(modelName.StartsWith("grok-3-mini"))
                return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
        
            if(modelName.StartsWith("grok-3"))
                return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
        }
        
        //
        // OpenAI models:
        //
        if (modelName.IndexOf("gpt-oss") is not -1 ||
            modelName.IndexOf("gpt-3.5") is not -1)
        {
            if(modelName.IndexOf("gpt-oss") is not -1)
                return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.WEB_SEARCH,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            if(modelName.IndexOf("gpt-3.5") is not -1)
                return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.CHAT_COMPLETION_API,
                ];
        }
        
        //
        // Z AI / GLM models:
        //
        if (modelName.IndexOf("glm") is not -1)
        {
            // GLM 5.3 uses forced thinking: the reasoning effort can be lowered, but
            // reasoning cannot be turned off. This check must stay in front of the
            // vision check below, because quantized builds such as GLM-5.3-Flash-NVFP4
            // contain a "v" and would be misread as a vision model:
            if (modelName.IndexOf("glm-5.3") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("glm-5.2") is not -1)
                return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if(modelName.IndexOf("v") is not -1)
                return 
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.OPTIONAL_REASONING,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("glm-4-") is not -1)
                return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            
            return 
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.FUNCTION_CALLING,
                    Capability.OPTIONAL_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];
        }
        
        // Default:
        return [
            Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}