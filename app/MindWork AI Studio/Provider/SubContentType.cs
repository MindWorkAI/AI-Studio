namespace AIStudio.Provider;

/// <summary>
/// Sub content types for OpenAI-compatible API interactions when using multimodal messages.
/// </summary>
public enum SubContentType
{
    /// <summary>
    /// Default type for user prompts in multimodal messages. This type is supported across all providers.
    /// </summary>
    TEXT,
    
    /// <summary>
    /// Right now only supported by OpenAI and it's responses API. Even other providers that support multimodal messages
    /// and the responses API do not support this type. They use TEXT instead.
    /// </summary>
    INPUT_TEXT,
    
    /// <summary>
    /// Right now only supported by OpenAI and it's responses API. Even other providers that support multimodal messages
    /// and the responses API do not support this type. They use IMAGE_URL instead.
    /// </summary>
    INPUT_IMAGE,
    
    /// <summary>
    /// Default type for images in multimodal messages. This type is supported across all providers.
    /// </summary>
    IMAGE_URL,
    
    /// <summary>
    /// The image type is used exclusively by Anthropic's messages API.
    /// </summary>
    IMAGE,
    
    /// <summary>
    /// Right now only supported by OpenAI (responses & chat completion API), Google (chat completions API), and Mistral (chat completions API).
    /// </summary>
    INPUT_AUDIO,
}