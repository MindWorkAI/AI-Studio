using System.Globalization;

using AIStudio.Chat;

using AngouriMath;

using PeterO.Numbers;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Refuses the computations which AngouriMath cannot stop once they started.
/// </summary>
/// <remarks>
/// AngouriMath computes an exact power or factorial in full, and an expansion term by term, without
/// checking its cancellation token meanwhile: 9^9^9, (10^9)!, and the expansion of (x + y)^500 run
/// on long past any budget, see AngouriMathBehaviorTests. The guards estimate the size of such a
/// result from the expression alone, before anything is computed, and refuse it when it would be
/// larger than any result the tools return anyway.<br/><br/>
/// The estimate works from the inside out, so the inner power of 9^9^9^9 is known before the outer
/// one is judged. Only numbers of known value count: a power of a variable is never computed as a
/// number. Call the guards inside a computation of MathComputationService, since they evaluate the
/// exponents and the arguments of factorials, which have passed the guards before.
/// </remarks>
public static class MathGuards
{
    private const int MAX_SHOWN_LENGTH = 120;

    /// <summary>
    /// Refuses an expression whose exact evaluation would produce a number of more than
    /// MathLimits.MAX_EXACT_DIGITS digits.
    /// </summary>
    /// <param name="expression">The expression to be evaluated or simplified.</param>
    /// <exception cref="MathComputationException">A part of the expression would grow too large.</exception>
    public static void CheckExactSize(Entity expression) => EstimateDigits(expression);

    /// <summary>
    /// Refuses an expression whose expansion would take a power of a sum beyond
    /// MathLimits.MAX_EXPANSION_EXPONENT.
    /// </summary>
    /// <param name="expression">The expression to be expanded.</param>
    /// <exception cref="MathComputationException">A power of a sum has too large an exponent.</exception>
    public static void CheckExpansion(Entity expression)
    {
        foreach (var node in expression.Nodes)
        {
            if (node is not Entity.Powf { Base: Entity.Sumf or Entity.Minusf } power)
                continue;

            var exponent = ValueOf(power.Exponent);
            if (exponent is null || Math.Abs(exponent.Value) <= MathLimits.MAX_EXPANSION_EXPONENT)
                continue;

            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(power)}' raises a sum to the power of {exponent.Value:0.###}. Expanding is limited to exponents up to {MathLimits.MAX_EXPANSION_EXPONENT}, since AngouriMath cannot stop a larger expansion midway."));
        }
    }

    /// <summary>
    /// Estimates how many digits the exact value of a node needs, and refuses it beyond the limit.
    /// </summary>
    /// <remarks>
    /// An upper bound for sums, products, and quotients: the digits of numerator and denominator
    /// add up. A number which is not exact, such as a decimal of the current precision, needs none.
    /// </remarks>
    private static double EstimateDigits(Entity node)
    {
        var digits = node switch
        {
            Entity.Number.Rational rational => DigitsOf(rational.ERational.Numerator) + DigitsOf(rational.ERational.Denominator),
            Entity.Powf power => EstimatePower(power),
            Entity.Factorialf factorial => EstimateFactorial(factorial),
            _ => node.DirectChildren.Sum(EstimateDigits),
        };

        if (digits > MathLimits.MAX_EXACT_DIGITS)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(node)}' would have about {digits:N0} digits as an exact number. AngouriMath computes such a number in full and cannot stop midway, so exact numbers are limited to {MathLimits.MAX_EXACT_DIGITS:N0} digits."));

        return digits;
    }

    private static double EstimatePower(Entity.Powf power)
    {
        var baseDigits = EstimateDigits(power.Base);
        var exponentDigits = EstimateDigits(power.Exponent);
        if (power.Base.FreeVariables.Any())
            return baseDigits + exponentDigits;

        var exponent = ValueOf(power.Exponent);
        if (exponent is null)
            return baseDigits + exponentDigits;

        var magnitude = Math.Abs(exponent.Value);
        if (power.Base.Evaled is Entity.Number.Rational rational)
        {
            var size = Log10(rational.ERational.Numerator) + Log10(rational.ERational.Denominator);
            var digits = magnitude * size;
            if (digits > MathLimits.MAX_EXACT_DIGITS && rational.ERational.Sign > 0)
                throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(power)}' would have about {digits:N0} digits as an exact number; its decimal logarithm is about {exponent.Value * (Log10(rational.ERational.Numerator) - Log10(rational.ERational.Denominator)):0.###}. AngouriMath computes such a number in full and cannot stop midway, so exact numbers are limited to {MathLimits.MAX_EXACT_DIGITS:N0} digits."));

            return digits;
        }

        // A base which is exact without being rational, such as sqrt(2), can still become one, as 2^500000:
        return magnitude * Math.Max(baseDigits, 0);
    }

    private static double EstimateFactorial(Entity.Factorialf factorial)
    {
        var argumentDigits = EstimateDigits(factorial.Argument);
        var argument = ValueOf(factorial.Argument);
        if (argument is null)
            return argumentDigits;

        // Stirling: log10(n!) is about n * log10(n / e) + log10(2 * pi * n) / 2.
        var n = Math.Abs(argument.Value);
        if (n < 2)
            return argumentDigits + 1;

        return n * Math.Log10(n / Math.E) + Math.Log10(2 * Math.PI * n) / 2 + 1;
    }

    /// <summary>
    /// The value of a part without variables, as a double, or null when it has none.
    /// </summary>
    private static double? ValueOf(Entity part)
    {
        if (part.FreeVariables.Any())
            return null;

        var value = part.Evaled switch
        {
            Entity.Number.Real real => real.EDecimal.ToDouble(),
            Entity.Number.Complex complex => Math.Abs(complex.RealPart.EDecimal.ToDouble()) + Math.Abs(complex.ImaginaryPart.EDecimal.ToDouble()),
            _ => double.NaN,
        };

        return double.IsNaN(value) ? null : value;
    }

    private static double DigitsOf(EInteger value) => value.IsZero ? 1 : value.Abs().GetDigitCountAsInt64();

    /// <summary>
    /// The decimal logarithm of an integer's magnitude, also beyond the range of a double.
    /// </summary>
    private static double Log10(EInteger value)
    {
        var magnitude = value.Abs();
        if (magnitude.IsZero)
            return 0;

        var digits = magnitude.GetDigitCountAsInt64();
        if (digits <= 15)
            return Math.Log10(magnitude.ToInt64Checked());

        var leading = double.Parse(magnitude.ToString()[..15], CultureInfo.InvariantCulture);
        return Math.Log10(leading) + (digits - 15);
    }

    private static string Show(Entity node) => node.ToString().Shorten(MAX_SHOWN_LENGTH);
}