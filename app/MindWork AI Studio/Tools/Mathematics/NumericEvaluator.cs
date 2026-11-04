using System.Globalization;

using AIStudio.Chat;

using AngouriMath;

using PeterO.Numbers;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Evaluates an expression without variables to a number, exactly where it can, and states how many
/// of its digits are certain.
/// </summary>
/// <remarks>
/// A rational value stays exact, so 547 / 57576 comes back as that fraction. Every other value is
/// evaluated twice, at ten and at twenty digits beyond those asked for, and only the digits both agree
/// on are reported: AngouriMath rounds at every step, and a compound expression such as pi^4/15 loses
/// its last digits at the precision asked for, see AngouriMathBehaviorTests.<br/><br/>
/// A definite integral is integrated numerically, never by AngouriMath: its antiderivative may jump
/// between the bounds, and Evaled turns the constants inside an integral it cannot take into decimals.
/// Its value then carries the digits of a double at most. The two evaluations shift every integral
/// by its estimated error, down in one and up in the other, so the digits they agree on are those the
/// integrals vouch for, too.<br/><br/>
/// Call it inside a computation of MathComputationService.
/// </remarks>
public static class NumericEvaluator
{
    private const int GUARD_DIGITS = 10;

    private const int MAX_SHOWN_LENGTH = 120;

    /// <summary>
    /// Evaluates an expression to a number.
    /// </summary>
    /// <param name="expression">The expression, read by MathExpressionReader.</param>
    /// <param name="digits">How many significant digits are asked for.</param>
    /// <param name="token">The cancellation token of the computation.</param>
    /// <returns>The value with its certain digits.</returns>
    /// <exception cref="MathComputationException">The expression has no numeric value, or cannot be integrated.</exception>
    public static NumericValue Evaluate(Entity expression, int digits, CancellationToken token)
    {
        RefuseWhatIsNoNumber(expression);
        MathGuards.CheckExactSize(expression);

        var integrals = new List<NumericIntegral>();
        var issues = new List<string>();
        var withPlaceholders = expression.Replace(node => node is Entity.Integralf integral ? Integrate(integral, integrals, issues, token) : node);

        var exact = integrals.Count == 0 ? ExactValueOf(expression) : null;
        var central = At(Substituted(withPlaceholders, integrals, 0), digits + GUARD_DIGITS);
        if (IsNaN(central))
            throw new MathComputationException($"'{Show(expression)}' has no value: AngouriMath evaluates it to NaN, as for a division by zero, a pole, or the factorial of a negative integer.");

        if (IsInfinite(central))
        {
            issues.Add("The value is infinite.");
            return new(null, central, 0, integrals.Count > 0, null, issues);
        }

        if (exact is not null)
            return new(exact, central, digits, false, null, issues);

        var low = integrals.Count == 0 ? central : At(Substituted(withPlaceholders, integrals, -1), digits + GUARD_DIGITS);
        var high = At(Substituted(withPlaceholders, integrals, 1), digits + 2 * GUARD_DIGITS);
        var certainDigits = MathFormatting.CountMatchingDigits(low, high, digits);
        double? estimatedError = integrals.Count == 0 ? null : MathFormatting.Distance(low, high) / 2;
        if (certainDigits == 0)
            issues.Add(string.Create(CultureInfo.InvariantCulture, $"No digit of the value is certain: it lies within about {MathFormatting.FormatDecimal(MathFormatting.Distance(low, high), 2)} of the decimal."));
        else if (certainDigits < digits)
            issues.Add(string.Create(CultureInfo.InvariantCulture, $"Only {certainDigits} of the {digits} significant digits asked for are certain, so the decimal has {certainDigits}."));

        return new(null, central, certainDigits, integrals.Count > 0, estimatedError, issues);
    }

    private static void RefuseWhatIsNoNumber(Entity expression)
    {
        if (expression is Entity.Statement)
            throw new MathComputationException($"'{Show(expression)}' is a statement, such as a comparison, and has no numeric value. An equation with one '=' is checked by comparing its sides.");

        if (expression is Entity.Set)
            throw new MathComputationException($"'{Show(expression)}' is a set, and has no numeric value.");

        var variables = MathVariables.FreeIn(expression).Select(variable => variable.Name).ToList();
        if (variables.Count > 0)
            throw new MathComputationException($"'{Show(expression)}' contains the variable{(variables.Count == 1 ? string.Empty : "s")} {string.Join(", ", variables)}, so it has no numeric value. compute_symbolically works with variables.");
    }

    /// <summary>
    /// The exact value of an expression when it is rational, or complex with rational parts.
    /// </summary>
    private static Entity.Number? ExactValueOf(Entity expression) => expression.InnerSimplified switch
    {
        Entity.Number.Rational rational => rational,
        Entity.Number.Complex { RealPart: Entity.Number.Rational, ImaginaryPart: Entity.Number.Rational } complex => complex,
        _ => null,
    };

    /// <summary>
    /// Integrates a definite integral numerically, and puts a variable of its own in its place.
    /// </summary>
    private static Entity Integrate(Entity.Integralf integral, List<NumericIntegral> integrals, List<string> issues, CancellationToken token)
    {
        if (integral.Range is not { } range)
            throw new MathComputationException($"'{Show(integral)}' has no bounds, so it has no numeric value. compute_symbolically integrates without bounds.");

        var (from, to) = range;

        if (integral.Var is not Entity.Variable variable)
            throw new MathComputationException($"'{Show(integral)}' does not integrate over a variable.");

        if (MathVariables.FreeIn(integral.Expression).Any(free => free != variable) || MathVariables.HasAny(from) || MathVariables.HasAny(to))
            throw new MathComputationException($"'{Show(integral)}' depends on the variable of an outer integral. Nested integrals are not integrated numerically.");

        var lower = BoundOf(from, integral);
        var upper = BoundOf(to, integral);
        Func<double, double> integrand;
        try
        {
            integrand = integral.Expression.Compile<double, double>(variable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new MathComputationException($"The integrand of '{Show(integral)}' cannot be evaluated numerically: {exception.Message.Shorten(MAX_SHOWN_LENGTH)}");
        }

        var result = DoubleExponentialQuadrature.Integrate(integrand, lower, upper, token);
        switch (result.Status)
        {
            case QuadratureStatus.NO_VALUE:
                throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"The integrand of '{Show(integral)}' has no finite value at {variable.Name} = {MathFormatting.FormatDecimal(result.FailurePoint ?? double.NaN, 6)}, inside the interval of integration."));

            case QuadratureStatus.NOT_CONVERGED:
                throw new MathComputationException($"The numerical integration of '{Show(integral)}' did not settle. The integral may diverge, converge only conditionally such as sin(x)/x over [0, +oo), or have a singularity inside the interval, which a split at the singularity moves to a bound.");
        }

        // The expression has no free variables at this point, so no name can clash with one of them:
        var placeholder = MathS.Var($"Q_{integrals.Count + 1}");
        integrals.Add(new(placeholder, result.Value, Math.Max(result.EstimatedError, Math.Abs(result.Value) * Math.ScaleB(1.0, -52))));
        issues.Add(string.Create(CultureInfo.InvariantCulture, $"'{Show(integral)}' was integrated numerically, with an estimated error of {MathFormatting.FormatDecimal(result.EstimatedError, 2)}."));
        return placeholder;
    }

    private static double BoundOf(Entity bound, Entity.Integralf integral)
    {
        var value = bound.Evaled;
        if (value is not Entity.Number.Real real)
            throw new MathComputationException($"A bound of '{Show(integral)}' is not a real number.");

        var number = real.EDecimal;
        if (number.IsNaN())
            throw new MathComputationException($"A bound of '{Show(integral)}' has no value.");

        if (number.IsInfinity())
            return number.IsNegative ? double.NegativeInfinity : double.PositiveInfinity;

        return number.ToDouble();
    }

    /// <summary>
    /// The expression with every integral replaced by its value, shifted by its estimated error in the given direction.
    /// </summary>
    private static Entity Substituted(Entity expression, IReadOnlyList<NumericIntegral> integrals, int direction)
    {
        foreach (var integral in integrals)
            expression = expression.Substitute(integral.Placeholder, Entity.Number.Real.Create(EDecimal.FromDouble(integral.Value + direction * integral.Error)));

        return expression;
    }

    private static Entity.Number.Complex At(Entity expression, int precision)
    {
        using var context = MathS.Settings.DecimalPrecisionContext.Set(EContext.ForPrecision(precision));
        return expression.EvalNumerical();
    }

    private static bool IsNaN(Entity.Number.Complex number) => number.RealPart.EDecimal.IsNaN() || number.ImaginaryPart.EDecimal.IsNaN();

    private static bool IsInfinite(Entity.Number.Complex number) => number.RealPart.EDecimal.IsInfinity() || number.ImaginaryPart.EDecimal.IsInfinity();

    private static string Show(Entity expression) => expression.ToString().Shorten(MAX_SHOWN_LENGTH);

    /// <summary>
    /// A definite integral, integrated numerically, and the variable which stands for it.
    /// </summary>
    private sealed record NumericIntegral(Entity.Variable Placeholder, double Value, double Error);
}