namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The materials a person takes away from a preparation in the BeyondFacts Lab, in the last step.
/// </summary>
/// <remarks>
/// They are called results in the code, since BflMaterial already names what the person uploads. Each of them
/// is stored in a file of its own, see BflResultKindExtensions.FileName.
/// </remarks>
public enum BflResultKind
{
    /// <summary>
    /// All confirmed work states, the material, the open questions, and the revisions, put together word for word,
    /// without a model.
    /// </summary>
    REFLECTION_FOLDER,

    /// <summary>
    /// One page on what the person stands for and why.
    /// </summary>
    POSITION_PAPER,

    /// <summary>
    /// The opening, the key messages, questions to others, and wordings for uncertainty or disagreement.
    /// </summary>
    CONVERSATION_GUIDE,

    /// <summary>
    /// Critical questions from the test situation with approaches to answering them.
    /// </summary>
    QUESTIONS_AND_ANSWERS,

    /// <summary>
    /// What the person still has to research, clarify, or decide before the event.
    /// </summary>
    OPEN_POINTS,
}