namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Shortens a question to what a summary or a stored result keeps of it, so that the user recognizes it again.
/// </summary>
public static class ModelComparisonQuestionPreview
{
    /// <summary>
    /// How many characters of a question the Model Comparison assistant keeps at most.
    /// </summary>
    public const int MAX_CHARACTERS = 300;

    /// <summary>
    /// Shortens a question, and marks the cut.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="maxCharacters">The number of characters of the question to keep, at most.</param>
    /// <returns>The question, or its beginning followed by an ellipsis.</returns>
    public static string Of(string question, int maxCharacters = MAX_CHARACTERS)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);

        if (question.Length <= maxCharacters)
            return question;

        // Cutting between the two halves of a character outside the basic plane, an emoji for example, would leave half of it:
        var length = char.IsHighSurrogate(question[maxCharacters - 1]) ? maxCharacters - 1 : maxCharacters;
        return question[..length].TrimEnd() + "…";
    }
}