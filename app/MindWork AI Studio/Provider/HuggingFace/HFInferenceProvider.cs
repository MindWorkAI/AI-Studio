namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// Enum for inference providers that Hugging Face supports.
/// </summary>
public enum HFInferenceProvider
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