using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Mistral;

/// <summary>
/// A Mistral family whose abilities depend on when the model was released rather than on its name.
/// </summary>
/// <remarks>
/// This is the case the refinement exists for. Four Mistral families gained image input and
/// reasoning at some release and carried the same name before and after, so no pattern can tell
/// the two apart: mistral-large-2411 and mistral-large-2512 differ in what they can do and in
/// nothing a rule could match on.
///
/// So the rule states what the family has always been able to do, and each family says from which
/// release on it gained the rest. Everything shared sits here; a family below is three numbers and
/// one rule.
/// </remarks>
public abstract class MistralReleaseDatedFamily : ModelFamily
{
    /// <summary>
    /// What every one of these families could do from its very first release.
    /// </summary>
    protected const Capability WHAT_THEY_COULD_ALWAYS_DO = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.FUNCTION_CALLING;

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <summary>
    /// The release from which this family accepts images.
    /// </summary>
    protected abstract int VisionSince { get; }

    /// <summary>
    /// The release from which this family can reason, or never.
    /// </summary>
    protected abstract int ReasoningSince { get; }

    /// <summary>
    /// The release this family's "latest" alias currently points at.
    /// </summary>
    /// <remarks>
    /// Mistral moves the alias on with every release, so it has to behave like the release it
    /// resolves to instead of carrying rules of its own.
    /// </remarks>
    protected abstract int LatestRelease { get; }

    /// <inheritdoc />
    public override ModelProfile Refine(in ModelId id, in ModelProfile selected)
    {
        var release = MistralReleases.Of(id, this.LatestRelease);

        return selected with
        {
            Capabilities = release >= this.VisionSince ? selected.Capabilities | Capability.MULTIPLE_IMAGE_INPUT : selected.Capabilities,
            Reasoning = release >= this.ReasoningSince ? ReasoningSupport.OPTIONAL : selected.Reasoning,
        };
    }
}