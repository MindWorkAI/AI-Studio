namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A work state the person confirmed, which the later steps build on.
/// </summary>
public sealed class BflConfirmedWorkState
{
    /// <summary>
    /// Gets or sets the revision of the work state, which grows with every confirmation of the step.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets when the person confirmed the work state.
    /// </summary>
    public DateTimeOffset ConfirmedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the texts of the work state, by the keys of their sections. An empty section is left out.
    /// </summary>
    public Dictionary<string, string> Sections { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the revisions of the confirmed work states of the earlier steps at the time of the confirmation.
    /// An earlier step without a confirmed work state is missing.
    /// </summary>
    public Dictionary<BflStep, int> UpstreamRevisions { get; set; } = [];

    /// <summary>
    /// Gets or sets the fingerprint of the form of the step at the time of the confirmation.
    /// </summary>
    public string DetailsFingerprint { get; set; } = string.Empty;
}