namespace AIStudio.Models;

/// <summary>
/// States how a model reasons.
/// </summary>
/// <remarks>
/// This is the resolved answer to a question the capability flags could only ask three times at
/// once. A model reasons in exactly one of these ways, so one value says it, and the combinations
/// which contradict each other cannot be written down any more.
///
/// The user interface has always thought in these terms: the expert dialog offers "no reasoning",
/// "can be enabled", "on by default", and "always on", and used to recompute them from three flags
/// on every render.
/// </remarks>
public enum ReasoningSupport
{
    /// <summary>
    /// The model does not reason. This is the answer for everything we have no statement about.
    /// </summary>
    NONE,

    /// <summary>
    /// The model can reason, but only when the request asks it to.
    /// </summary>
    OPTIONAL,

    /// <summary>
    /// The model reasons unless the request turns it off.
    /// </summary>
    ON_BY_DEFAULT,

    /// <summary>
    /// The model always reasons. There is no way to turn it off.
    /// </summary>
    ALWAYS,
}