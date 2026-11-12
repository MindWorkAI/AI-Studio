namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where the work state of a step stands, see BflStaleness.
/// </summary>
public enum BflWorkStateStatus
{
    /// <summary>
    /// The person has not confirmed a work state for the step.
    /// </summary>
    NOT_CONFIRMED,

    /// <summary>
    /// The person confirmed the work state, and nothing it builds on changed since.
    /// </summary>
    CONFIRMED,

    /// <summary>
    /// The person changed their details in the form of the step since they confirmed the work state.
    /// </summary>
    DETAILS_CHANGED,

    /// <summary>
    /// The work state of an earlier step changed since the person confirmed this one: it was confirmed for the first
    /// time, confirmed anew, or withdrawn.
    /// </summary>
    OUTDATED,
}