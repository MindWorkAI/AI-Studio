using AIStudio.Models;
using AIStudio.Models.Registry;
using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
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
    public static bool SupportsImageInput(this Provider provider) => provider.GetModelProfile().HasAny(Capability.SINGLE_IMAGE_INPUT | Capability.MULTIPLE_IMAGE_INPUT);
}