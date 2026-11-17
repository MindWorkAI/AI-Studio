namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How much room a chat of the BeyondFacts Lab has left in the context of its model.
/// </summary>
public enum BflTokenBudgetState
{
    /// <summary>
    /// Nobody knows: the count failed, or the model has no statement about its context.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// Room enough.
    /// </summary>
    OK,

    /// <summary>
    /// Most of the context is used; the person should think about starting over.
    /// </summary>
    WARNING,

    /// <summary>
    /// The next message and its answer would not fit anymore, so the lab sends no more.
    /// </summary>
    FULL,
}