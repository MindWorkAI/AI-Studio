namespace AIStudio.Provider.HuggingFace;

public static class HFInferenceProviderExtensions
{
    public static string Endpoints(this HFInferenceProvider provider, Model model) => provider switch
    {
        HFInferenceProvider.CEREBRAS => "cerebras/v1/",
        HFInferenceProvider.NEBIUS_AI_STUDIO => "nebius/v1/",
        HFInferenceProvider.SAMBANOVA => "sambanova/v1/",
        HFInferenceProvider.NOVITA => "novita/v3/openai/",
        HFInferenceProvider.HYPERBOLIC => "hyperbolic/v1/",
        HFInferenceProvider.TOGETHER_AI => "together/v1/",
        HFInferenceProvider.FIREWORKS => "fireworks-ai/inference/v1/",
        HFInferenceProvider.HF_INFERENCE_API => $"hf-inference/models/{model.ToString()}/v1/",
        _ => string.Empty,
    };
    
    public static string EndpointsId(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.CEREBRAS => "cerebras",
        HFInferenceProvider.NEBIUS_AI_STUDIO => "nebius",
        HFInferenceProvider.SAMBANOVA => "sambanova",
        HFInferenceProvider.NOVITA => "novita",
        HFInferenceProvider.HYPERBOLIC => "hyperbolic",
        HFInferenceProvider.TOGETHER_AI => "together",
        HFInferenceProvider.FIREWORKS => "fireworks",
        HFInferenceProvider.HF_INFERENCE_API => "hf-inference",
        _ => string.Empty,
    };
    
    public static string ToName(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.CEREBRAS => "Cerebras",
        HFInferenceProvider.NEBIUS_AI_STUDIO => "Nebius AI Studio",
        HFInferenceProvider.SAMBANOVA => "Sambanova",
        HFInferenceProvider.NOVITA => "Novita",
        HFInferenceProvider.HYPERBOLIC => "Hyperbolic",
        HFInferenceProvider.TOGETHER_AI => "Together AI",
        HFInferenceProvider.FIREWORKS => "Fireworks AI",
        HFInferenceProvider.HF_INFERENCE_API => "Hugging Face Inference API",
        _ => string.Empty,
    };
}