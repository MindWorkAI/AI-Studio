using AIStudio.Tools.Mathematics;

using AngouriMath;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class DoubleExponentialQuadratureTests
{
    private static Func<double, double> Compiled(string expression) => MathExpressionReader.Read(expression, "expression").Compile<double, double>(MathS.Var("x"));

    /// <summary>
    /// Checks a converged result against the exact value, and that its estimated error is honest.
    /// </summary>
    private static void AssertIntegral(Func<double, double> integrand, double lower, double upper, double exact, double relativeTolerance = 1e-13)
    {
        var result = DoubleExponentialQuadrature.Integrate(integrand, lower, upper);
        var actualError = Math.Abs(result.Value - exact);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(QuadratureStatus.CONVERGED));
            Assert.That(result.Value, Is.EqualTo(exact).Within(relativeTolerance * Math.Abs(exact)));
            Assert.That(actualError, Is.LessThanOrEqualTo(Math.Max(result.EstimatedError, 1e-15 * Math.Abs(exact))), "The estimated error must not claim more digits than the value has.");
        }
    }

    [Test]
    public void IntegratesOverAFiniteInterval()
    {
        AssertIntegral(x => x * x, 0, 1, 1.0 / 3);
        AssertIntegral(Math.Sin, 0, Math.PI, 2);
        AssertIntegral(Math.Exp, -1, 2, Math.Exp(2) - Math.Exp(-1));
    }

    [Test]
    public void IntegratesASingularityAtTheLowerBoundZero()
    {
        AssertIntegral(Math.Log, 0, 1, -1);
        AssertIntegral(x => 1 / Math.Sqrt(x), 0, 1, 2);
    }

    [Test]
    public void ReachesASingularityAtAnotherBoundWithFewerDigits()
    {
        var result = DoubleExponentialQuadrature.Integrate(x => 1 / Math.Sqrt(1 - x), 0, 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(QuadratureStatus.CONVERGED));
            Assert.That(result.Value, Is.EqualTo(2).Within(1e-6));
            Assert.That(Math.Abs(result.Value - 2), Is.LessThanOrEqualTo(result.EstimatedError), "Near 1, a double carries few digits of the distance to 1; the estimate has to say so.");
        }
    }

    [Test]
    public void IntegratesOverInfiniteIntervals()
    {
        AssertIntegral(x => Math.Exp(-x), 0, double.PositiveInfinity, 1);
        AssertIntegral(Math.Exp, double.NegativeInfinity, 0, 1);
        AssertIntegral(x => 1 / (1 + x * x), 0, double.PositiveInfinity, Math.PI / 2);
        AssertIntegral(x => Math.Exp(-x * x), double.NegativeInfinity, double.PositiveInfinity, Math.Sqrt(Math.PI));
        AssertIntegral(x => Math.Exp(-x), 2, double.PositiveInfinity, Math.Exp(-2));
    }

    [Test]
    public void IntegratesTheExampleOfTheBoseIntegral()
    {
        // The compiled integrand is 0/0 at 0, and has no finite value for x below about 1e-16, where e^x - 1 is 0 in doubles:
        var bose = Compiled("x^3/(e^x-1)");
        Assert.That(double.IsFinite(bose(1e-17)), Is.False);
        AssertIntegral(bose, 0, double.PositiveInfinity, Math.Pow(Math.PI, 4) / 15);
    }

    [Test]
    public void ReportsAnIntegralWhichDoesNotSettle()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DoubleExponentialQuadrature.Integrate(x => Math.Sin(x) / x, 0, double.PositiveInfinity).Status, Is.EqualTo(QuadratureStatus.NOT_CONVERGED), "sin(x)/x converges only conditionally, which the substitution cannot settle.");
            Assert.That(DoubleExponentialQuadrature.Integrate(x => 1 / x, 0, 1).Status, Is.EqualTo(QuadratureStatus.NOT_CONVERGED), "1/x diverges at 0.");
            Assert.That(DoubleExponentialQuadrature.Integrate(x => 1 / x, 1, double.PositiveInfinity).Status, Is.EqualTo(QuadratureStatus.NOT_CONVERGED), "1/x diverges at infinity.");
            Assert.That(DoubleExponentialQuadrature.Integrate(Math.Sin, 0, double.PositiveInfinity).Status, Is.EqualTo(QuadratureStatus.NOT_CONVERGED));
        }
    }

    [Test]
    public void ReportsWhereTheIntegrandHasNoValue()
    {
        var pole = DoubleExponentialQuadrature.Integrate(x => 1 / x, -1, 1);
        var root = DoubleExponentialQuadrature.Integrate(Compiled("sqrt(x - 1)"), 0, 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pole.Status, Is.EqualTo(QuadratureStatus.NO_VALUE));
            Assert.That(pole.FailurePoint, Is.Zero, "The pole lies in the middle of the interval.");
            Assert.That(root.Status, Is.EqualTo(QuadratureStatus.NO_VALUE));
            Assert.That(root.FailurePoint, Is.LessThan(1), "A compiled function stays real, so sqrt has no value below 1.");
        }
    }

    [Test]
    public void ChangesTheSignForReversedBounds()
    {
        var reversed = DoubleExponentialQuadrature.Integrate(x => x * x, 1, 0);
        var empty = DoubleExponentialQuadrature.Integrate(x => x * x, 2, 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reversed.Value, Is.EqualTo(-1.0 / 3).Within(1e-15));
            Assert.That(empty.Value, Is.Zero);
            Assert.That(empty.Status, Is.EqualTo(QuadratureStatus.CONVERGED));
        }
    }

    [Test]
    public void SettlesAnIntegralOfZero()
    {
        var result = DoubleExponentialQuadrature.Integrate(Math.Sin, -1, 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(QuadratureStatus.CONVERGED));
            Assert.That(result.Value, Is.EqualTo(0).Within(1e-15));
        }
    }

    [Test]
    public void StopsAtTheCancellationToken()
    {
        var canceled = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => DoubleExponentialQuadrature.Integrate(x => Math.Sin(x) / x, 0, double.PositiveInfinity, canceled));
    }
}