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
}