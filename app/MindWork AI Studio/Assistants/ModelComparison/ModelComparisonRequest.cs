namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What both models are asked: an optional context, such as a document, and the question about it.
/// </summary>
/// <remarks>
/// Both models get exactly the text <see cref="ToPrompt"/> returns, in the same shape, and nothing
/// else. That is what makes the answers comparable: a difference in the wording of a prompt would
/// count as a difference between the models.
/// </remarks>
public sealed record ModelComparisonRequest
{
    /// <summary>
    /// Creates the request from what the user entered. Leading and trailing white space is dropped,
    /// so a stray line break at the end of a pasted document does not count as text.
    /// </summary>
    /// <param name="context">The context, or an empty text when there is none.</param>
    /// <param name="question">The question or task.</param>
    /// <param name="languageName">
    /// The language both models are asked to answer in, by its English name (for example
    /// "German"), or an empty text to leave the language to the models themselves.
    /// </param>
    public ModelComparisonRequest(string context, string question, string languageName = "")
    {
        this.Context = context.Trim();
        this.Question = question.Trim();
        this.LanguageName = languageName.Trim();
    }

    public string Context { get; }

    public string Question { get; }

    public string LanguageName { get; }

    /// <summary>
    /// Shortens the question to something which fits into a summary, and marks the cut.
    /// </summary>
    /// <param name="maxCharacters">The number of characters of the question to keep, at most.</param>
    /// <returns>The question, or its beginning followed by an ellipsis.</returns>
    public string GetQuestionPreview(int maxCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);

        if (this.Question.Length <= maxCharacters)
            return this.Question;

        // Cutting between the two halves of a character outside the basic plane, an emoji for example, would leave half of it:
        var length = char.IsHighSurrogate(this.Question[maxCharacters - 1]) ? maxCharacters - 1 : maxCharacters;
        return this.Question[..length].TrimEnd() + "…";
    }

    /// <summary>
    /// Builds the prompt. Without a context, it is the question alone: headings around a lone
    /// question would only give the models something to comment on.
    /// </summary>
    /// <param name="cacheBuster">
    /// An identifier to append as a plainly labeled, ignorable line, or an empty text to leave the
    /// prompt exactly as asked. Comparing a model against itself relies on asking it more than once,
    /// and a byte-identical prompt is exactly what a caching gateway between here and the model --
    /// LiteLLM among them -- is built to recognize and answer from its cache instead of asking again.
    /// A fixed request would silently stop being independent samples without this.
    /// </param>
    /// <returns>The user prompt both models receive.</returns>
    public string ToPrompt(string cacheBuster = "")
    {
        var body = this.Context.Length == 0
            ? this.Question
            : $"""
               # Context
               {this.Context}

               # Question or task
               {this.Question}
               """;

        var withLanguage = this.LanguageName.Length == 0
            ? body
            : $"{body}\n\nAnswer in {this.LanguageName}.";

        return cacheBuster.Length == 0
            ? withLanguage
            : $"{withLanguage}\n\n(Request ID: {cacheBuster}. This has no bearing on your answer; it is only here so repeated requests are not answered from a cache.)";
    }
}