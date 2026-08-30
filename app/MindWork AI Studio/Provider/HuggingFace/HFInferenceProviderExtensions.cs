using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider.HuggingFace;

public static class HFInferenceProviderExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(HFInferenceProviderExtensions).Namespace, nameof(HFInferenceProviderExtensions));

    /// <summary>
    /// The slug Hugging Face uses for this inference provider.
    /// </summary>
    /// <param name="provider">The inference provider.</param>
    /// <returns>The slug, or an empty string for the routing strategies, which name no provider.</returns>
    public static string EndpointsId(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.BASETEN => "baseten",
        HFInferenceProvider.CEREBRAS => "cerebras",
        HFInferenceProvider.COHERE => "cohere",
        HFInferenceProvider.DEEPINFRA => "deepinfra",
        HFInferenceProvider.FEATHERLESS_AI => "featherless-ai",
        HFInferenceProvider.FIREWORKS => "fireworks-ai",
        HFInferenceProvider.GROQ => "groq",
        HFInferenceProvider.HF_INFERENCE_API => "hf-inference",
        HFInferenceProvider.NOVITA => "novita",
        HFInferenceProvider.NSCALE => "nscale",
        HFInferenceProvider.OVHCLOUD => "ovhcloud",
        HFInferenceProvider.PUBLIC_AI => "publicai",
        HFInferenceProvider.SCALEWAY => "scaleway",
        HFInferenceProvider.TOGETHER_AI => "together",
        HFInferenceProvider.ZAI => "zai-org",

        _ => string.Empty,
    };

    /// <summary>
    /// The suffix which tells the router where to send the request.
    /// </summary>
    /// <remarks>
    /// The router serves every provider through one endpoint. Which provider answers is decided by
    /// a suffix on the model name, e.g. "google/gemma-4-31B-it:novita". Without a suffix, the router
    /// picks the fastest provider itself.
    /// </remarks>
    /// <param name="provider">The inference provider.</param>
    /// <returns>The suffix including its colon, or an empty string when the router should choose.</returns>
    public static string ModelSuffix(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.NONE or HFInferenceProvider.AUTOMATIC => string.Empty,

        HFInferenceProvider.CHEAPEST => ":cheapest",
        HFInferenceProvider.PREFERRED => ":preferred",

        _ => $":{provider.EndpointsId()}",
    };

    /// <summary>
    /// Whether this inference provider serves models to chat with.
    /// </summary>
    /// <remarks>
    /// The Hugging Face Inference API is the odd one out: it grew from the classic NLP tasks and
    /// serves embeddings, speech recognition, classification, and translation, but no chat models
    /// at all. Asking it for one is answered with "The requested model is not supported by provider
    /// 'hf-inference'", whichever model is named. So it must not be offered where a chat provider
    /// is chosen.
    /// </remarks>
    /// <param name="provider">The inference provider.</param>
    /// <returns>True, when the provider serves chat models.</returns>
    public static bool SupportsChat(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.NONE => false,
        HFInferenceProvider.HF_INFERENCE_API => false,

        _ => true,
    };

    /// <summary>
    /// Removes the routing suffix from a model, if it carries one.
    /// </summary>
    /// <remarks>
    /// The suffix says where a request goes, not what the model is. Everything asking what a model
    /// can do has to look at the bare name: "google/gemma-4-31B-it:novita" is the same model as
    /// "google/gemma-4-31B-it", and a name detection which never heard of the suffix would miss it.
    /// Model IDs on the hub are written as "org/model" and carry no colon of their own, so the last
    /// colon always starts the suffix.
    /// </remarks>
    /// <param name="model">The model as it is configured.</param>
    /// <returns>The model without its routing suffix.</returns>
    public static Model WithoutRoutingSuffix(this Model model)
    {
        var separatorIndex = model.Id.LastIndexOf(':');
        return separatorIndex is -1 ? model : model with { Id = model.Id[..separatorIndex] };
    }

    /// <summary>
    /// The value to filter the Hugging Face model catalog by.
    /// </summary>
    /// <param name="provider">The inference provider.</param>
    /// <returns>The provider slug, or "all" when no particular provider was chosen.</returns>
    public static string CatalogFilter(this HFInferenceProvider provider)
    {
        var slug = provider.EndpointsId();
        return string.IsNullOrEmpty(slug) ? "all" : slug;
    }

    public static string ToName(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.AUTOMATIC => TB("Automatic: the fastest provider"),
        HFInferenceProvider.CHEAPEST => TB("Automatic: the cheapest provider"),
        HFInferenceProvider.PREFERRED => TB("Automatic: your preferred order"),

        HFInferenceProvider.BASETEN => "Baseten",
        HFInferenceProvider.CEREBRAS => "Cerebras",
        HFInferenceProvider.COHERE => "Cohere",
        HFInferenceProvider.DEEPINFRA => "DeepInfra",
        HFInferenceProvider.FEATHERLESS_AI => "Featherless AI",
        HFInferenceProvider.FIREWORKS => "Fireworks AI",
        HFInferenceProvider.GROQ => "Groq",
        HFInferenceProvider.HF_INFERENCE_API => "Hugging Face Inference API",
        HFInferenceProvider.NOVITA => "Novita",
        HFInferenceProvider.NSCALE => "Nscale",
        HFInferenceProvider.OVHCLOUD => "OVHcloud",
        HFInferenceProvider.PUBLIC_AI => "Public AI",
        HFInferenceProvider.SCALEWAY => "Scaleway",
        HFInferenceProvider.TOGETHER_AI => "Together AI",
        HFInferenceProvider.ZAI => "Z.ai",

        _ => string.Empty,
    };
}