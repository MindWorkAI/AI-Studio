namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// Enum for instance providers that Hugging Face supports.
/// </summary>
public enum HFInstanceProvider
{
    NONE,
    
    CEREBRAS,
    NEBIUS_AI_STUDIO,
    SAMBANOVA,
    NOVITA,
    HYPERBOLIC,
    TOGETHER_AI,
    FIREWORKS,
    HF_INFERENCE_API,
}