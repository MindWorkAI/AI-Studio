namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// One version of a result: a draft, or the version the person confirmed.
/// </summary>
public sealed class BflResultVersion
{
    /// <summary>
    /// Gets or sets the text of the result, in Markdown.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the revisions of the confirmed work states the result was built from, see
    /// BflStaleness.ConfirmedRevisionsOf. A step without a confirmed work state at that time is missing.
    /// </summary>
    /// <remarks>
    /// Editing the text keeps them: the person changes the wording, not what the result builds on.
    /// </remarks>
    public Dictionary<BflStep, int> WorkStateRevisions { get; set; } = [];

    /// <summary>
    /// Gets or sets when the lab wrote the draft, or when the person confirmed the result.
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; set; }
}