namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Integrates a function numerically by double exponential quadrature, also over infinite intervals.
/// </summary>
/// <remarks>
/// A substitution moves the points of a trapezoidal sum doubly exponentially close to the bounds of
/// the interval, or out to infinity: tanh-sinh on [a, b], exp-sinh on [a, oo) and (-oo, b], and
/// sinh-sinh on the whole line. Integrands which are analytic inside the interval converge to about
/// fourteen digits within a few hundred points, singularities at the bounds included, such as ln(x) or
/// 1/sqrt(x) at 0. The points never lie on a bound, where an integrand such as x^3/(e^x - 1) has no
/// value.<br/><br/>
/// Each level halves the step, and the difference between two levels estimates the error. The terms
/// left out at the ends add to it whenever the sum had to stop before they became negligible: at the
/// largest parameter, at a bound which a double cannot tell apart from its neighbors, or where the
/// integrand has no value. That is what keeps an integral like sin(x)/x over [0, oo), which the
/// substitution cannot settle, from converging by chance. A singularity at a bound other than 0 is
/// reached only to about seven digits, since a double near such a bound carries few digits of its
/// distance to it; the estimated error says so.<br/><br/>
/// The integrand is a double function, compiled by AngouriMath, so the result carries at most the
/// digits of a double.
/// </remarks>
public static class DoubleExponentialQuadrature
{
    /// <summary>
    /// The finest step is 2^-MAX_LEVEL.
    /// </summary>
    private const int MAX_LEVEL = 10;

    /// <summary>
    /// The coarsest step which may end the integration: a coarser sum can match the next one by chance.
    /// </summary>
    private const int MIN_CONVERGED_LEVEL = 3;

    /// <summary>
    /// Two levels which differ by less than this, relative to the integral, end the refinement.
    /// </summary>
    private const double RELATIVE_TOLERANCE = 1e-13;

    /// <summary>
    /// The largest estimated relative error of a converged result: six digits at least.
    /// </summary>
    private const double ACCEPTABLE_RELATIVE_ERROR = 1e-6;

    /// <summary>
    /// A term this much smaller than the sum so far no longer counts.
    /// </summary>
    private const double NEGLIGIBLE = 1e-18;

    /// <summary>
    /// How many negligible terms in a row end one side of a sum.
    /// </summary>
    private const int NEGLIGIBLE_IN_A_ROW = 3;

    /// <summary>
    /// The largest parameter on a finite interval: there, the points lie within 1e-37 of the bounds.
    /// </summary>
    private const double FINITE_MAX_PARAMETER = 4.0;

    /// <summary>
    /// The largest parameter towards infinity: there, the points lie beyond 1e50.
    /// </summary>
    private const double INFINITE_MAX_PARAMETER = 5.0;

    /// <summary>
    /// How much more the terms left out at an end may add up to than estimated. The last terms before
    /// a bound are also the least accurate ones: 1 - x carries few digits for an x close to 1, which a
    /// singularity such as 1/sqrt(1 - x) magnifies.
    /// </summary>
    private const double TAIL_SAFETY_FACTOR = 10;

    private const int EVALUATIONS_PER_CANCELLATION_CHECK = 256;

    private const double HALF_PI = Math.PI / 2;

    /// <summary>
    /// Integrates a function over an interval.
    /// </summary>
    /// <param name="integrand">The function, which returns NaN or an infinity where it has no value.</param>
    /// <param name="lower">The lower bound, which may be negative infinity.</param>
    /// <param name="upper">The upper bound, which may be positive infinity; below the lower bound, the integral changes its sign.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result, with its estimated error and how the integration ended.</returns>
    public static QuadratureResult Integrate(Func<double, double> integrand, double lower, double upper, CancellationToken token = default)
    {
        if (double.IsNaN(lower) || double.IsNaN(upper))
            throw new ArgumentException("A bound of the integral is not a number.");

        if (lower.Equals(upper))
            return new(0, 0, QuadratureStatus.CONVERGED, null, 0);

        if (lower > upper)
        {
            var reversed = Integrate(integrand, upper, lower, token);
            return reversed with { Value = -reversed.Value };
        }

        var interval = new Interval(lower, upper);
        var evaluations = 0;
        var previous = double.NaN;
        LevelSum sum = default;
        for (var level = 0; level <= MAX_LEVEL; level++)
        {
            sum = Sum(integrand, interval, Math.ScaleB(1.0, -level), ref evaluations, token);
            if (sum.NoValueAt is { } point)
                return new(double.NaN, double.PositiveInfinity, QuadratureStatus.NO_VALUE, point, evaluations);

            var difference = Math.Abs(sum.Integral - previous);
            var scale = Math.Max(Math.Abs(sum.Integral), sum.AbsoluteIntegral);
            var settled = level >= MIN_CONVERGED_LEVEL && difference <= RELATIVE_TOLERANCE * scale;
            if (settled || level == MAX_LEVEL)
                return Result(sum, difference + sum.TailError, scale, evaluations);

            previous = sum.Integral;
        }

        return Result(sum, double.PositiveInfinity, 0, evaluations);
    }

    private static QuadratureResult Result(LevelSum sum, double error, double scale, int evaluations)
    {
        if (!double.IsFinite(sum.Integral) || double.IsNaN(error))
            return new(sum.Integral, double.PositiveInfinity, QuadratureStatus.NOT_CONVERGED, null, evaluations);

        var status = error <= ACCEPTABLE_RELATIVE_ERROR * scale ? QuadratureStatus.CONVERGED : QuadratureStatus.NOT_CONVERGED;
        return new(sum.Integral, error, status, null, evaluations);
    }

    /// <summary>
    /// The trapezoidal sum of one level, from the middle of the parameter range outwards on both sides.
    /// </summary>
    private static LevelSum Sum(Func<double, double> integrand, Interval interval, double step, ref int evaluations, CancellationToken token)
    {
        var center = interval.NodeAt(0);
        var centerValue = integrand(center.X);
        evaluations++;
        if (!double.IsFinite(centerValue))
            return new(0, 0, 0, center.X);

        var sum = center.Weight * centerValue;
        var absoluteSum = Math.Abs(sum);
        var tail = 0.0;
        foreach (var direction in (ReadOnlySpan<int>)[1, -1])
        {
            // Unknown before the first term of this side, so a missing value there is never negligible:
            var lastTerm = double.PositiveInfinity;
            var lastParameter = 0.0;
            var negligibleInARow = 0;
            for (var index = 1; ; index++)
            {
                var parameter = direction * index * step;
                if (Math.Abs(parameter) > interval.MaxParameter)
                {
                    tail += Tail(lastTerm, lastParameter, step);
                    break;
                }

                var node = interval.NodeAt(parameter);
                if (node.AtBound)
                {
                    tail += Tail(lastTerm, lastParameter, step);
                    break;
                }

                if (++evaluations % EVALUATIONS_PER_CANCELLATION_CHECK == 0)
                    token.ThrowIfCancellationRequested();

                var value = integrand(node.X);
                if (!double.IsFinite(value))
                {
                    // Near a bound, an integrand such as x^3/(e^x - 1) loses its value to rounding, where its terms no longer count:
                    if (Math.Abs(lastTerm) <= NEGLIGIBLE * Math.Abs(sum))
                    {
                        tail += Tail(lastTerm, lastParameter, step);
                        break;
                    }

                    return new(0, 0, 0, node.X);
                }

                var term = node.Weight * value;
                sum += term;
                absoluteSum += Math.Abs(term);
                lastTerm = term;
                lastParameter = parameter;
                if (Math.Abs(term) <= NEGLIGIBLE * Math.Abs(sum) && sum != 0)
                {
                    if (++negligibleInARow >= NEGLIGIBLE_IN_A_ROW)
                        break;
                }
                else
                    negligibleInARow = 0;
            }
        }

        return new(step * sum, step * tail, step * absoluteSum, null);
    }

    /// <summary>
    /// What the terms beyond the last one of a side may add up to, before the sum is multiplied by the
    /// step: as much as the last term, or the integral of terms which decay from it at the pace of the
    /// substitution, whichever is more, times TAIL_SAFETY_FACTOR.
    /// </summary>
    private static double Tail(double lastTerm, double lastParameter, double step) => TAIL_SAFETY_FACTOR * Math.Abs(lastTerm) * Math.Max(1, 1 / (HALF_PI * Math.Cosh(lastParameter) * step));

    /// <summary>
    /// The sum of one level: the integral, the estimated error of what its sides left out, and the
    /// integral of the magnitude, which scales the tolerance of an integral close to zero.
    /// </summary>
    private readonly record struct LevelSum(double Integral, double TailError, double AbsoluteIntegral, double? NoValueAt);

    /// <summary>
    /// A point of the sum and its weight; at a bound when a double cannot tell the point from the bound.
    /// </summary>
    private readonly record struct QuadratureNode(double X, double Weight, bool AtBound);

    /// <summary>
    /// The substitution which fits the bounds.
    /// </summary>
    private readonly record struct Interval(double Lower, double Upper)
    {
        public double MaxParameter => double.IsFinite(this.Lower) && double.IsFinite(this.Upper) ? FINITE_MAX_PARAMETER : INFINITE_MAX_PARAMETER;

        public QuadratureNode NodeAt(double parameter)
        {
            var inner = HALF_PI * Math.Sinh(parameter);
            var derivative = HALF_PI * Math.Cosh(parameter);
            if (double.IsFinite(this.Lower) && double.IsFinite(this.Upper))
            {
                // tanh-sinh, with the distance to the nearer bound computed directly: 1 - tanh(u) = 2 / (e^(2u) + 1).
                var halfWidth = (this.Upper - this.Lower) / 2;
                var distance = halfWidth * 2 / (Math.Exp(2 * Math.Abs(inner)) + 1);
                var x = parameter >= 0 ? this.Upper - distance : this.Lower + distance;
                var secant = 2 / (Math.Exp(inner) + Math.Exp(-inner));
                var atBound = distance == 0 || x.Equals(parameter >= 0 ? this.Upper : this.Lower);
                return new(x, halfWidth * derivative * secant * secant, atBound);
            }

            if (double.IsFinite(this.Lower))
            {
                // exp-sinh towards positive infinity:
                var offset = Math.Exp(inner);
                var x = this.Lower + offset;
                return new(x, derivative * offset, !double.IsFinite(x) || x.Equals(this.Lower));
            }

            if (double.IsFinite(this.Upper))
            {
                // exp-sinh towards negative infinity, mirrored:
                var offset = Math.Exp(inner);
                var x = this.Upper - offset;
                return new(x, derivative * offset, !double.IsFinite(x) || x.Equals(this.Upper));
            }

            // sinh-sinh over the whole line:
            var point = Math.Sinh(inner);
            return new(point, derivative * Math.Cosh(inner), !double.IsFinite(point));
        }
    }
}