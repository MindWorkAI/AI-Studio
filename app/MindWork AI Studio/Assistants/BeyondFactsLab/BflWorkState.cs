namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The work state of one step: what the step yielded, stored as work-state.json in the folder of the step.
/// </summary>
/// <remarks>
/// The lab writes a draft from the conversation of the step, which the person edits and confirms. Only a confirmed
/// work state reaches the prompts of the lab; a draft never does. The person may change or withdraw a confirmed work
/// state at any time. The texts are stored under the keys of the sections of the step, see BflWorkStateSections.
/// </remarks>
public sealed class BflWorkState
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
    /// Gets or sets the draft the person has not confirmed yet, by the keys of its sections, or null while there is
    /// none.
    /// </summary>
    public Dictionary<string, string>? Draft { get; set; }

    /// <summary>
    /// Gets or sets the work state the person confirmed, or null while there is none.
    /// </summary>
    public BflConfirmedWorkState? Confirmed { get; set; }

    /// <summary>
    /// Gets or sets the revision the latest confirmation got.
    /// </summary>
    /// <remarks>
    /// It keeps counting when the person withdraws a work state, so that a confirmation after a withdrawal never gets
    /// the revision of an earlier one, which the later steps still remember.
    /// </remarks>
    public int LastRevision { get; set; }

    /// <summary>
    /// Gets or sets the values the lab suggests for empty fields of the form of the step, by the IDs of the fields.
    /// </summary>
    /// <remarks>
    /// The lab writes them together with a draft. The person takes over or dismisses each of them; until then, the
    /// form stays as it is.
    /// </remarks>
    public Dictionary<string, string> FieldSuggestions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Confirms a work state: it becomes the one the later steps build on, and the draft is gone.
    /// </summary>
    /// <param name="sections">The texts of the work state, by the keys of their sections.</param>
    /// <param name="upstreamRevisions">The revisions of the work states of the earlier steps, see BflStaleness.UpstreamRevisionsOf.</param>
    /// <param name="detailsFingerprint">The fingerprint of the form of the step, see BflStaleness.FingerprintOf.</param>
    /// <param name="confirmedAtUtc">When the person confirmed it.</param>
    public void Confirm(IReadOnlyDictionary<string, string> sections, IReadOnlyDictionary<BflStep, int> upstreamRevisions, string detailsFingerprint, DateTimeOffset confirmedAtUtc)
    {
        this.LastRevision++;
        this.Confirmed = new BflConfirmedWorkState
        {
            Revision = this.LastRevision,
            ConfirmedAtUtc = confirmedAtUtc,
            Sections = new(sections.Where(section => !string.IsNullOrWhiteSpace(section.Value)), StringComparer.Ordinal),
            UpstreamRevisions = new(upstreamRevisions),
            DetailsFingerprint = detailsFingerprint,
        };

        this.Draft = null;
    }

    /// <summary>
    /// Withdraws the confirmed work state, so that the later steps no longer build on it.
    /// </summary>
    public void Withdraw() => this.Confirmed = null;
}