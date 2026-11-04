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
        var variables = left.FreeVariables.Concat(right.FreeVariables).Distinct().OrderBy(variable => variable.Name, StringComparer.Ordinal).ToArray();

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
            if (!AreClose(leftValue, rightValue))
                return NumericCheckOutcome.FAILED;
        }

        return validPoints >= MIN_VALID_POINTS ? NumericCheckOutcome.PASSED : NumericCheckOutcome.NOT_POSSIBLE;
    }

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
            var hundredths = SAMPLE_HUNDREDTHS[(point * 3 + index * 5) % SAMPLE_HUNDREDTHS.Length];
            assignment[variables[index]] = Entity.Number.Rational.Create(hundredths, 100);
        }

        return assignment;
    }

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

            var number = substituted.EvalNumerical();
            return IsFinite(number) ? number : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static Entity WithoutCondition(Entity expression) => expression is Entity.Providedf provided ? provided.Expression : expression;

    private static bool IsFinite(Entity.Number.Complex number) => IsFinite(number.RealPart.EDecimal) && IsFinite(number.ImaginaryPart.EDecimal);

    private static bool IsFinite(EDecimal value) => !value.IsNaN() && !value.IsInfinity();

    private static bool AreClose(Entity.Number.Complex left, Entity.Number.Complex right)
    {
        var context = EContext.ForPrecision(PRECISION);
        var difference = left.RealPart.EDecimal.Subtract(right.RealPart.EDecimal, context).Abs().Add(left.ImaginaryPart.EDecimal.Subtract(right.ImaginaryPart.EDecimal, context).Abs(), context);
        var leftSize = left.RealPart.EDecimal.Abs().Add(left.ImaginaryPart.EDecimal.Abs(), context);
        var rightSize = right.RealPart.EDecimal.Abs().Add(right.ImaginaryPart.EDecimal.Abs(), context);
        var scale = EDecimal.Max(EDecimal.One, EDecimal.Max(leftSize, rightSize));
        return difference.CompareTo(TOLERANCE.Multiply(scale, context)) <= 0;
    }
}