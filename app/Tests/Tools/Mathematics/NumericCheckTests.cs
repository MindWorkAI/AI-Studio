using AIStudio.Tools.Mathematics;

using AngouriMath;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class NumericCheckTests
{
    private static NumericCheckOutcome Check(string left, string right) => NumericCheck.AreEqual(MathExpressionReader.Read(left, "left"), MathExpressionReader.Read(right, "right"));

    [Test]
    public void PassesWhatIsEqual()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Check("sin(x)^2 + cos(x)^2", "1"), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(Check("(x + y)^2", "x^2 + 2*x*y + y^2"), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(Check("x^4 - 1", "(x + 1)*(x - 1)*(x^2 + 1)"), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(Check("547 / 57576", "1094 / 115152"), Is.EqualTo(NumericCheckOutcome.PASSED));
        }
    }

    [Test]
    public void PassesAnExpansionWhoseTermsCancel()
    {
        // At the sample point x = 1.47, the terms of the expanded form reach about 10^92 and add up to
        // about 10^-66; in doubles, or at 50 digits, nothing of the value would be left:
        var power = MathExpressionReader.Read("(x - 1)^200", "expression");
        Assert.That(NumericCheck.AreEqual(power, power.Expand()), Is.EqualTo(NumericCheckOutcome.PASSED));
    }

    [Test]
    public void FailsWhatDiffers()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Check("x^2", "x^3"), Is.EqualTo(NumericCheckOutcome.FAILED));
            Assert.That(Check("(x + y)^2", "x^2 + y^2"), Is.EqualTo(NumericCheckOutcome.FAILED));
            Assert.That(Check("pi^4/15", "6.4939394022668"), Is.EqualTo(NumericCheckOutcome.FAILED), "Fourteen digits are not equality.");
            Assert.That(Check("sin(x)", "sin(x + 2*pi)"), Is.EqualTo(NumericCheckOutcome.PASSED), "pi is a constant, not a variable which takes the values of the sample points.");
            Assert.That(Check("e^x", "x + 1"), Is.EqualTo(NumericCheckOutcome.FAILED));
        }
    }

    [Test]
    public void ComparesWhereAConditionHolds()
    {
        var simplified = MathExpressionReader.Read("(x^2 - 1)/(x - 1)", "expression").Simplify();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(simplified, Is.TypeOf<Entity.Providedf>());
            Assert.That(NumericCheck.AreEqual(simplified, MathExpressionReader.Read("x + 1", "expression")), Is.EqualTo(NumericCheckOutcome.PASSED));
        }
    }

    [Test]
    public void IsNotPossibleWithoutValues()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Check("integral(sin(sin(x)), x)", "x"), Is.EqualTo(NumericCheckOutcome.NOT_POSSIBLE), "An integral without a closed form has no value.");
            Assert.That(Check("1/(x - x)", "1/(y - y)"), Is.EqualTo(NumericCheckOutcome.NOT_POSSIBLE), "No sample point has a value.");
        }
    }

    [Test]
    public void ChecksADerivativeByItsCentralDifference()
    {
        var x = MathS.Var("x");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("2*x*sin(x) + x^2*cos(x)", "derivative"), MathExpressionReader.Read("x^2*sin(x)", "function"), x), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("2*x + 1", "derivative"), MathExpressionReader.Read("x^2", "function"), x), Is.EqualTo(NumericCheckOutcome.FAILED));
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("a", "derivative"), MathExpressionReader.Read("a*x + C", "function"), x), Is.EqualTo(NumericCheckOutcome.PASSED), "Other variables, the constant of integration among them, take sample values.");
        }
    }

    [Test]
    public void GivesAnIntegerParameterIntegerValues()
    {
        var parameter = MathS.Var("n_1");
        var values = Enumerable.Range(0, 6).Select(point => NumericCheck.SampleAssignment(point, [parameter])[parameter]).Distinct().ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values, Is.EquivalentTo(new Entity[] { -1, 0, 1 }));
            Assert.That(NumericCheck.IsIntegerParameter(MathS.Var("n_12")), Is.True);
            Assert.That(NumericCheck.IsIntegerParameter(MathS.Var("n")), Is.False);
            Assert.That(NumericCheck.IsIntegerParameter(MathS.Var("n_x")), Is.False);
        }
    }

    [Test]
    public void TheSamplePointsAreFixedAndDifferForEachVariable()
    {
        var variables = new[] { MathS.Var("x"), MathS.Var("y"), MathS.Var("z") };
        var first = NumericCheck.SampleAssignment(0, variables);
        var again = NumericCheck.SampleAssignment(0, variables);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(again, Is.EqualTo(first));
            Assert.That(first.Values.Distinct().Count(), Is.EqualTo(3));
            Assert.That(first[variables[0]], Is.EqualTo(Entity.Number.Rational.Create(61, 100)));
            Assert.That(NumericCheck.SampleAssignment(1, variables)[variables[0]], Is.Not.EqualTo(first[variables[0]]));
        }
    }
}