using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    private static List<Capability> GetModelCapabilitiesOpenRouter(Model model)
    {
        //
        // OpenRouter model IDs follow the pattern "vendor/model-name". Examples:
        // - openai/gpt-5.6
        // - anthropic/claude-opus-5
        // - google/gemini-3.7-flash
        // - qwen/qwen3.8-flash-next
        //
        // OpenRouter offers the models of all the other providers. Instead of keeping a
        // second set of rules here, which would always lag behind, we hand the model
        // over to the provider implementation which already knows it. The vendor prefix
        // has to be removed first: some of those implementations match the beginning of
        // the model name and would not recognize a prefixed ID.
        //
        var separatorIndex = model.Id.IndexOf('/');
        var vendor = separatorIndex is -1 ? string.Empty : model.Id[..separatorIndex].ToLowerInvariant();
        var bareModel = separatorIndex is -1 ? model : model with { Id = model.Id[(separatorIndex + 1)..] };
        var bareModelName = bareModel.Id.ToLowerInvariant().AsSpan();

        var capabilities = vendor switch
        {
            // The gpt-oss models are open weights. The OpenAI implementation does not
            // know them, because they are not part of the OpenAI cloud offering:
            "openai" when bareModelName.IndexOf("gpt-oss") is not -1 => GetModelCapabilitiesOpenSource(bareModel),
            "openai" => GetModelCapabilitiesOpenAI(bareModel),

            "anthropic" => GetModelCapabilitiesAnthropic(bareModel),

            // Gemma is open weights, Gemini is not:
            "google" when bareModelName.IndexOf("gemma") is not -1 => GetModelCapabilitiesOpenSource(bareModel),
            "google" => GetModelCapabilitiesGoogle(bareModel),

            "mistralai" => GetModelCapabilitiesMistral(bareModel),
            "perplexity" => GetModelCapabilitiesPerplexity(bareModel),

            // Everything else is open source: Qwen, Llama, GLM, Kimi, Muse, Hunyuan,
            // Nemotron, Grok, and whatever OpenRouter adds next. DeepSeek belongs here
            // as well: its own implementation covers the aliases of the DeepSeek
            // platform, while OpenRouter uses the names of the open weights.
            _ => GetModelCapabilitiesOpenSource(bareModel),
        };

        return NormalizeForOpenRouter(capabilities);
    }

    /// <summary>
    /// Adjusts the capabilities reported by another provider for use through OpenRouter.
    /// </summary>
    /// <param name="capabilities">The capabilities as reported by the provider implementation.</param>
    /// <returns>The capabilities as they apply when using the model through OpenRouter.</returns>
    /// <remarks>
    /// OpenRouter serves every model through its OpenAI-compatible chat completion API.
    /// The Responses API is not available there, no matter which API the original
    /// provider offers.
    /// </remarks>
    private static List<Capability> NormalizeForOpenRouter(List<Capability> capabilities)
    {
        capabilities.Remove(Capability.RESPONSES_API);
        if(!capabilities.Contains(Capability.CHAT_COMPLETION_API))
            capabilities.Add(Capability.CHAT_COMPLETION_API);

        return capabilities;
    }
}