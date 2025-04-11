namespace AIStudio.Provider.HuggingFace;

public static class HFInstanceProviderExtensions
{
    public static string Endpoints(this HFInstanceProvider provider, Model model) => provider switch
    {
        HFInstanceProvider.CEREBRAS => "cerebras/v1/",
        HFInstanceProvider.NEBIUS_AI_STUDIO => "nebius/v1/",
        HFInstanceProvider.SAMBANOVA => "sambanova/v1/",
        HFInstanceProvider.NOVITA => "novita/v3/openai/",
        HFInstanceProvider.HYPERBOLIC => "hyperbolic/v1/",
        HFInstanceProvider.TOGETHER_AI => "together/v1/",
        HFInstanceProvider.FIREWORKS => "fireworks-ai/inference/v1/",
        HFInstanceProvider.HF_INFERENCE_API => $"hf-inference/models/{model.ToString()}/v1/",
        _ => string.Empty,
    };
    
    public static string EndpointsId(this HFInstanceProvider provider) => provider switch
    {
        HFInstanceProvider.CEREBRAS => "cerebras",
        HFInstanceProvider.NEBIUS_AI_STUDIO => "nebius",
        HFInstanceProvider.SAMBANOVA => "sambanova",
        HFInstanceProvider.NOVITA => "novita",
        HFInstanceProvider.HYPERBOLIC => "hyperbolic",
        HFInstanceProvider.TOGETHER_AI => "together",
        HFInstanceProvider.FIREWORKS => "fireworks",
        HFInstanceProvider.HF_INFERENCE_API => "hf-inference",
        _ => string.Empty,
    };
    
    public static string ToName(this HFInstanceProvider provider) => provider switch
    {
        HFInstanceProvider.CEREBRAS => "Cerebras",
        HFInstanceProvider.NEBIUS_AI_STUDIO => "Nebius AI Studio",
        HFInstanceProvider.SAMBANOVA => "Sambanova",
        HFInstanceProvider.NOVITA => "Novita",
        HFInstanceProvider.HYPERBOLIC => "Hyperbolic",
        HFInstanceProvider.TOGETHER_AI => "Together AI",
        HFInstanceProvider.FIREWORKS => "Fireworks AI",
        HFInstanceProvider.HF_INFERENCE_API => "Hugging Face Inference API",
        _ => string.Empty,
    };
}