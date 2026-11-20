using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the BeyondFacts Lab reads from the suggestion of a model whom to research: the names it accepts, or what is
/// wrong with the suggestion.
/// </summary>
/// <remarks>
/// The suggestion comes from a request which knows the details of the preparation, and its names go on into searches.
/// Therefore the lab accepts it only as a whole, and only when every name stands on the list of participants: it
/// neither corrects a name nor drops one it does not know. A rejected suggestion goes back to the model once, with its
/// problems, see BflPromptComposer.ComposeParticipantSelectionRepairRequest.
/// </remarks>
/// <param name="Names">The names, spelled as on the list, in the order of the answer; empty when there are problems.</param>
/// <param name="Problems">What is wrong with the suggestion, for the model, in German like the prompts.</param>
public sealed record BflParticipantSelectionCheck(IReadOnlyList<string> Names, IReadOnlyList<string> Problems)
{
    /// <summary>
    /// Reads and checks the suggestion of a model.
    /// </summary>
    /// <param name="answer">The answer of the model.</param>
    /// <param name="participants">The list of participants.</param>
    /// <returns>The names, or the problems.</returns>
    public static BflParticipantSelectionCheck Read(string answer, IReadOnlyList<BflParticipant> participants)
    {
        var json = ModelAnswerJson.ExtractObject(answer);
        if (json.IsWhiteSpace())
            return Rejected("Die Antwort enthält kein vollständiges JSON-Objekt.");

        BflParticipantSelectionAnswer? read;
        try
        {
            read = JsonSerializer.Deserialize<BflParticipantSelectionAnswer>(json, BflModelAnswerJson.OPTIONS);
        }
        catch (JsonException exception)
        {
            return Rejected($"Das JSON-Objekt ist nicht lesbar: {exception.Message}");
        }

        if (read?.Participants is not { } names)
            return Rejected("Das Feld „participants“ fehlt.");

        var problems = new List<string>();
        if (names.Count is 0)
            problems.Add("Die Auswahl ist leer; sie braucht mindestens einen Namen.");
        else if (names.Count > BflParticipantSelection.MAX_SUGGESTED_PEOPLE)
            problems.Add($"Die Auswahl enthält {names.Count} Namen; erlaubt sind höchstens {BflParticipantSelection.MAX_SUGGESTED_PEOPLE}.");

        var accepted = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                problems.Add("Ein Eintrag der Auswahl ist leer.");
                continue;
            }

            if (participants.FirstOrDefault(candidate => candidate.HasName(name)) is not { } participant)
            {
                problems.Add($"„{name.Trim()}“ steht nicht in der Liste der Teilnehmenden.");
                continue;
            }

            if (!keys.Add(BflParticipant.NameKey(participant.Name)))
            {
                problems.Add($"„{participant.Name}“ steht mehr als einmal in der Auswahl.");
                continue;
            }

            accepted.Add(participant.Name);
        }

        return problems.Count > 0 ? new BflParticipantSelectionCheck([], problems) : new BflParticipantSelectionCheck(accepted, []);
    }

    /// <summary>
    /// Gets a check which rejects the suggestion for one reason.
    /// </summary>
    /// <param name="problem">The reason.</param>
    private static BflParticipantSelectionCheck Rejected(string problem) => new([], [problem]);
}