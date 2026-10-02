namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// One of the two models in a comparison, and the way to ask it.
/// </summary>
/// <remarks>
/// The way to ask is a function rather than a provider, so the comparison itself does not depend on
/// anything of the app. <see cref="ModelComparisonProviderAdapter"/> builds the function for a real
/// provider.
/// </remarks>
public sealed record ModelComparisonParticipant
{
    /// <summary>
    /// The name to show for the model. It is copied into the answer, see <see cref="ModelComparisonAnswer.Label"/>.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Sends a prompt to the model and streams the answer back, chunk by chunk.
    /// </summary>
    public required Func<string, CancellationToken, IAsyncEnumerable<string>> Ask { get; init; }
}