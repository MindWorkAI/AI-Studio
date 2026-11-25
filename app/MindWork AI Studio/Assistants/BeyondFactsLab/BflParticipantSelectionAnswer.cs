namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The answer in which a model suggests whom of a long list of participants the BeyondFacts Lab should research, as
/// JSON, see Prompts/research/participant-selection.md.
/// </summary>
public sealed class BflParticipantSelectionAnswer
{
    /// <summary>
    /// Gets or sets the names the model chose, which must come from the list; null when the answer leaves them out.
    /// </summary>
    public List<string>? Participants { get; set; }
}