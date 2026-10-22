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
    /// <param name="provider">The inference provider.</param>
    /// <returns>True, when the provider serves chat models.</returns>
    public static bool SupportsChat(this HFInferenceProvider provider) => provider is not HFInferenceProvider.NONE;

    /// <summary>
    /// Whether this inference provider creates embeddings for us.
    /// </summary>
    /// <remarks>
    /// Embeddings are a much shorter story than chatting. The router serves them nowhere near its
    /// own endpoint, only through the route of a provider, and only two of those answer the
    /// OpenAI-compatible form we send. The routing strategies are out by their nature: without a
    /// named provider there is no route to address.
    /// </remarks>
    /// <param name="provider">The inference provider.</param>
    /// <returns>True, when we can create embeddings through this provider.</returns>
    public static bool SupportsEmbeddings(this HFInferenceProvider provider) => provider is HFInferenceProvider.TOGETHER_AI or HFInferenceProvider.DEEPINFRA;

    /// <summary>
    /// Whether this inference provider transcribes audio for us.
    /// </summary>
    /// <remarks>
    /// The same two providers as for embeddings, and for the same reason: transcription lives on a
    /// provider's own route, and only these two answer the OpenAI-compatible form there. Others do
    /// transcribe for Hugging Face, but not in a shape we could send an audio file to: fal-ai and
    /// Replicate both turn the request down with "Model not supported by provider".
    /// </remarks>
    /// <param name="provider">The inference provider.</param>
    /// <returns>True, when we can transcribe audio through this provider.</returns>
    public static bool SupportsTranscription(this HFInferenceProvider provider) => provider is HFInferenceProvider.TOGETHER_AI or HFInferenceProvider.DEEPINFRA;

    /// <summary>
    /// The base URL of the provider's own OpenAI-compatible route.
    /// </summary>
    /// <remarks>
    /// Only chatting goes through the router's own endpoint. Everything else has to address the
    /// provider directly, and they do not agree on where their OpenAI-compatible API sits: DeepInfra
    /// keeps it below an additional "openai" segment, and answers the path without it with
    /// "Not allowed to POST /v1/embeddings for provider deepinfra".
    /// </remarks>
    /// <param name="provider">The inference provider.</param>
    /// <returns>The base URL, or an empty string when the provider has no such route.</returns>
    public static string ProviderBaseURL(this HFInferenceProvider provider) => provider switch
    {
        HFInferenceProvider.TOGETHER_AI => "https://router.huggingface.co/together/v1/",
        HFInferenceProvider.DEEPINFRA => "https://router.huggingface.co/deepinfra/v1/openai/",

        _ => string.Empty,
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