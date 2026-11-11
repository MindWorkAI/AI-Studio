namespace AIStudio.Tools.Mathematics;

/// <summary>
/// What a numeric check found.
/// </summary>
public enum NumericCheckOutcome
{
    /// <summary>
    /// Too few sample points had a value on both sides, or one side cannot be evaluated as a number.
    /// </summary>
    NOT_POSSIBLE = 0,

    /// <summary>
    /// Both sides agree at every sample point where both have a value.
    /// </summary>
    PASSED,

    /// <summary>
    /// The sides differ at a sample point.
    /// </summary>
    FAILED,
}