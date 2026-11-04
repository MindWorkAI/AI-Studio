namespace AIStudio.Tools.Mathematics;

/// <summary>
/// How a numerical integration ended.
/// </summary>
public enum QuadratureStatus
{
    /// <summary>
    /// The estimate settled within the tolerance; its estimated error says how far.
    /// </summary>
    CONVERGED = 0,

    /// <summary>
    /// The estimate did not settle: the integral may diverge or oscillate, or the integrand varies too
    /// much for the finest step. The value is the last estimate and must not be taken for the integral.
    /// </summary>
    NOT_CONVERGED,

    /// <summary>
    /// The integrand has no finite value at a point inside the interval, such as a pole.
    /// </summary>
    NO_VALUE,
}