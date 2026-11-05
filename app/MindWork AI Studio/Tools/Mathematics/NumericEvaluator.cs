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
/// A tiny value, such as one computed with the Planck constant, gets as many more digits as it has
/// leading zeros, see EvaluateAt.<br/><br/>
/// Call it inside a computation of MathComputationService.
/// </remarks>
public static class NumericEvaluator
{
    private const int GUARD_DIGITS = 10;

    private const int MAX_SHOWN_LENGTH = 120;

    /// <summary>
    /// The most digits an evaluation computes with. A value which needs more, because it is smaller
    /// than about 1e-900, loses its digits.
    /// </summary>
    private const int MAX_PRECISION = 1_000;

    /// <summary>
    /// The precision of a bound of an integral, which ends up as a double.
    /// </summary>
    private const int BOUND_PRECISION = 30;

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
        if (exact is not null && IsZero(exact) && MayBeTiny(expression, digits))
            exact = null;

        if (exact is not null)
            return new(exact, DecimalOf(exact, digits + GUARD_DIGITS), digits, false, null, issues);

        var central = EvaluateAt(Substituted(withPlaceholders, integrals, 0), digits + GUARD_DIGITS);
        if (IsNaN(central))
            throw new MathComputationException($"'{Show(expression)}' has no value: AngouriMath evaluates it to NaN, as for a division by zero, a pole, or the factorial of a negative integer.");

        if (IsInfinite(central))
        {
            issues.Add("The value is infinite.");
            return new(null, central, 0, integrals.Count > 0, null, issues);
        }

        //
        // Where AngouriMath rounds to decimal places, a value far below 1 keeps only some of its digits, and a
        // tiny one none at all. The precision therefore grows by the leading zeros of the value, and a value of
        // zero is evaluated again with twice the digits, until MAX_PRECISION. More digits cannot change an
        // integral's value of zero, which comes from the quadrature:
        //
        var shift = 0;
        var maxShift = MAX_PRECISION - digits - 2 * GUARD_DIGITS;
        while (true)
        {
            int wantedShift;
            if (!IsZero(central))
                wantedShift = LeadingZerosOf(central);
            else if (integrals.Count == 0)
                wantedShift = 2 * shift + digits + GUARD_DIGITS;
            else
                break;

            wantedShift = Math.Min(wantedShift, maxShift);
            if (wantedShift <= shift + GUARD_DIGITS)
                break;

            token.ThrowIfCancellationRequested();
            shift = wantedShift;
            central = EvaluateAt(Substituted(withPlaceholders, integrals, 0), digits + GUARD_DIGITS + shift);
        }

        var low = integrals.Count == 0 ? central : EvaluateAt(Substituted(withPlaceholders, integrals, -1), digits + GUARD_DIGITS + shift);
        var high = EvaluateAt(Substituted(withPlaceholders, integrals, 1), digits + 2 * GUARD_DIGITS + shift);
        var certainDigits = MathFormatting.CountMatchingDigits(low, high, digits);
        double? estimatedError = integrals.Count == 0 ? null : MathFormatting.Distance(low, high) / 2;
        if (IsZero(central) && integrals.Count == 0)
        {
            certainDigits = 0;
            issues.Add(string.Create(CultureInfo.InvariantCulture, $"AngouriMath evaluates the value to 0 with {digits + GUARD_DIGITS + shift} digits but cannot show that it is exactly 0: a value too small for that many digits comes out as 0, too."));
        }
        else if (certainDigits == 0)
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
    /// <param name="expression">The expression.</param>
    /// <returns>The exact value, or null for an expression without one.</returns>
    public static Entity.Number.Complex? ExactValueOf(Entity expression) => expression.InnerSimplified switch
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
        var value = EvaluateAt(bound, BOUND_PRECISION);
        if (!value.ImaginaryPart.EDecimal.IsZero)
            throw new MathComputationException($"A bound of '{Show(integral)}' is not a real number.");

        var number = value.RealPart.EDecimal;
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
        // Downcasting would turn the value of a tiny integral into 0, see EvaluateAt:
        using var withoutDowncasting = MathS.Settings.DowncastingEnabled.Set(false);
        foreach (var integral in integrals)
            expression = expression.Substitute(integral.Placeholder, Entity.Number.Real.Create(EDecimal.FromDouble(integral.Value + direction * integral.Error)));

        return expression;
    }

    /// <summary>
    /// Evaluates an expression without variables to a number, with at least the given number of significant digits.
    /// </summary>
    /// <remarks>
    /// By default, AngouriMath downcasts: it turns a number which looks like an integer at the precision into that
    /// integer, and so a tiny value such as 1.380649e-23 * 300 into 0, see AngouriMathBehaviorTests. This evaluation
    /// does without. Then AngouriMath rounds the quotient of two integers to as many decimal places as the precision
    /// has digits, so 21/10^50 vanishes at 30 digits. The precision therefore grows by the leading zeros of the
    /// smallest fraction in the expression, up to MAX_PRECISION.
    /// </remarks>
    /// <param name="expression">The expression, without variables.</param>
    /// <param name="precision">How many significant digits to compute with, at least.</param>
    /// <returns>The value, which may be NaN or infinite.</returns>
    public static Entity.Number.Complex EvaluateAt(Entity expression, int precision)
    {
        precision = Math.Min(precision + LeadingZerosOfFractions(expression), MAX_PRECISION);
        using var context = MathS.Settings.DecimalPrecisionContext.Set(EContext.ForPrecision(precision));
        using var withoutDowncasting = MathS.Settings.DowncastingEnabled.Set(false);
        return expression.EvalNumerical();
    }

    /// <summary>
    /// Whether an exact 0 may stand for a tiny value instead.
    /// </summary>
    /// <remarks>
    /// AngouriMath simplifies a value below about 1e-1000 to 0, e.g., pi/10^1500, see AngouriMathBehaviorTests.
    /// A fraction that small, such as 1e-2000, leaves the 0 in doubt. Otherwise, a true 0, as for sin(pi),
    /// evaluates to rounding noise which changes with the precision, but a tiny value, as for exp(-3000), keeps
    /// its digits. A quotient such as pi/10^1500 evaluates to 0 as well, since AngouriMath rounds it to decimal
    /// places; its 0 stands.
    /// </remarks>
    private static bool MayBeTiny(Entity expression, int digits)
    {
        if (LeadingZerosOfFractions(expression) > MAX_PRECISION - digits - 2 * GUARD_DIGITS)
            return true;

        var value = EvaluateAt(expression, digits + GUARD_DIGITS);
        var check = EvaluateAt(expression, digits + 2 * GUARD_DIGITS);
        return !IsZero(value) && MathFormatting.CountMatchingDigits(value, check, GUARD_DIGITS) >= GUARD_DIGITS / 2;
    }

    /// <summary>
    /// How many zeros the smallest fraction of an expression has after its decimal point, e.g., 49 for 21/10^50.
    /// </summary>
    /// <remarks>
    /// The fractions are those the expression writes, such as 1e-200, and those its simplification computes, such as
    /// 21/10^50 from 3/10^50 * 7.
    /// </remarks>
    private static int LeadingZerosOfFractions(Entity expression) => expression.Nodes
        .Concat(expression.InnerSimplified.Nodes)
        .OfType<Entity.Number.Complex>()
        .SelectMany(number => new[] { number.RealPart, number.ImaginaryPart })
        .OfType<Entity.Number.Rational>()
        .Select(fraction => fraction.ERational.Denominator.ToString().Length - fraction.ERational.Numerator.Abs().ToString().Length)
        .Append(0)
        .Max();

    /// <summary>
    /// How many zeros a number has after its decimal point, e.g., 18 for 3.97e-19, and 0 for a number of at least 0.1.
    /// </summary>
    private static int LeadingZerosOf(Entity.Number.Complex number)
    {
        var real = number.RealPart.EDecimal;
        var imaginary = number.ImaginaryPart.EDecimal;
        var largest = real.Abs().CompareTo(imaginary.Abs()) >= 0 ? real : imaginary;
        if (largest.IsZero || largest.IsNaN() || largest.IsInfinity())
            return 0;

        var exponentOfFirstDigit = largest.Exponent.ToInt64Checked() + largest.UnsignedMantissa.ToString().Length - 1;
        return (int)Math.Clamp(-exponentOfFirstDigit - 1, 0, MAX_PRECISION);
    }

    private static bool IsZero(Entity.Number.Complex number) => number.RealPart.EDecimal.IsZero && number.ImaginaryPart.EDecimal.IsZero;

    /// <summary>
    /// An exact value as a decimal with the given number of significant digits.
    /// </summary>
    /// <remarks>
    /// AngouriMath converts a fraction to a fixed number of decimal places, so 1/10^200 would become 0,
    /// see AngouriMathBehaviorTests.
    /// </remarks>
    private static Entity.Number.Complex DecimalOf(Entity.Number.Complex exact, int precision)
    {
        var context = EContext.ForPrecision(precision);
        using var withoutDowncasting = MathS.Settings.DowncastingEnabled.Set(false);
        return Entity.Number.Complex.Create(Entity.Number.Real.Create(DecimalOf(exact.RealPart, context)), Entity.Number.Real.Create(DecimalOf(exact.ImaginaryPart, context)));
    }

    private static EDecimal DecimalOf(Entity.Number.Real part, EContext context) => part is Entity.Number.Rational fraction ? fraction.ERational.ToEDecimal(context) : part.EDecimal;

    private static bool IsNaN(Entity.Number.Complex number) => number.RealPart.EDecimal.IsNaN() || number.ImaginaryPart.EDecimal.IsNaN();

    private static bool IsInfinite(Entity.Number.Complex number) => number.RealPart.EDecimal.IsInfinity() || number.ImaginaryPart.EDecimal.IsInfinity();

    private static string Show(Entity expression) => expression.ToString().Shorten(MAX_SHOWN_LENGTH);

    /// <summary>
    /// A definite integral, integrated numerically, and the variable which stands for it.
    /// </summary>
    private sealed record NumericIntegral(Entity.Variable Placeholder, double Value, double Error);
}