namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The fixed labels in the data of a briefing, which its runtime shows.
/// </summary>
/// <param name="Reset">The label of the buttons which reset a simulation.</param>
/// <param name="Brand">The name of the app which created the briefing.</param>
internal sealed record VisualBriefingLabels(string Reset, string Brand)
{
    /// <summary>
    /// Creates the labels of a briefing.
    /// </summary>
    /// <param name="resetLabel">The label of the buttons which reset a simulation.</param>
    /// <returns>The labels.</returns>
    internal static VisualBriefingLabels Of(string resetLabel) => new(resetLabel, "MindWork AI Studio");
}