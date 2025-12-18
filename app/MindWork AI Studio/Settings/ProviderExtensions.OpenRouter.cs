using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    public static List<Capability> GetModelCapabilitiesOpenRouter(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();

        //
        // OpenRouter model IDs follow the pattern: "provider/model-name"
        // Examples:
        // - openai/gpt-4o
        // - anthropic/claude-3-5-sonnet
        // - google/gemini-pro-1.5
        // - meta-llama/llama-3.1-405b-instruct
        //
        // We need to detect capabilities based on both provider and model name.
        //

        //
        // OpenAI models via OpenRouter:
        //
        if (modelName.IndexOf("openai/") is not -1)
        {
            // Reasoning models (o1, o3, o4 series)
            if (modelName.IndexOf("/o1") is not -1 ||
                modelName.IndexOf("/o3") is not -1 ||
                modelName.IndexOf("/o4") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // GPT-4o and GPT-5 series with multimodal
            if (modelName.IndexOf("/gpt-4o") is not -1 ||
                modelName.IndexOf("/gpt-5") is not -1 ||
                modelName.IndexOf("/chatgpt-4o") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Standard GPT-4
            if (modelName.IndexOf("/gpt-4") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // GPT-3.5
            if (modelName.IndexOf("/gpt-3.5") is not -1 ||
                modelName.IndexOf("/gpt-3") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
        }

        //
        // Anthropic models via OpenRouter:
        //
        if (modelName.IndexOf("anthropic/") is not -1)
        {
            // Claude 3.5 and newer with vision
            if (modelName.IndexOf("/claude-3.5") is not -1 ||
                modelName.IndexOf("/claude-3-5") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Claude 3 Opus/Sonnet with vision
            if (modelName.IndexOf("/claude-3-opus") is not -1 ||
                modelName.IndexOf("/claude-3-sonnet") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Other Claude 3 models
            if (modelName.IndexOf("/claude-3") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];
        }

        //
        // Google models via OpenRouter:
        //
        if (modelName.IndexOf("google/") is not -1)
        {
            // Gemini models with multimodal
            if (modelName.IndexOf("/gemini") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];
        }

        //
        // xAI Grok models via OpenRouter:
        //
        if (modelName.IndexOf("x-ai/") is not -1 || modelName.IndexOf("/grok") is not -1)
        {
            if (modelName.IndexOf("-vision") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];

            return [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // DeepSeek models via OpenRouter:
        //
        if (modelName.IndexOf("/deepseek") is not -1)
        {
            if (modelName.IndexOf("-r1") is not -1 || modelName.IndexOf(" r1") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Mistral models via OpenRouter:
        //
        if (modelName.IndexOf("/mistral") is not -1 || modelName.IndexOf("/pixtral") is not -1)
        {
            if (modelName.IndexOf("/pixtral") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.FUNCTION_CALLING,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Meta Llama models via OpenRouter:
        //
        if (modelName.IndexOf("/llama") is not -1)
        {
            // Llama 4 with vision
            if (modelName.IndexOf("/llama-4") is not -1 ||
                modelName.IndexOf("/llama4") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Vision models
            if (modelName.IndexOf("-vision") is not -1)
                return [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Llama 3.1+ with function calling
            if (modelName.IndexOf("/llama-3.") is not -1 ||
                modelName.IndexOf("/llama3.") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.FUNCTION_CALLING,
                    Capability.CHAT_COMPLETION_API,
                ];

            // Default Llama
            return [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Qwen models via OpenRouter:
        //
        if (modelName.IndexOf("/qwen") is not -1 || modelName.IndexOf("/qwq") is not -1)
        {
            if (modelName.IndexOf("/qwq") is not -1)
                return [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    Capability.ALWAYS_REASONING,
                    Capability.CHAT_COMPLETION_API,
                ];

            return [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
                Capability.CHAT_COMPLETION_API,
            ];
        }

        //
        // Default for unknown models:
        // Assume basic text input/output with chat completion
        //
        return [
            Capability.TEXT_INPUT,
            Capability.TEXT_OUTPUT,
            Capability.CHAT_COMPLETION_API,
        ];
    }
}
