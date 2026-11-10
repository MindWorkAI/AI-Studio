namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A result of a preparation, such as the position paper, stored in the folder "results" of the preparation.
/// </summary>
/// <remarks>
/// The lab writes a draft, which the person reads, edits, and confirms; only a confirmed result can be exported. Like
/// a work state, a result keeps its confirmed version while the person works on a new draft. Whether it is outdated
/// is computed from the work states it was built from, see BflStaleness.IsOutdated.
/// </remarks>
public sealed class BflResult
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes. Raise it with every new property.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets the draft the person has not confirmed yet, or null while there is none.
    /// </summary>
    public BflResultVersion? Draft { get; set; }

    /// <summary>
    /// Gets or sets the version the person confirmed, or null while there is none.
    /// </summary>
    public BflResultVersion? Confirmed { get; set; }

    /// <summary>
    /// Starts a draft from the confirmed version, so that the person can edit it; a draft which is there already
    /// stays as it is.
    /// </summary>
    public void Edit()
    {
        if (this.Draft is not null || this.Confirmed is not { } confirmed)
            return;

        this.Draft = new BflResultVersion
        {
            Text = confirmed.Text,
            WorkStateRevisions = new(confirmed.WorkStateRevisions),
            ChangedAtUtc = confirmed.ChangedAtUtc,
        };
    }

    /// <summary>
    /// Confirms the draft with the text the person approved: it becomes the version to export, and the draft is gone.
    /// </summary>
    /// <param name="text">The text of the draft as the person approved it.</param>
    /// <param name="confirmedAtUtc">When the person confirmed it.</param>
    /// <exception cref="InvalidOperationException">There is no draft to confirm.</exception>
    public void Confirm(string text, DateTimeOffset confirmedAtUtc)
    {
        if (this.Draft is not { } draft)
            throw new InvalidOperationException("There is no draft of the result to confirm.");

        this.Confirmed = new BflResultVersion
        {
            Text = text,
            WorkStateRevisions = new(draft.WorkStateRevisions),
            ChangedAtUtc = confirmedAtUtc,
        };

        this.Draft = null;
    }
}