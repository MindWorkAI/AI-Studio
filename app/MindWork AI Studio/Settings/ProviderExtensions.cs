using AIStudio.Models;
using AIStudio.Models.Live;
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
    /// win: what the person said about their own installation, then what the installation itself
    /// reported, then what the rules worked out from the name, and last what the app assumes when
    /// nothing else said anything.
    /// </remarks>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The profile of the configured model.</returns>
    public static ModelProfile GetModelProfile(this Provider provider)
    {
        var automatic = provider.GetAutomaticModelProfile();
        return provider.CapabilityOverrides?.ApplyTo(automatic) ?? automatic;
    }

    /// <summary>
    /// Everything known about the configured model except what the person themselves switched.
    /// </summary>
    /// <remarks>
    /// This is what happens when somebody fills in nothing, which is why the expert dialog shows it
    /// as the automatic answer. It has to include what the provider reported: a person who leaves
    /// the window empty gets the number their own engine stated, and a placeholder showing them a
    /// different one would be a promise the app does not keep.
    /// </remarks>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The profile of the configured model, without that provider's overrides.</returns>
    public static ModelProfile GetAutomaticModelProfile(this Provider provider)
    {
        var stated = provider.UsedLLMProvider.GetModelProfile(provider.Model);
        return ListedModels.Shared.Of(provider.Id, provider.Model.Id).ApplyTo(stated);
    }

    /// <summary>
    /// Everything the rules know about a model at a provider, without anybody's own installation.
    /// </summary>
    /// <remarks>
    /// The answer to the model as such, which is the same for everybody who uses that name at that
    /// provider -- and therefore the answer the registry caches. What one particular installation
    /// says about it is asked one link further up, where the instance is known.
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

    /// <summary>
    /// Checks whether this model can be used for chatting.
    /// </summary>
    /// <remarks>
    /// What a model can do and what it is made for used to be two questions answered by two pieces
    /// of code, each walking the same name with rules of its own. They disagreed: a model like
    /// nomic-embed-text was an embedding model at one provider and a chat model at the next. Both
    /// come out of the same rules now, which is why this takes the provider -- the same name means
    /// different things depending on who serves it, and only the provider knows how to unwrap it.
    ///
    /// The direction of the answer is deliberate. Everything not recognized as something else is a
    /// chat model, so a provider adding a family we have never seen keeps it visible to the person
    /// paying for it. Getting it wrong the other way would hide a model.
    /// </remarks>
    /// <param name="model">The model to check.</param>
    /// <param name="provider">The provider serving it.</param>
    /// <returns>True, when the model is a chat model or when we recognize no other kind.</returns>
    public static bool IsChatModel(this Model model, LLMProviders provider) => provider.GetModelProfile(model).Kind is ModelKind.CHAT;

    /// <summary>
    /// Checks whether this model creates embeddings.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <param name="provider">The provider serving it.</param>
    /// <returns>True, when the model is an embedding model.</returns>
    public static bool IsEmbeddingModel(this Model model, LLMProviders provider) => provider.GetModelProfile(model).Kind is ModelKind.EMBEDDING;

    /// <summary>
    /// Checks whether this model transcribes audio.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <param name="provider">The provider serving it.</param>
    /// <returns>True, when the model is a transcription model.</returns>
    public static bool IsTranscriptionModel(this Model model, LLMProviders provider) => provider.GetModelProfile(model).Kind is ModelKind.TRANSCRIPTION;

    /// <summary>
    /// Checks whether this model generates images.
    /// </summary>
    /// <param name="model">The model to check.</param>
    /// <param name="provider">The provider serving it.</param>
    /// <returns>True, when the model is an image generation model.</returns>
    public static bool IsImageModel(this Model model, LLMProviders provider) => provider.GetModelProfile(model).Kind is ModelKind.IMAGE_GENERATION;
}