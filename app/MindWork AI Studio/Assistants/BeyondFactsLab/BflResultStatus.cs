namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where a result of a preparation stands, as the header of its panel shows it.
/// </summary>
public enum BflResultStatus
{
    /// <summary>
    /// This version of AI Studio cannot open the result.
    /// </summary>
    UNREADABLE,

    /// <summary>
    /// The result has neither a draft nor a confirmed version.
    /// </summary>
    NOT_CREATED,

    /// <summary>
    /// The model writes the result right now.
    /// </summary>
    WRITING,

    /// <summary>
    /// The result has a draft the person has not confirmed yet.
    /// </summary>
    DRAFT,

    /// <summary>
    /// The person confirmed the result, and it still fits the work states.
    /// </summary>
    CONFIRMED,

    /// <summary>
    /// The person confirmed the result, but a work state changed since.
    /// </summary>
    OUTDATED,
}