using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The entries a models endpoint lists which are no models.
/// </summary>
/// <remarks>
/// OpenAI lists its code interpreter's container resource among the models. Talking to it gets an
/// error, so it must not appear in any list the app shows -- and whatever else such a name might
/// suggest, none of the other kinds applies to it. That is why it outranks every one of them
/// instead of competing on the length of a word.
/// </remarks>
public sealed class NotAModelFamily : ModelFamily
{
    /// <summary>
    /// Why this outranks every other statement about a kind.
    /// </summary>
    private const string THERE_IS_NO_MODEL_TO_CLASSIFY = "An entry which is no model cannot be a model of some kind. Whatever else its name carries is beside the point, so no other statement may outweigh this one.";

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/api-reference/containers", new DateOnly(2026, 9, 12), "Ported from the marker of Provider/ModelKindExtensions.cs which was checked before all others, written as a name part rather than as a substring so that a containerized model keeps its kind.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Modifier("container").AsSegment()
            .Rank(2, THERE_IS_NO_MODEL_TO_CLASSIFY)
            .Kind(ModelKind.OTHER);
}