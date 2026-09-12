using AIStudio.Models;
using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    /// <summary>
    /// The longest model ID we normalize without going to the heap.
    /// </summary>
    private const int MAX_STACK_ALLOCATED_MODEL_ID_LENGTH = 256;

    /// <summary>
    /// Brings a model ID into the form the capability rules are written in.
    /// </summary>
    /// <remarks>
    /// Every provider names the same model differently, and the difference is rarely in the words:
    /// it is in what sits between them. Ollama separates the variant with a colon
    /// ("qwen3.8:27b-mlx"), Blablador answers with a whole sentence ("10 - Muse Glimmer 30b - the
    /// newest META model"), Fireworks puts a path in front
    /// ("accounts/fireworks/models/llama-v3p1-405b-instruct"), and the hubs use hyphens. Without
    /// this, every rule would have to spell out each of those writings, which is what the Llama
    /// block used to do with four variants of one check.
    ///
    /// The dots stay. They carry the version boundary: llama3 and llama3.1 are different models,
    /// and only the latter calls functions. Dropping them would merge the two.
    ///
    /// The patterns in the rules are written in this normalized form already, which is why they
    /// use lowercase and hyphens throughout.
    /// </remarks>
    /// <param name="modelId">The model ID as the provider reports it.</param>
    /// <returns>The model ID in lowercase, with every separator written as a single hyphen.</returns>
    private static string NormalizeModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return string.Empty;

        //
        // Normalizing never makes a name longer, so the original length is always enough room.
        // Model IDs are short, which is why the buffer lives on the stack: the longest ones we
        // know of are the descriptive names Blablador answers with, at around 75 characters. A
        // provider reporting something longer still gets a correct answer, just from the heap.
        //
        Span<char> normalized = modelId.Length <= MAX_STACK_ALLOCATED_MODEL_ID_LENGTH
            ? stackalloc char[modelId.Length]
            : new char[modelId.Length];

        var length = 0;
        foreach (var character in modelId)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.')
            {
                normalized[length++] = char.ToLowerInvariant(character);
                continue;
            }

            // Anything else separates two parts of the name. A leading separator, and a repeated
            // one, say nothing and would only get in the way of the patterns:
            if (length is 0 || normalized[length - 1] is '-')
                continue;

            normalized[length++] = '-';
        }

        // A trailing separator carries no meaning either:
        if (length > 0 && normalized[length - 1] is '-')
            length--;

        return new string(normalized[..length]);
    }

    /// <summary>
    /// Everything the app knows about the model this provider instance is configured with.
    /// </summary>
    /// <remarks>
    /// The one door to that question. Behind it stand the links of the chain, in the order they
    /// win: what the person said about their own installation, then what the rules worked out from
    /// the name, then what the app assumes when nothing else said anything.
    /// </remarks>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The profile of the configured model.</returns>
    public static ModelProfile GetModelProfile(this Provider provider)
    {
        var stated = provider.UsedLLMProvider.GetModelProfile(provider.Model);
        return provider.CapabilityOverrides?.ApplyTo(stated) ?? stated;
    }

    /// <summary>
    /// Everything the rules know about a model at a provider, without anybody's own settings.
    /// </summary>
    /// <remarks>
    /// What the expert dialog shows next to each switch as the automatic answer, so that a person
    /// can see what they are overriding.
    ///
    /// The assumed profile fills in where no rule stated a single capability. It fills in the
    /// capabilities only: a modifier may well have said what the model is made for without any rule
    /// saying what it can do, and an embedding model nobody wrote a rule for stays an embedding
    /// model rather than turning into a chat model with an assumption attached.
    /// </remarks>
    /// <param name="provider">The LLM provider the model is reached through.</param>
    /// <param name="model">The model, named the way that provider names it.</param>
    /// <returns>The profile, which knows nothing when there is nothing to reach.</returns>
    public static ModelProfile GetModelProfile(this LLMProviders provider, Model model)
    {
        //
        // Without a provider there is nothing to reach the model through, and an empty name is what
        // a provider reports before anybody picked one. Neither is a model we could assume anything
        // about, so neither gets the assumption.
        //
        if (provider is LLMProviders.NONE || string.IsNullOrWhiteSpace(model.Id))
            return ModelProfile.UNKNOWN;

        var stated = ModelRegistry.Shared.Profile(provider, model.Id);
        return stated.Capabilities is Capability.NONE
            ? stated with { Capabilities = ModelProfile.ASSUMED.Capabilities }
            : stated;
    }

    /// <summary>
    /// Get the capabilities of the model used by the configured provider.
    /// </summary>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The capabilities of the configured model.</returns>
    public static List<Capability> GetModelCapabilities(this Provider provider)
    {
        var automaticCapabilities = provider.UsedLLMProvider.GetModelCapabilities(provider.Model);
        return provider.CapabilityOverrides?.ApplyTo(automaticCapabilities) ?? automaticCapabilities;
    }
    
    /// <summary>
    /// Get whether the model used by the configured provider accepts images as input.
    /// </summary>
    /// <remarks>
    /// Two capabilities express image input, one for a single image and one for several. Anything that
    /// wants to know whether an image may be sent has to accept both, which is why the question is asked
    /// here instead of at each call site: attaching a file and validating an already attached file must
    /// never disagree about it.
    /// </remarks>
    /// <param name="provider">The configured provider.</param>
    /// <returns><c>true</c> when the model accepts image input.</returns>
    public static bool SupportsImageInput(this Provider provider)
    {
        var capabilities = provider.GetModelCapabilities();
        return capabilities.Contains(Capability.SINGLE_IMAGE_INPUT) || capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT);
    }

    /// <summary>
    /// Get the capabilities of a model for a specific provider.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <param name="model">The model to get the capabilities for.</param>
    /// <returns>>The capabilities of the model.</returns>
    public static List<Capability> GetModelCapabilities(this LLMProviders provider, Model model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
            return [];

        return provider switch
        {
            LLMProviders.OPEN_AI => GetModelCapabilitiesOpenAI(model),
            LLMProviders.MISTRAL => GetModelCapabilitiesMistral(model),
            LLMProviders.ANTHROPIC => GetModelCapabilitiesAnthropic(model),
            LLMProviders.GOOGLE => GetModelCapabilitiesGoogle(model),
            LLMProviders.X => GetModelCapabilitiesOpenSource(model),
            LLMProviders.DEEP_SEEK => GetModelCapabilitiesDeepSeek(model),
            LLMProviders.ALIBABA_CLOUD => GetModelCapabilitiesAlibaba(model),
            LLMProviders.PERPLEXITY => GetModelCapabilitiesPerplexity(model),
            LLMProviders.OPEN_ROUTER => GetModelCapabilitiesGateway(model),
            LLMProviders.HETZNER or LLMProviders.IONOS => GetModelCapabilitiesOpenSource(model),
            
            //
            // LiteLLM is a gateway just like OpenRouter, and it names its models the same way:
            // "vendor/model", e.g. "anthropic/claude-opus-5" or "azure/gpt-5.6". So we let the
            // gateway detection handle it, which resolves the vendor prefix and asks the
            // provider who really knows the model. Everything it cannot place is treated as
            // an open source model, which is the right fallback for a freely named alias:
            //
            LLMProviders.LITE_LLM => GetModelCapabilitiesGateway(model),

            LLMProviders.GROQ or LLMProviders.FIREWORKS => GetModelCapabilitiesOpenSource(model),

            //
            // Hugging Face names its models the way the hub does, "org/model", which is the same
            // shape the other gateways use. So we let the gateway detection resolve the organization
            // and ask the provider implementation which really knows the model. The routing suffix
            // has to go first: it says which inference provider answers, not what the model is.
            //
            LLMProviders.HUGGINGFACE => GetModelCapabilitiesGateway(model.WithoutRoutingSuffix()),
        
            LLMProviders.HELMHOLTZ => GetModelCapabilitiesOpenSource(model),
            LLMProviders.GWDG => GetModelCapabilitiesOpenSource(model),
        
            LLMProviders.SELF_HOSTED => GetModelCapabilitiesOpenSource(model),
        
            _ => []
        };
    }
}