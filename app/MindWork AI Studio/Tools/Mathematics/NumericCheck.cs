using AngouriMath;

using PeterO.Numbers;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Checks numerically whether two expressions are equal, at fixed sample points.
/// </summary>
/// <remarks>
/// A symbolic answer is only as good as the library which gave it, and AngouriMath changes answers
/// between releases. The tools therefore check what they can: a simplification against its input, an
/// antiderivative by its derivative, a solution by inserting it.<br/><br/>
/// The sample points are exact rationals between 0.6 and 2.3, so a polynomial is evaluated exactly,
/// however much its terms cancel, and everything else at 50 digits. They are fixed, so the same
/// question gets the same answer every time; the providers cache a conversation from its beginning.
/// A point where either side has no value, such as a pole, is skipped. Agreement at the points
/// supports the equality; it does not prove it.<br/><br/>
/// A derivative is compared with the central difference of its function, over a step of 1e-20 at 60
/// digits. The integer parameter of periodic solutions, which AngouriMath names n_1, takes the values
/// -1, 0, and 1 instead, since such a solution holds at integers only.<br/><br/>
/// Call it inside a computation of MathComputationService.
/// </remarks>
public static class NumericCheck
{
    private const int SAMPLE_POINTS = 6;

    private const int MIN_VALID_POINTS = 3;

    private const int PRECISION = 50;

    /// <summary>
    /// The values the variables take, in hundredths: irregular, so that no point lies on a pattern
    /// such as a multiple of pi, and eleven of them, a prime, so that the variables of a point differ.
    /// </summary>
    private static readonly int[] SAMPLE_HUNDREDTHS = [61, 73, 89, 107, 119, 131, 147, 163, 179, 197, 223];

    private static readonly EDecimal TOLERANCE = EDecimal.FromString("1e-30");

    /// <summary>
    /// The precision of a derivative check: the central difference divides by its tiny step.
    /// </summary>
    private const int DERIVATIVE_PRECISION = 60;

    /// <summary>
    /// The step of the central difference: rounding at 60 digits costs about 1e-40 of it, and its own
    /// error, about the square of the step, 1e-40 as well.
    /// </summary>
    private static readonly Entity.Number.Rational DERIVATIVE_STEP = Entity.Number.Rational.Create(EInteger.One, EInteger.FromInt32(10).Pow(20));

    private static readonly Entity.Number.Rational TWO_DERIVATIVE_STEPS = Entity.Number.Rational.Create(EInteger.FromInt32(2), EInteger.FromInt32(10).Pow(20));

    private static readonly EDecimal DERIVATIVE_TOLERANCE = EDecimal.FromString("1e-20");

    /// <summary>
    /// Checks whether two expressions agree at every sample point where both have a value.
    /// </summary>
    /// <remarks>
    /// A condition of AngouriMath, as in x + 1 provided not x - 1 = 0, is left aside on both sides:
    /// the expressions are compared where they have a value.
    /// </remarks>
    /// <param name="left">One expression.</param>
    /// <param name="right">The other expression.</param>
    /// <returns>What the check found.</returns>
    public static NumericCheckOutcome AreEqual(Entity left, Entity right)
    {
        left = WithoutCondition(left);
        right = WithoutCondition(right);
        var variables = MathVariables.FreeIn(left).Concat(MathVariables.FreeIn(right)).Distinct().OrderBy(variable => variable.Name, StringComparer.Ordinal).ToArray();

        using var precision = MathS.Settings.DecimalPrecisionContext.Set(EContext.ForPrecision(PRECISION));
        var validPoints = 0;
        for (var point = 0; point < SAMPLE_POINTS; point++)
        {
            var assignment = SampleAssignment(point, variables);
            var leftValue = ValueAt(left, assignment);
            var rightValue = ValueAt(right, assignment);
            if (leftValue is null || rightValue is null)
                continue;

            validPoints++;
            if (!AreClose(leftValue, rightValue, TOLERANCE))
                return NumericCheckOutcome.FAILED;
        }

        return validPoints >= MIN_VALID_POINTS ? NumericCheckOutcome.PASSED : NumericCheckOutcome.NOT_POSSIBLE;
    }

    /// <summary>
    /// Checks whether one expression is the derivative of another, by a central difference at each sample point.
    /// </summary>
    /// <param name="derivative">The claimed derivative.</param>
    /// <param name="function">The function.</param>
    /// <param name="variable">The variable of the derivative.</param>
    /// <returns>What the check found.</returns>
    public static NumericCheckOutcome IsDerivativeOf(Entity derivative, Entity function, Entity.Variable variable)
    {
        derivative = WithoutCondition(derivative);
        function = WithoutCondition(function);
        var variables = MathVariables.FreeIn(derivative).Concat(MathVariables.FreeIn(function)).Append(variable).Distinct().OrderBy(free => free.Name, StringComparer.Ordinal).ToArray();

        using var precision = MathS.Settings.DecimalPrecisionContext.Set(EContext.ForPrecision(DERIVATIVE_PRECISION));
        var validPoints = 0;
        for (var point = 0; point < SAMPLE_POINTS; point++)
        {
            var assignment = SampleAssignment(point, variables);
            var at = assignment[variable];
            var claimed = ValueAt(derivative, assignment);
            var above = ValueAt(function, With(assignment, variable, (at + DERIVATIVE_STEP).InnerSimplified));
            var below = ValueAt(function, With(assignment, variable, (at - DERIVATIVE_STEP).InnerSimplified));
            if (claimed is null || above is null || below is null)
                continue;

            var difference = NumericEvaluator.EvaluateAt(((Entity)above - below) / TWO_DERIVATIVE_STEPS, DERIVATIVE_PRECISION);
            validPoints++;
            if (!AreClose(claimed, difference, DERIVATIVE_TOLERANCE))
                return NumericCheckOutcome.FAILED;
        }

        return validPoints >= MIN_VALID_POINTS ? NumericCheckOutcome.PASSED : NumericCheckOutcome.NOT_POSSIBLE;
    }

    /// <summary>
    /// Whether AngouriMath named a variable as the integer parameter of periodic solutions, as in pi / 6 + 2 * pi * n_1.
    /// </summary>
    /// <param name="variable">The variable.</param>
    /// <returns>True for n_1, n_2, and so on.</returns>
    public static bool IsIntegerParameter(Entity.Variable variable) => variable.Name.StartsWith("n_", StringComparison.Ordinal) && variable.Name.Length > 2 && variable.Name[2..].All(char.IsAsciiDigit);

    /// <summary>
    /// The values of the variables at one sample point.
    /// </summary>
    /// <param name="point">The number of the point, from zero.</param>
    /// <param name="variables">The variables, in a fixed order.</param>
    /// <returns>Each variable with its exact value.</returns>
    public static IReadOnlyDictionary<Entity.Variable, Entity> SampleAssignment(int point, IReadOnlyList<Entity.Variable> variables)
    {
        var assignment = new Dictionary<Entity.Variable, Entity>(variables.Count);
        for (var index = 0; index < variables.Count; index++)
        {
            // An integer parameter takes -1, 0, and 1, since a periodic solution holds only at integers:
            if (IsIntegerParameter(variables[index]))
            {
                assignment[variables[index]] = Entity.Number.Integer.Create((point + index) % 3 - 1);
                continue;
            }

            var hundredths = SAMPLE_HUNDREDTHS[(point * 3 + index * 5) % SAMPLE_HUNDREDTHS.Length];
            assignment[variables[index]] = Entity.Number.Rational.Create(hundredths, 100);
        }

        return assignment;
    }

    private static Dictionary<Entity.Variable, Entity> With(IReadOnlyDictionary<Entity.Variable, Entity> assignment, Entity.Variable variable, Entity value) => new(assignment) { [variable] = value };

    /// <summary>
    /// The value of an expression with its variables replaced, or null when it has none there.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="assignment">The value of each variable.</param>
    /// <returns>The value, or null for no value, an infinite one, or one AngouriMath cannot evaluate.</returns>
    public static Entity.Number.Complex? ValueAt(Entity expression, IReadOnlyDictionary<Entity.Variable, Entity> assignment)
    {
        try
        {
            var substituted = expression;
            foreach (var (variable, value) in assignment)
                substituted = substituted.Substitute(variable, value);

            if (!substituted.EvaluableNumerical)
                return null;

            //
            // A polynomial at a sample point stays exact. Anything else is evaluated without the downcasting which turns
            // tiny values into 0, and so is an exact 0: simplifying takes a tiny value such as 6.62607015e-34 * sin(0.61)
            // for 0 at this precision, see AngouriMathBehaviorTests:
            //
            var exact = NumericEvaluator.ExactValueOf(substituted);
            var number = exact is not null && !IsZero(exact) ? exact : NumericEvaluator.EvaluateAt(substituted, MathS.Settings.DecimalPrecisionContext.Value.Precision.ToInt32Checked());
            return IsFinite(number) ? number : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static Entity WithoutCondition(Entity expression) => expression is Entity.Providedf provided ? provided.Expression : expression;

    private static bool IsFinite(Entity.Number.Complex number) => IsFinite(number.RealPart.EDecimal) && IsFinite(number.ImaginaryPart.EDecimal);

    private static bool IsZero(Entity.Number.Complex number) => number.RealPart.EDecimal.IsZero && number.ImaginaryPart.EDecimal.IsZero;

    private static bool IsFinite(EDecimal value) => !value.IsNaN() && !value.IsInfinity();

    private static bool AreClose(Entity.Number.Complex left, Entity.Number.Complex right, EDecimal tolerance)
    {
        var context = EContext.ForPrecision(PRECISION);
        var difference = left.RealPart.EDecimal.Subtract(right.RealPart.EDecimal, context).Abs().Add(left.ImaginaryPart.EDecimal.Subtract(right.ImaginaryPart.EDecimal, context).Abs(), context);
        var leftSize = left.RealPart.EDecimal.Abs().Add(left.ImaginaryPart.EDecimal.Abs(), context);
        var rightSize = right.RealPart.EDecimal.Abs().Add(right.ImaginaryPart.EDecimal.Abs(), context);

        //
        // Against 0, the tolerance is absolute: an expression whose terms cancel, such as sin(2*x) - 2*sin(x)*cos(x),
        // comes only as close as its rounding noise. Between two other values, it is relative, so that tiny values,
        // such as those of a formula with the Planck constant, are still told apart:
        //
        var scale = leftSize.IsZero || rightSize.IsZero ? EDecimal.One : EDecimal.Max(leftSize, rightSize);
        return difference.CompareTo(tolerance.Multiply(scale, context)) <= 0;
    }
}