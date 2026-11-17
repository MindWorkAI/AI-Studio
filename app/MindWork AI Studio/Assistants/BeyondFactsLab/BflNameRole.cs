namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whether a field of the BeyondFacts Lab holds names of people, which sharing a session replaces.
/// </summary>
public enum BflNameRole
{
    /// <summary>
    /// The field holds no names on purpose.
    /// </summary>
    NONE,

    /// <summary>
    /// The field holds the names of other participants of the event.
    /// </summary>
    PARTICIPANTS,
}