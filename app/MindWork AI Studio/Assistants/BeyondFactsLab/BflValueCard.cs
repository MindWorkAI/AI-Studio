namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A value of the value deck of the BeyondFacts Lab, which the person sorts in the step "Values and perspectives".
/// </summary>
/// <remarks>
/// The card sort stores the ID only, so that the same sort shows in every language of the app. The ID never changes.
/// </remarks>
public sealed class BflValueCard
{
    /// <summary>
    /// Gets or sets the ID of the value, e.g., "honesty".
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the value, e.g., "Honesty".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the value means, in a few words, e.g., "Present statements sincerely".
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}