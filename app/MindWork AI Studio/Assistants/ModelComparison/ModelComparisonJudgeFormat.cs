namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How a judge of the Model Comparison assistant names the two things it compares, and how it answers: the same JSON
/// for every judge, so that ModelComparisonJudgeVerdict reads them all.
/// </summary>
/// <remarks>
/// The judge of the stability mode compares two answers, "A" and "B", the columns the user sees. The judge of a
/// comparison series compares two chats, "1" and "2", the names the user sees. Either way, the judge refers to them by
/// these names only, so that its reasoning matches the screen, and never by a model.
/// </remarks>
/// <param name="Subject">What the judge compares, in the singular, e.g., "answer".</param>
/// <param name="NameKind">What kind of name tells the two apart, e.g., "letter".</param>
/// <param name="First">The name of the first one, which stands for COLUMN_A.</param>
/// <param name="Second">The name of the second one, which stands for COLUMN_B.</param>
public sealed record ModelComparisonJudgeFormat(string Subject, string NameKind, string First, string Second)
{
    /// <summary>
    /// Two answers, "A" and "B", as in the stability mode.
    /// </summary>
    public static readonly ModelComparisonJudgeFormat ANSWERS = new("answer", "letter", "A", "B");

    /// <summary>
    /// Two chats, "1" and "2", as in a comparison series.
    /// </summary>
    public static readonly ModelComparisonJudgeFormat CHATS = new("chat", "number", "1", "2");

    /// <summary>
    /// Tells the judge how to answer.
    /// </summary>
    /// <param name="languageName">The language of the reasoning, by its English name, or an empty text to leave it to the judge.</param>
    /// <returns>The instructions, as the last part of a prompt.</returns>
    public string AnswerInstructions(string languageName) => $$"""
        Respond with exactly one JSON object and nothing else, in this shape:

        {"preferred": "{{this.First}}", "reasoning": "A short explanation, a few sentences at most."}

        The value of "preferred" must be exactly one of "{{this.First}}", "{{this.Second}}", or "TIE". In the reasoning, refer to each {{this.Subject}} only by its {{this.NameKind}}, {{this.First}} or {{this.Second}}, never by a model name -- translated into whatever language you write the reasoning in, not forced into English.{{this.LanguageInstruction(languageName)}}
        """;

    /// <summary>
    /// Reads which one the judge preferred.
    /// </summary>
    /// <param name="preferred">The value of "preferred" as the judge wrote it.</param>
    /// <returns>COLUMN_A for the first one, COLUMN_B for the second one, TIE, or null for anything else.</returns>
    public ModelComparisonVote? VoteOf(string? preferred)
    {
        var value = preferred?.Trim().ToUpperInvariant();
        if (value == this.First.ToUpperInvariant())
            return ModelComparisonVote.COLUMN_A;

        if (value == this.Second.ToUpperInvariant())
            return ModelComparisonVote.COLUMN_B;

        return value == "TIE" ? ModelComparisonVote.TIE : null;
    }

    /// <summary>
    /// Tells the judge to decide by the expectations, values, and goals of the user, see
    /// ModelComparisonJudgeExpectations: without them, the judge would decide by what it learned in training.
    /// </summary>
    /// <param name="expectations">The expectations, values, and goals of the user, or an empty text when the user gave none.</param>
    /// <returns>A paragraph and a section of their own, to follow the criterion of the judge, or an empty text.</returns>
    public static string ExpectationsOfTheUser(string expectations) => expectations.Trim().Length == 0
        ? string.Empty
        : $"\n\nDecide by the expectations, values, and goals of the user below. They decide what counts as good here, before any preference of your own.\n\n# Expectations, values, and goals of the user\n{expectations.Trim()}";

    /// <summary>
    /// Adds an identifier to a prompt of a judge as a plainly labeled, ignorable line, so that a caching gateway does
    /// not answer a judge which is asked again about similar content from its cache, see ModelComparisonRequest.ToPrompt.
    /// </summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="cacheBuster">The identifier, or an empty text to leave the prompt as it is.</param>
    /// <returns>The prompt.</returns>
    public static string WithCacheBuster(string prompt, string cacheBuster) => cacheBuster.Length == 0
        ? prompt
        : $"{prompt}\n\n(Request ID: {cacheBuster}. This has no bearing on your verdict; it is only here so repeated requests are not answered from a cache.)";

    /// <summary>
    /// Tells the judge which language to write in, or nothing when no language was given.
    /// </summary>
    private string LanguageInstruction(string languageName) => languageName.Trim().Length == 0
        ? string.Empty
        : $" Write the reasoning in {languageName.Trim()}, regardless of the language of the {this.Subject}s.";
}