using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesOpenSource(Model model)
    {
        var modelName = NormalizeModelId(model.Id).AsSpan();
        
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
        // - Ollama: llama3.1:405b
        //
        // The name arrives here already normalized by NormalizeModelId: lowercase, with every
        // separator written as a single hyphen. That is why the checks below no longer carry a
        // variant with a space or a colon. What normalization cannot do is insert a separator
        // where a provider left it out, or remove one where it added it, so a family which is
        // written both as "llama3" and as "llama-3" still needs both spellings.
        //

        //
        // Some providers serve the models of the big vendors under their plain names, without the
        // "vendor/model" prefix a gateway would put in front. GWDG is the case which brought this
        // up: next to the open weights it hosts, it resells Claude and GPT models and names them
        // the way their vendor does. A freely chosen LiteLLM alias and a self-hosted proxy can do
        // the same. Without this, all of them would be judged by the rules for open weights, which
        // know none of them, and would lose tool calling, vision, and reasoning alike.
        //
        // Only vendors whose rules do not lead back here may be asked. Mistral and DeepSeek fall
        // back to this function themselves, so delegating to them would loop.
        //
        // Whatever comes back is normalized the way a gateway's answer is: a provider reselling a
        // model serves it through its own OpenAI-compatible chat completion API, never through the
        // Responses API of the vendor it bought the model from.
        //
        if (modelName.StartsWith("claude-") || modelName.IndexOf("-claude-") is not -1)
            return NormalizeForGateway(GetModelCapabilitiesAnthropic(model));

        if (modelName.StartsWith("gemini-") || modelName.IndexOf("-gemini-") is not -1)
            return NormalizeForGateway(GetModelCapabilitiesGoogle(model));

        if (IsOpenAICloudModelName(modelName))
            return NormalizeForGateway(GetModelCapabilitiesOpenAI(model));

        //
        // Base checkpoints, whatever family they come from. They were never instruction-tuned:
        // they continue a text instead of answering, and they know neither a chat template nor
        // tools. This is checked before any family, because otherwise each of them would have to
        // repeat it, and because the default at the end of this function assumes tool calling.
        //
        // The name part has to be exactly "base", so that a model whose name merely contains the
        // word, as in "based", is left alone.
        //
        if (modelName.EndsWith("-base") || modelName.IndexOf("-base-") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // DeepSeek models. This block has to come before the Llama one: the R1 distills are Llama
        // and Qwen checkpoints, and a name such as deepseek-r1-distill-llama-70b would otherwise
        // be read as a plain Llama and lose its reasoning.
        //
        if (modelName.IndexOf("deepseek") is not -1)
        {
            //
            // The distills are Llama and Qwen checkpoints fine-tuned on R1 answers. They reason,
            // but they kept the chat template of the model they were built from, so none of the
            // tool calling R1 itself was trained for survived. They are checked first because
            // they carry "r1" in their name and would match the rule for it below:
            //
            if (modelName.IndexOf("distill") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // The V4 generation, which also covers the point releases such as V4.1, and the
            // experimental checkpoint which takes images:
            if (modelName.IndexOf("deepseek-v4") is not -1)
            {
                if (modelName.IndexOf("vision") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.TEXT_OUTPUT,
                        Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            if(modelName.IndexOf("deepseek-r1") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            //
            // From V3.1 on, the model has a thinking mode which the request turns on; V3.2 added
            // tool calling inside that mode. The gateways write these two either as "deepseek-v3.1"
            // or as "deepseek-chat-v3.1", so the version alone is what we look for:
            //
            if (modelName.IndexOf("v3.1") is not -1 ||
                modelName.IndexOf("v3.2") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // The rest of the V3 line answers directly and calls functions:
            if (modelName.IndexOf("v3") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Meta llama models:
        //
        if (modelName.IndexOf("llama") is not -1)
        {
            if (modelName.IndexOf("llama4") is not -1 ||
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
        // Qwen models:
        //
        if (modelName.IndexOf("qwen") is not -1 || modelName.IndexOf("qwq") is not -1)
        {
            //
            // QwQ has no tool calling. That is worth stating, because Alibaba serves a model of
            // the same family name: qwq-plus is a commercial thinking-only model of theirs, while
            // QwQ-32B here is the open-weight one built on Qwen2.5. They are two different models,
            // and neither the model card of the open weights nor Alibaba's list of models which
            // call functions mentions either of them.
            //
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

            //
            // Check for the multimodal Qwen 3.8 27B checkpoint. Blablador writes this one in two
            // further ways, which no normalization can turn into the canonical name: it separates
            // the family from the version ("Qwen 3.8-27B with DFlash on haicluster"), and its short
            // alias drops the dot ("alias-qwen38-27b").
            //
            if(modelName.IndexOf("qwen3.8-27b") is not -1 ||
               modelName.IndexOf("qwen-3.8-27b") is not -1 ||
               modelName.IndexOf("qwen38-27b") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            //
            // Any other Qwen 3.8 checkpoint. The three checks above all need a size or a variant
            // in the name, which the rolling tags do not carry: Ollama serves the 27B checkpoint
            // as "qwen3.8:latest". Without this, such a name would fall through to the generic
            // Qwen rule and lose everything the family can do. The 27B checkpoint is what the
            // rolling tag points to, so it decides what this tier promises.
            //
            if(modelName.IndexOf("qwen3.8") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
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

            //
            // Every other Qwen. The whole line calls functions, from Qwen 2.5 on, and the Coder
            // checkpoints are built for exactly that. Reasoning is not promised here: the older
            // generations have none, and which of the newer ones think by default differs per
            // checkpoint, so the rules above name them one by one.
            //
            return [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.FUNCTION_CALLING,
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
        // The vision checkpoint reasons, but it is the one Kimi model no vendor lists among those
        // which call functions, so it does not get that ability here:
        //
        if (modelName.IndexOf("kimi-vl") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // The rest of the Kimi line. Moonshot builds these for agentic work, and the K2 model card
        // says so plainly: pass the tools with the request and the model decides on its own when
        // to call them. The thinking variants say what they are in their name; the others answer
        // directly. All of them take text only.
        //
        if (modelName.IndexOf("kimi") is not -1 || modelName.IndexOf("moonshot") is not -1)
        {
            if (modelName.IndexOf("thinking") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }
        
        //
        // Tencent Hunyuan models. Hy3 answers directly by default: its reasoning_effort
        // parameter defaults to no_think, low and high must be requested. We also match
        // the short name because providers offer the model as tencent/hy3, so checking
        // the start of the name is not enough.
        //
        if (modelName.IndexOf("hunyuan") is not -1 ||
            modelName.IndexOf("hy3") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // Ministral models. They need their own block because their names do not contain
        // "mistral" as a substring, so the block below never sees them. Only Ministral 3 accepts
        // images, the 2024 models are text only, which is why the release date decides here too:
        //
        if (modelName.IndexOf("ministral") is not -1)
            return BuildMistralCapabilities(GetMistralReleaseDate(modelName, MINISTRAL_LATEST), MINISTRAL_VISION_SINCE, MISTRAL_REASONING_NEVER);

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
        
            // One member of the 4.20 line answers without thinking, and it says so in its
            // name. It has to be asked about before the general Grok 4 rule, which would
            // otherwise claim the opposite of what the name states:
            if(modelName.IndexOf("-non-reasoning") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Grok 4 models take text and images. Reasoning is always on, only the
            // reasoning effort can be configured. Video is not among their modalities:
            // xAI serves audio, image, and video through models and APIs of their own,
            // and the model pages of the 4.x line say "text, image" and nothing else:
            if(modelName.IndexOf("grok-4") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
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

            // Any other Grok model. Without this, unknown Grok versions would fall
            // through to the global default and would lose function calling:
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }
        
        //
        // The open-weight models of OpenAI. Everything else named after an OpenAI model, the
        // gpt-3.5 aliases included, was handed to their rules at the top of this function, which
        // is why only gpt-oss is left here.
        //
        if (modelName.IndexOf("gpt-oss") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.WEB_SEARCH,
                Capability.CHAT_COMPLETION_API,
            ];
        
        //
        // NVIDIA Nemotron models. They are built for agentic workloads and are text
        // only. The check also covers the quantized checkpoints such as
        // NVIDIA-Nemotron-3.5-Lightning-30B-A3B-NVFP4.
        //
        if (modelName.IndexOf("nemotron") is not -1)
        {
            // The third generation thinks unless the request says otherwise, through
            // enable_thinking=False:
            if (modelName.IndexOf("nemotron-3") is not -1)
                return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // The earlier ones have to be asked to think:
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Google Gemma models. Gemma is the open-weights family, while Gemini is not, which is why
        // Gemma is handled here and Gemini in the Google implementation.
        //
        if (modelName.IndexOf("gemma") is not -1)
        {
            //
            // Every checkpoint of the Gemma 4 generation is multimodal; there is no text-only
            // variant. Audio input is limited to the E2B, E4B, and 12B checkpoints. Video is not
            // a modality of any of them: the model card lists text, image, and audio, and mentions
            // video only as a sequence of frames somebody else has to cut it into. The models can
            // think, but only when the request asks them to, by putting a think token at the start
            // of the system prompt.
            //
            // Gemma 4 is also the first generation with tool calling of its own, with tool tokens
            // in its chat template. The generations below have none.
            //
            if (modelName.IndexOf("gemma-4") is not -1 ||
                modelName.IndexOf("gemma4") is not -1)
            {
                if (modelName.IndexOf("e2b") is not -1 ||
                    modelName.IndexOf("e4b") is not -1 ||
                    modelName.IndexOf("12b") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.AUDIO_INPUT,
                        Capability.TEXT_OUTPUT,

                        Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            //
            // Gemma 3 accepts images from the 4B checkpoint upwards; the 1B one is text-only, and
            // the 3n checkpoints take audio on top. This generation does not reason.
            //
            // It does not call functions either. What Google documents for Gemma 3 is writing the
            // tool descriptions into the prompt by hand, which is a different thing from what an
            // OpenAI-compatible tools field does: the chat template has neither a tool role nor
            // tool tokens, and Ollama refuses a request carrying tools for these models. Native
            // tool calling starts with Gemma 4 above.
            //
            // The check for the small checkpoint looks for "-1b" rather than "1b", so that a name
            // such as gemma-3-31b does not match it.
            //
            if (modelName.IndexOf("gemma-3") is not -1 ||
                modelName.IndexOf("gemma3") is not -1)
            {
                if (modelName.IndexOf("-1b") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                        Capability.CHAT_COMPLETION_API,
                    ];

                if (modelName.IndexOf("gemma-3n") is not -1 ||
                    modelName.IndexOf("gemma3n") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.AUDIO_INPUT,
                        Capability.TEXT_OUTPUT,
                        Capability.CHAT_COMPLETION_API,
                    ];

                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            //
            // The earlier generations take text only and were not built for tool usage:
            //
            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Z AI / GLM models:
        //
        if (modelName.IndexOf("glm") is not -1)
        {
            //
            // Both version checks below accept a hyphen as the version separator as well:
            // Mistral serves these models as glm-5-2 and zai-glm-5-2, while everybody else
            // writes the version with a dot.
            //

            // GLM 5.3 uses forced thinking: the reasoning effort can be lowered, but
            // reasoning cannot be turned off. This check must stay in front of the
            // vision check below, because quantized builds such as GLM-5.3-Flash-NVFP4
            // contain a "v" and would be misread as a vision model:
            if (modelName.IndexOf("glm-5.3") is not -1 ||
                modelName.IndexOf("glm-5-3") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("glm-5.2") is not -1 ||
                modelName.IndexOf("glm-5-2") is not -1)
                return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if(IsGlmVisionModelName(modelName))
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
        
        //
        // MiniMax models. The M line is built for agentic work and thinks between its tool calls,
        // which MiniMax calls interleaved thinking: the reasoning is part of the answer rather
        // than something the request switches on. The older Text-01 answers directly.
        //
        if (modelName.IndexOf("minimax") is not -1)
        {
            if (modelName.IndexOf("minimax-m") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // IBM Granite models. The instruct line calls functions using the OpenAI function
        // definition schema. From 4.2 on they think unless the request says otherwise; 3.2 and 3.3
        // have a thinking toggle which starts off, and the generations between them do not reason
        // at all. For the vision checkpoints, tool calling is not documented.
        //
        if (modelName.IndexOf("granite") is not -1)
        {
            if (modelName.IndexOf("vision") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("granite-4.2") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("granite-3.2") is not -1 ||
                modelName.IndexOf("granite-3.3") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Cohere Command models. Most of the line calls functions, in one step and in several.
        // Command A Vision is the exception Cohere states outright: tool use is not supported
        // with it.
        //
        if (modelName.IndexOf("command-a") is not -1 ||
            modelName.IndexOf("command-r") is not -1)
        {
            if (modelName.IndexOf("command-a-vision") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Command A+ sees and thinks unless the request disables thinking:
            if (modelName.IndexOf("command-a-plus") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("command-a-reasoning") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // The Aya models come from Cohere as well, but they were not trained with tool use in
        // mind, which their documentation says in as many words:
        //
        if (modelName.IndexOf("aya-expanse") is not -1 ||
            modelName.IndexOf("aya-vision") is not -1)
        {
            if (modelName.IndexOf("aya-vision") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // AI2 OLMo models. The instruct checkpoints of the third generation carry a functions
        // section in their chat template, and their default system prompt calls the model a
        // function-calling assistant. The Think variants reason on top of that. OLMo 2 has no
        // tool template.
        //
        if (modelName.IndexOf("olmo") is not -1)
        {
            if (modelName.IndexOf("olmo-3") is not -1 || modelName.IndexOf("olmo3") is not -1)
            {
                if (modelName.IndexOf("think") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                        Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // ByteDance Seed-OSS. Trained for agentic work, and it thinks with a budget the request
        // can cap; the thinking itself cannot be turned off.
        //
        if (modelName.IndexOf("seed-oss") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // TII Falcon. The third generation was post-trained on function calls and reports its
        // tool-calling benchmark in its own model card. Falcon-H1 documents no tool template,
        // except for the small checkpoint built for nothing else.
        //
        if (modelName.IndexOf("falcon") is not -1)
        {
            if (modelName.IndexOf("falcon-h1") is not -1 &&
                modelName.IndexOf("tool-calling") is -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // InclusionAI Ling and Ring. Both call functions natively; Ring is the thinking line of
        // the two, Ling the one which answers directly. Their names are only accepted where a
        // name part begins, because "ling" also sits inside unrelated models such as Starling.
        //
        if (modelName.IndexOf("inclusionai") is not -1 ||
            modelName.StartsWith("ling-") || modelName.IndexOf("-ling-") is not -1 ||
            modelName.StartsWith("ring-") || modelName.IndexOf("-ring-") is not -1)
        {
            if (modelName.StartsWith("ring-") || modelName.IndexOf("-ring-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.REASONING_BY_DEFAULT, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Baidu ERNIE. The thinking checkpoints call functions. The vision ones run in a thinking
        // and a non-thinking mode, and tool calling is not documented for them.
        //
        if (modelName.IndexOf("ernie") is not -1)
        {
            if (modelName.IndexOf("-vl") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            if (modelName.IndexOf("thinking") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Hugging Face SmolLM. The third generation ships a chat template with tool support and a
        // thinking mode the request switches on. The earlier ones have neither.
        //
        if (modelName.IndexOf("smollm") is not -1)
        {
            if (modelName.IndexOf("smollm3") is not -1 || modelName.IndexOf("smollm-3") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // ServiceNow Apriel. The Thinker models see and always reason, because their default chat
        // template opens the thinking channel. Tool tokens arrived with 1.6; 1.5 has none.
        //
        if (modelName.IndexOf("apriel") is not -1)
        {
            if (modelName.IndexOf("apriel-1.5") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // InternLM, and the InternVL family next to it. InternLM has a role of its own for tool
        // answers in its chat template and a deep-thinking mode the request asks for. What is
        // documented for InternVL is that it takes images.
        //
        if (modelName.IndexOf("internvl") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        if (modelName.IndexOf("internlm") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // Swiss AI Apertus 1.5. It calls functions in the OpenAI format, takes images and audio,
        // and thinks when asked to. Note that its tool calling does not work while it thinks --
        // a combination these capabilities cannot express, so both are stated side by side.
        //
        if (modelName.IndexOf("apertus-v1.5") is not -1 ||
            modelName.IndexOf("apertus-1.5") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.AUDIO_INPUT,
                Capability.TEXT_OUTPUT,

                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // Microsoft Phi. The mini and multimodal checkpoints of the fourth generation call
        // functions with tool tokens of their own. The 14B model has no tool role in its chat
        // template at all, and neither do the reasoning checkpoints, which always think.
        //
        if (modelName.IndexOf("phi-4") is not -1 || modelName.IndexOf("phi4") is not -1)
        {
            if (modelName.IndexOf("multimodal") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.AUDIO_INPUT,
                    Capability.TEXT_OUTPUT,

                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            //
            // The reasoning checkpoints have to be checked before the mini one, because
            // Phi-4-mini-reasoning is both and would otherwise be read as a mini model which
            // does not think:
            //
            if (modelName.IndexOf("reasoning") is not -1)
            {
                if (modelName.IndexOf("vision") is not -1)
                    return
                    [
                        Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                        Capability.TEXT_OUTPUT,

                        Capability.ALWAYS_REASONING,
                        Capability.CHAT_COMPLETION_API,
                    ];

                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];
            }

            if (modelName.IndexOf("mini") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,

                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // The models we know do not call functions. They have to be named one by one, because the
        // default below assumes that an unknown model does. None of these documents a tool
        // template: the European and Spanish public models, the discontinued Occiglot, and the Yi
        // line, whose open weights speak plain ChatML while only the closed Yi-Large-FC calls
        // functions. Salamandra is the one with a variant built for it, which keeps its ability.
        //
        // The family names are only accepted where a name part begins, so that "yi" does not
        // match every model which happens to contain those two letters.
        //
        if (modelName.IndexOf("teuken") is not -1 ||
            modelName.IndexOf("eurollm") is not -1 ||
            modelName.IndexOf("occiglot") is not -1 ||
            (modelName.IndexOf("salamandra") is not -1 && modelName.IndexOf("-tools") is -1) ||
            modelName.StartsWith("yi-") || modelName.IndexOf("-yi-") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];

        //
        // Default. A model we do not recognize is assumed to call functions, because by now that
        // is what an instruction-tuned model does: every family added here in the last while
        // could do it, and the ones which cannot are the exception listed above. Guessing the
        // other way around was the safer choice while the ability was only shown as an icon, but
        // it stopped being safe once it decides whether tools are offered at all -- a model which
        // can use them would silently never be asked to.
        //
        // Anybody hitting the rare case where this guess is wrong turns tool calling off for that
        // provider in the expert settings, and an organization can do the same for everybody.
        //
        return [
            Capability.TEXT_INPUT, Capability.TEXT_OUTPUT,
            Capability.FUNCTION_CALLING,
            Capability.CHAT_COMPLETION_API,
        ];
    }

    /// <summary>
    /// Checks whether a GLM model is one of the vision models.
    /// </summary>
    /// <remarks>
    /// Z AI marks these by appending a "v" to the version number: glm-4v, glm-4.1v, glm-4.5v.
    /// Looking for a bare "v" anywhere in the name, which is what this used to do, calls every
    /// quantized build a vision model, because "nvfp4" carries one too, and so does the name of
    /// more than one inference provider.
    /// </remarks>
    /// <param name="modelName">The normalized model name.</param>
    /// <returns>True, when the version number is followed by a "v".</returns>
    private static bool IsGlmVisionModelName(ReadOnlySpan<char> modelName)
    {
        for (var index = 1; index < modelName.Length; index++)
            if (modelName[index] is 'v' && char.IsAsciiDigit(modelName[index - 1]))
                return true;

        return false;
    }

    /// <summary>
    /// Checks whether a model is named after one of the models OpenAI serves through its API.
    /// </summary>
    /// <param name="modelName">The normalized model name.</param>
    /// <returns>True, when the name belongs to an OpenAI cloud model.</returns>
    private static bool IsOpenAICloudModelName(ReadOnlySpan<char> modelName)
    {
        //
        // The o-series carries no vendor word at all, which is why it counts only at the very
        // front of the name. Looking for it anywhere would claim open weights which end on the
        // same two characters, such as Marco-o1.
        //
        if (modelName.StartsWith("o1") || modelName.StartsWith("o3") || modelName.StartsWith("o4"))
            return true;

        if (IsVersionedGptName(modelName))
            return true;

        //
        // Providers which answer with a descriptive name carry the model in the middle of it, as
        // in "01 - GPT-5.5 - great overall performance":
        //
        var separatorIndex = modelName.IndexOf("-gpt-");
        return separatorIndex is not -1 && IsVersionedGptName(modelName[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Checks whether a name starts with "gpt-" followed by a version.
    /// </summary>
    /// <remarks>
    /// The digit is what separates the models OpenAI serves from the open weights which borrow
    /// the name: gpt-oss, gpt-neox, and gpt-j are none of theirs.
    /// </remarks>
    /// <param name="modelName">The normalized model name, or a part of it.</param>
    /// <returns>True, when the name starts with a versioned GPT name.</returns>
    private static bool IsVersionedGptName(ReadOnlySpan<char> modelName) =>
        modelName.StartsWith("gpt-") && modelName.Length > 4 && char.IsAsciiDigit(modelName[4]);
}