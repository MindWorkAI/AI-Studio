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
/// checking its cancellation token meanwhile: (10^9)! and the expansion of (x + y)^500 run on long
/// past any budget, and so did 9^9^9 up to version 2.6.0; since version 2.7.0, AngouriMath leaves a
/// power of more than sixteen million bits written instead. The guards estimate the size of such a
/// result from the expression alone, before anything is computed, and refuse it when it would be
/// larger than any result the tools return anyway.<br/><br/>
/// The estimate works from the inside out, so the inner power of 9^9^9^9 is known before the outer
/// one is judged. Only numbers of known value count: a power of a variable is never computed as a
/// number. Call the guards inside a computation of MathComputationService, since they evaluate the
/// exponents and the arguments of factorials, which have passed the guards before.<br/><br/>
/// The same holds for the special functions polygamma and gamma(a, z), which AngouriMath computes for as
/// long as their arguments ask, see MathLimits. CheckExactSize refuses them beyond their limits as well.
/// </remarks>
public static class MathGuards
{
    private const int MAX_SHOWN_LENGTH = 120;

    /// <summary>
    /// Refuses an expression whose exact evaluation would produce a number of more than
    /// MathLimits.MAX_EXACT_DIGITS digits, or which takes a special function beyond its limits.
    /// </summary>
    /// <param name="expression">The expression to be evaluated or simplified.</param>
    /// <exception cref="MathComputationException">A part of the expression would grow too large or take too long.</exception>
    public static void CheckExactSize(Entity expression) => EstimateDigits(expression, false);

    /// <summary>
    /// Refuses an expression whose variables have values, as at the sample points of NumericCheck, when one of its
    /// powers or factorials would have more than MathLimits.MAX_EXACT_DIGITS digits, or when it takes a special
    /// function beyond its limits.
    /// </summary>
    /// <remarks>
    /// A value of a variable may turn a part which CheckExactSize let through into one which runs on long past
    /// the budget: x = 0.61 makes (10^6 * x)! the factorial of 610,000, and x^100000 a fraction of hundreds of
    /// thousands of digits; in polygamma(1000 * x, 2), x becomes the order. Unlike CheckExactSize, it takes the
    /// largest of the parts of a sum, a product, or a quotient instead of adding up their digits: the checks
    /// evaluate long sums, such as the expansion of (x + 1)^200, whose terms each have few digits.
    /// </remarks>
    /// <param name="expression">The expression with the values of its variables in their place.</param>
    /// <exception cref="MathComputationException">A part of the expression would grow too large or take too long.</exception>
    public static void CheckValueAtPoint(Entity expression) => EstimateDigits(expression, true);

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

            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(power)}' raises a sum to the power of {exponent.Value:0.###}. Where the tool may expand, a sum is raised to exponents up to {MathLimits.MAX_EXPANSION_EXPONENT} only, since it cannot stop a larger expansion midway."));
        }
    }

    /// <summary>
    /// Estimates how many digits the exact value of a node needs, and refuses it beyond the limit.
    /// </summary>
    /// <remarks>
    /// An upper bound for sums, products, and quotients: the digits of numerator and denominator
    /// add up. A number which is not exact, such as a decimal of the current precision, needs none.
    /// </remarks>
    /// <param name="node">The node to be estimated.</param>
    /// <param name="largestPart">Whether a sum, a product, or a quotient counts the digits of its largest part only, see CheckValueAtPoint.</param>
    private static double EstimateDigits(Entity node, bool largestPart)
    {
        var digits = node switch
        {
            Entity.Number.Rational rational => DigitsOf(rational.ERational.Numerator) + DigitsOf(rational.ERational.Denominator),
            Entity.Powf power => EstimatePower(power, largestPart),
            Entity.Factorialf factorial => EstimateFactorial(factorial, largestPart),
            Entity.Polygammaf polygamma => EstimatePolygamma(polygamma, largestPart),
            Entity.IncompleteGammaf gamma => EstimateIncompleteGamma(gamma, largestPart),
            _ when largestPart => node.DirectChildren.Select(child => EstimateDigits(child, true)).DefaultIfEmpty(0).Max(),
            _ => node.DirectChildren.Sum(child => EstimateDigits(child, false)),
        };

        if (digits > MathLimits.MAX_EXACT_DIGITS)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(node)}' would have about {digits:N0} digits as an exact number. The tool computes such a number in full and cannot stop midway, so exact numbers are limited to {MathLimits.MAX_EXACT_DIGITS:N0} digits."));

        return digits;
    }

    private static double EstimatePower(Entity.Powf power, bool largestPart)
    {
        var baseDigits = EstimateDigits(power.Base, largestPart);
        var exponentDigits = EstimateDigits(power.Exponent, largestPart);
        if (MathVariables.HasAny(power.Base))
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
                throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(power)}' would have about {digits:N0} digits as an exact number; its decimal logarithm is about {exponent.Value * (Log10(rational.ERational.Numerator) - Log10(rational.ERational.Denominator)):0.###}. The tool computes such a number in full and cannot stop midway, so exact numbers are limited to {MathLimits.MAX_EXACT_DIGITS:N0} digits."));

            return digits;
        }

        // A base which is exact without being rational, such as sqrt(2), can still become one, as 2^500000:
        return magnitude * Math.Max(baseDigits, 0);
    }

    private static double EstimateFactorial(Entity.Factorialf factorial, bool largestPart)
    {
        var argumentDigits = EstimateDigits(factorial.Argument, largestPart);
        var argument = ValueOf(factorial.Argument);
        if (argument is null)
            return argumentDigits;

        // Stirling: log10(n!) is about n * log10(n / e) + log10(2 * pi * n) / 2.
        var n = Math.Abs(argument.Value);
        if (n < 2)
            return argumentDigits + 1;

        return n * Math.Log10(n / Math.E) + Math.Log10(2 * Math.PI * n) / 2 + 1;
    }

    private static double EstimatePolygamma(Entity.Polygammaf polygamma, bool largestPart)
    {
        var digits = EstimateDigits(polygamma.Order, largestPart) + EstimateDigits(polygamma.Argument, largestPart);
        var order = ValueOf(polygamma.Order);
        if (order > MathLimits.MAX_POLYGAMMA_ORDER)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(polygamma)}' has the order {order.Value:0.###}. The tool cannot stop the computation of polygamma midway, and its time grows with the order, so the order is limited to {MathLimits.MAX_POLYGAMMA_ORDER}."));

        return digits;
    }

    private static double EstimateIncompleteGamma(Entity.IncompleteGammaf gamma, bool largestPart)
    {
        var digits = EstimateDigits(gamma.Parameter, largestPart) + EstimateDigits(gamma.LowerLimit, largestPart);
        var argument = NumberOf(gamma.LowerLimit);
        if (argument is not null && AbsoluteValueOf(argument) > MathLimits.MAX_INCOMPLETE_GAMMA_ARGUMENT)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(gamma)}' takes gamma(a, z) at a z of about {AbsoluteValueOf(argument):0.###} in absolute value. The tool cannot stop this computation midway, and its time grows with z, so the absolute value of z is limited to {MathLimits.MAX_INCOMPLETE_GAMMA_ARGUMENT:N0}."));

        var parameter = NumberOf(gamma.Parameter);
        if (parameter is not null && !IsAllowedIncompleteGammaParameter(parameter))
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"'{Show(gamma)}' takes gamma(a, z) for a = {Show(parameter)}. The tool cannot stop this computation midway, and its time grows with z, the more so when a is negative or not real, so a is limited to the real numbers from {MathLimits.MIN_INCOMPLETE_GAMMA_PARAMETER} to {MathLimits.MAX_INCOMPLETE_GAMMA_PARAMETER}."));

        return digits;
    }

    private static bool IsAllowedIncompleteGammaParameter(Entity.Number.Complex parameter)
    {
        var real = parameter.RealPart.EDecimal.ToDouble();
        return parameter.ImaginaryPart.EDecimal.IsZero && real is >= MathLimits.MIN_INCOMPLETE_GAMMA_PARAMETER and <= MathLimits.MAX_INCOMPLETE_GAMMA_PARAMETER;
    }

    /// <summary>
    /// The value of a part without variables, as a number, or null when it has none.
    /// </summary>
    private static Entity.Number.Complex? NumberOf(Entity part) => MathVariables.HasAny(part) ? null : part.Evaled as Entity.Number.Complex;

    private static double AbsoluteValueOf(Entity.Number.Complex number) => double.Hypot(number.RealPart.EDecimal.ToDouble(), number.ImaginaryPart.EDecimal.ToDouble());

    /// <summary>
    /// The value of a part without variables, as a double, or null when it has none.
    /// </summary>
    private static double? ValueOf(Entity part)
    {
        if (MathVariables.HasAny(part))
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