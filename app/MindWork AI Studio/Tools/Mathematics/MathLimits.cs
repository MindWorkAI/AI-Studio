namespace AIStudio.Tools.Mathematics;

/// <summary>
/// The limits of the Mathematics tools, in one place.
/// </summary>
/// <remarks>
/// AI Studio also runs on a Raspberry Pi, and AngouriMath cannot stop every computation midway. The
/// limits keep a single call from taking the device: they bound what is read, what is computed
/// exactly, and what goes back to the model. "Mathematics" in documentation/Tools.md lists them.
/// </remarks>
public static class MathLimits
{
    /// <summary>
    /// The longest expression read, in characters.
    /// </summary>
    /// <remarks>
    /// It also bounds how deeply an expression can nest: 1,000 levels of parentheses at most, which
    /// the stack of a computation holds, see MathComputationService.
    /// </remarks>
    public const int MAX_EXPRESSION_LENGTH = 2_000;

    /// <summary>
    /// The longest result returned, in characters, for each form of it.
    /// </summary>
    public const int MAX_RESULT_LENGTH = 20_000;

    /// <summary>
    /// The most digits an exact number may have while it is computed.
    /// </summary>
    /// <remarks>
    /// AngouriMath computes an exact power or factorial in full and checks no cancellation meanwhile,
    /// so MathGuards refuses one which would grow beyond this, before it starts.
    /// </remarks>
    public const int MAX_EXACT_DIGITS = 20_000;

    /// <summary>
    /// The largest exponent of a sum which may be expanded.
    /// </summary>
    /// <remarks>
    /// (x + y)^200 expands in a few milliseconds into about 13,000 characters, while (x + y)^500 runs
    /// on without checking for cancellation.
    /// </remarks>
    public const int MAX_EXPANSION_EXPONENT = 200;

    /// <summary>
    /// The most significant digits a decimal result may have.
    /// </summary>
    public const int MAX_DIGITS = 100;

    /// <summary>
    /// The highest order n of polygamma(n, x) which is computed.
    /// </summary>
    /// <remarks>
    /// AngouriMath computes polygamma without checking its cancellation token, and its time grows with the
    /// order: on a Mac, with 100 digits, order 100 takes 0.2 seconds at most, order 1,000 up to 1.5 seconds,
    /// and order 3,000 over 2 seconds. A derivative of order 10 of digamma is polygamma(10, x).
    /// </remarks>
    public const int MAX_POLYGAMMA_ORDER = 100;

    /// <summary>
    /// The largest absolute value of z in the incomplete gamma function gamma(a, z) which is computed.
    /// </summary>
    /// <remarks>
    /// AngouriMath computes gamma(a, z) without checking its cancellation token, and its time grows with z, the
    /// more so for a parameter a which is negative or not real. Within the limits of z and a, it takes
    /// 0.5 seconds at most on a Mac, with 100 digits, as for gamma(-1/2, 1000), while gamma(-199/2, 100)
    /// takes 2 seconds, gamma(-14 + 14*i, 1000) 1.3 seconds, gamma(i, 2000) 3 seconds, and
    /// gamma(10000, 10000) longer than the time budget.
    /// </remarks>
    public const int MAX_INCOMPLETE_GAMMA_ARGUMENT = 1_000;

    /// <summary>
    /// The smallest parameter a of gamma(a, z) which is computed; a has to be real.
    /// </summary>
    public const int MIN_INCOMPLETE_GAMMA_PARAMETER = -20;

    /// <summary>
    /// The largest parameter a of gamma(a, z) which is computed; a has to be real.
    /// </summary>
    public const int MAX_INCOMPLETE_GAMMA_PARAMETER = 200;
}