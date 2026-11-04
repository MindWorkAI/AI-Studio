using System.Globalization;
using System.Text;

using AngouriMath;

using PeterO.Numbers;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Writes the results of the Mathematics tools for the model: as text, as LaTeX, and as decimals.
/// </summary>
/// <remarks>
/// Every number is written the same way whatever the language of the user, because it goes to a
/// model and comes back in the plain syntax of the tools.
/// </remarks>
public static class MathFormatting
{
    /// <summary>
    /// The smallest decimal exponent written without scientific notation.
    /// </summary>
    private const int MIN_PLAIN_EXPONENT = -6;

    /// <summary>
    /// The largest decimal exponent written without scientific notation.
    /// </summary>
    private const int MAX_PLAIN_EXPONENT = 20;

    /// <summary>
    /// An expression in the plain syntax which the tools also read.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="what">What the expression is, named when it is too long.</param>
    /// <returns>The expression as text.</returns>
    /// <exception cref="MathComputationException">The text is longer than MathLimits.MAX_RESULT_LENGTH.</exception>
    public static string ToText(Entity expression, string what) => EnsureLength(expression.Stringize(), what);

    /// <summary>
    /// An expression as LaTeX, which the chat renders.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="what">What the expression is, named when it is too long.</param>
    /// <returns>The expression as LaTeX.</returns>
    /// <exception cref="MathComputationException">The LaTeX is longer than MathLimits.MAX_RESULT_LENGTH.</exception>
    public static string ToLatex(Entity expression, string what) => EnsureLength(expression.Latexize(), what);

    /// <summary>
    /// Refuses a text longer than MathLimits.MAX_RESULT_LENGTH.
    /// </summary>
    /// <param name="text">The text of a result.</param>
    /// <param name="what">What the text is, named when it is too long.</param>
    /// <returns>The text.</returns>
    /// <exception cref="MathComputationException">The text is too long.</exception>
    public static string EnsureLength(string text, string what)
    {
        if (text.Length > MathLimits.MAX_RESULT_LENGTH)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"{what} has {text.Length:N0} characters, but at most {MathLimits.MAX_RESULT_LENGTH:N0} are returned."));

        return text;
    }

    /// <summary>
    /// A decimal, rounded to the given number of significant digits.
    /// </summary>
    /// <remarks>
    /// Trailing zeros are left out, so 0.5 stays 0.5 at twenty digits; how many digits the value
    /// carries is reported next to it. Between 1e-6 and 1e21 the decimal is written out, beyond that
    /// in scientific notation such as 6.02214076e+23.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="digits">How many significant digits to keep.</param>
    /// <returns>The decimal.</returns>
    public static string FormatDecimal(EDecimal value, int digits)
    {
        if (value.IsNaN())
            return "NaN";

        if (value.IsInfinity())
            return value.IsNegative ? "-oo" : "+oo";

        var rounded = value.RoundToPrecision(EContext.ForPrecisionAndRounding(digits, ERounding.HalfEven)).Reduce(null);
        if (rounded.IsZero)
            return "0";

        var mantissa = rounded.Mantissa.Abs().ToString();
        var exponent = rounded.Exponent.ToInt64Checked() + mantissa.Length - 1;
        if (exponent is >= MIN_PLAIN_EXPONENT and <= MAX_PLAIN_EXPONENT)
            return rounded.ToPlainString();

        var scientific = new StringBuilder();
        if (rounded.IsNegative)
            scientific.Append('-');

        scientific.Append(mantissa[0]);
        if (mantissa.Length > 1)
            scientific.Append('.').Append(mantissa, 1, mantissa.Length - 1);

        scientific.Append('e').Append(exponent < 0 ? '-' : '+').Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture));
        return scientific.ToString();
    }

    /// <summary>
    /// A double, rounded to the given number of significant digits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="digits">How many significant digits to keep, 17 at most.</param>
    /// <returns>The decimal.</returns>
    public static string FormatDecimal(double value, int digits)
    {
        if (double.IsNaN(value))
            return "NaN";

        if (double.IsInfinity(value))
            return value < 0 ? "-oo" : "+oo";

        return FormatDecimal(EDecimal.FromDouble(value), Math.Min(digits, 17));
    }

    /// <summary>
    /// How many significant digits two values of the same quantity have in common.
    /// </summary>
    /// <remarks>
    /// Counted from their relative difference, as is usual for correct digits: 0.99999 and 1.00001
    /// agree to four digits although no digit is the same. Evaluated at two working precisions, the
    /// digits they share are the ones the computation carries.
    /// </remarks>
    /// <param name="first">One value.</param>
    /// <param name="second">The other value.</param>
    /// <param name="maximum">The most digits to report, for values which are equal.</param>
    /// <returns>The number of digits, between 0 and maximum.</returns>
    public static int CountMatchingDigits(EDecimal first, EDecimal second, int maximum)
    {
        if (first.IsNaN() || second.IsNaN() || first.IsInfinity() || second.IsInfinity())
            return 0;

        if (first.CompareTo(second) == 0)
            return maximum;

        var context = EContext.ForPrecision(20);
        var scale = first.Abs().CompareTo(second.Abs()) >= 0 ? first.Abs() : second.Abs();
        var relative = first.Subtract(second, context).Abs().Divide(scale, context).ToDouble();
        if (relative <= 0)
            return maximum;

        return (int)Math.Clamp(Math.Floor(-Math.Log10(relative)), 0, maximum);
    }
}