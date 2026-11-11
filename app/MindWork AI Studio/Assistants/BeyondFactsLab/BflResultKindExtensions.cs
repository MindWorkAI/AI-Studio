namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflResultKindExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflResultKindExtensions).Namespace, nameof(BflResultKindExtensions));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflResultKindExtensions).Namespace, nameof(BflResultKindExtensions));

    /// <param name="kind">The kind of result.</param>
    extension(BflResultKind kind)
    {
        /// <summary>
        /// Gets the name of the result.
        /// </summary>
        public string Title => kind switch
        {
            BflResultKind.REFLECTION_FOLDER => TB("Reflection folder"),
            BflResultKind.POSITION_PAPER => TB("Position paper"),
            BflResultKind.CONVERSATION_GUIDE => TB("Conversation guide"),
            BflResultKind.QUESTIONS_AND_ANSWERS => TB("Questions and answers"),
            BflResultKind.OPEN_POINTS => TB("Open points and next steps"),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets what the result contains, for the info symbol beside its name.
        /// </summary>
        public string Description => kind switch
        {
            BflResultKind.REFLECTION_FOLDER => TA("All your confirmed work states, your material, the open questions, and your revisions, word for word. If you like, with the complete conversations as an appendix."),
            BflResultKind.POSITION_PAPER => TA("One page on what you stand for and why: your key statements, your reasons, the trade-offs between values, and the limits of your expertise."),
            BflResultKind.CONVERSATION_GUIDE => TA("The opening, your key messages, questions to others, and wordings for uncertainty or disagreement. You can adapt it for a talk, a pitch, or a hearing."),
            BflResultKind.QUESTIONS_AND_ANSWERS => TA("Critical questions from the test situation, your approaches to answering them, and the points on which you have no reliable answer."),
            BflResultKind.OPEN_POINTS => TA("What you still have to research, clarify with others, or decide yourself before you appear."),

            _ => string.Empty,
        };

        /// <summary>
        /// Tells whether a model writes the result. The reflection folder is put together from what the person
        /// confirmed, word for word.
        /// </summary>
        public bool IsWrittenByModel => kind is not BflResultKind.REFLECTION_FOLDER;

        /// <summary>
        /// Gets the name of the file in which a preparation keeps the result.
        /// </summary>
        /// <remarks>
        /// These names are stored on disk, so they never change.
        /// </remarks>
        public string FileName => kind switch
        {
            BflResultKind.REFLECTION_FOLDER => "reflection-folder.json",
            BflResultKind.POSITION_PAPER => "position-paper.json",
            BflResultKind.CONVERSATION_GUIDE => "conversation-guide.json",
            BflResultKind.QUESTIONS_AND_ANSWERS => "questions-and-answers.json",
            BflResultKind.OPEN_POINTS => "open-points.json",

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The result has no file."),
        };

        /// <summary>
        /// Gets every kind of result, in the order the last step shows them.
        /// </summary>
        public static IReadOnlyList<BflResultKind> All => Enum.GetValues<BflResultKind>();
    }
}