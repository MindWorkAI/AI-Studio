namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Who chose the people of the list of participants whom the BeyondFacts Lab researches.
/// </summary>
public enum BflParticipantSelectionSource
{
    /// <summary>
    /// The lab suggested them, since the list was long. The person may change the selection.
    /// </summary>
    SUGGESTION,

    /// <summary>
    /// The person chose them.
    /// </summary>
    PERSON,
}