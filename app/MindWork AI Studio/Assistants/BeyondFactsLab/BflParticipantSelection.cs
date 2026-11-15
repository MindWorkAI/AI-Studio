namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whom of the list of participants the BeyondFacts Lab researches in depth, stored as research/participant-selection.json
/// in the folder of the step "Strategy".
/// </summary>
/// <remarks>
/// A selection belongs to the list it was made for, see IsFor. While there is none for the current list, a short list
/// counts as selected as a whole, and a long one as not selected, see SelectedIn; for a long list, the lab suggests a
/// selection, see BflResearchRunner.SuggestSelectionAsync.
/// </remarks>
public sealed class BflParticipantSelection
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes. Raise it with every new property.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// How many people the lab suggests at most. A list of no more people counts as selected as a whole.
    /// </summary>
    public const int MAX_SUGGESTED_PEOPLE = 5;

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets the names of the selected people, as the list gives them.
    /// </summary>
    public List<string> Names { get; set; } = [];

    /// <summary>
    /// Gets or sets who chose the people.
    /// </summary>
    public BflParticipantSelectionSource Source { get; set; } = BflParticipantSelectionSource.PERSON;

    /// <summary>
    /// Gets or sets when the list the selection was made for was researched, see BflResearch.CreatedAtUtc.
    /// </summary>
    public DateTimeOffset ListCreatedAtUtc { get; set; }

    /// <summary>
    /// Tells whether the selection was made for a list, and not for an earlier one.
    /// </summary>
    /// <param name="list">The research on the participants.</param>
    public bool IsFor(BflResearch list) => this.ListCreatedAtUtc == list.CreatedAtUtc;

    /// <summary>
    /// Gets the people of a list whom the lab researches in depth.
    /// </summary>
    /// <param name="list">The research on the participants.</param>
    /// <param name="selection">The stored selection, or null when there is none.</param>
    /// <returns>The people, in the order of the list.</returns>
    public static IReadOnlyList<BflParticipant> SelectedIn(BflResearch list, BflParticipantSelection? selection)
    {
        if (selection is not null && selection.IsFor(list))
            return [.. list.Participants.Where(participant => selection.Names.Any(participant.HasName))];

        return list.Participants.Count <= MAX_SUGGESTED_PEOPLE ? list.Participants : [];
    }

    /// <summary>
    /// Tells whether the lab suggests whom of a list to research: when the list is long and nobody chose for it yet.
    /// </summary>
    /// <param name="list">The research on the participants.</param>
    /// <param name="selection">The stored selection, or null when there is none.</param>
    public static bool NeedsSuggestion(BflResearch list, BflParticipantSelection? selection) => list.Participants.Count > MAX_SUGGESTED_PEOPLE && (selection is null || !selection.IsFor(list));
}