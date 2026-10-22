namespace AIStudio.Models.Matching;

/// <summary>
/// What the index made of one model name, and how it got there.
/// </summary>
/// <remarks>
/// The profile alone is what the app asks for. The rest is for the people maintaining the rules:
/// which rule answered, what adjusted the answer afterwards, and whether two rules claimed the name
/// with the same right. The verification run reads all of it; a test that wants to know why a model
/// came out the way it did reads it too.
/// </remarks>
/// <param name="Profile">Everything known about the model.</param>
/// <param name="Selector">The rule which chose the model, or null when no rule knows the name.</param>
/// <param name="Modifiers">The rules which adjusted the answer, in the order they were applied.</param>
/// <param name="TiedSelectors">Rules which claimed the name just as strongly as the selector did.</param>
public sealed record ModelResolution(ModelProfile Profile, ModelRule? Selector, IReadOnlyList<ModelRule> Modifiers, IReadOnlyList<ModelRule> TiedSelectors)
{
    /// <summary>
    /// The answer for a name no rule was even asked about.
    /// </summary>
    public static readonly ModelResolution NOTHING = new(ModelProfile.UNKNOWN, null, [], []);

    /// <summary>
    /// Whether more than one rule claimed this name with the same specificity.
    /// </summary>
    /// <remarks>
    /// Always a mistake in the rules. The answer is still the same one every time, so a build never
    /// depends on the order the rules were registered in, but which of the two was meant is
    /// something only a person can say.
    /// </remarks>
    public bool IsAmbiguous => this.TiedSelectors.Count > 0;

    /// <summary>
    /// Whether any rule at all knew this name.
    /// </summary>
    public bool IsKnown => this.Selector is not null;
}