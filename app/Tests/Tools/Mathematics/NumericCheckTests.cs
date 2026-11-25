using AIStudio.Tools.Mathematics;

using AngouriMath;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class NumericCheckTests
{
    private static NumericCheckOutcome Check(string left, string right) => NumericCheck.AreEqual(MathExpressionReader.Read(left, "left"), MathExpressionReader.Read(right, "right"));

    /// <summary>
    /// The values an index takes across the sample points.
    /// </summary>
    private static List<Entity> ValuesOf(SolutionIndex index) => Enumerable.Range(0, 6)
        .Select(point => NumericCheck.SampleAssignment(point, [index.Variable], [index]))
        .Where(assignment => assignment.ContainsKey(index.Variable))
        .Select(assignment => assignment[index.Variable])
        .Distinct()
        .ToList();

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
    public void TellsTinyValuesApart()
    {
        // With an absolute tolerance of 1e-30, any two values below it would pass for equal:
        var x = MathS.Var("x");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Check("6.62607015e-34 * x", "6.62607015e-35 * x"), Is.EqualTo(NumericCheckOutcome.FAILED));
            Assert.That(Check("6.62607015e-34 * sin(x)", "6.62607015e-34 * sin(x) * (1 + 1e-20)"), Is.EqualTo(NumericCheckOutcome.FAILED));
            Assert.That(Check("6.62607015e-34 * x^2", "x * 6.62607015e-34 * x"), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(Check("sin(2*x) - 2*sin(x)*cos(x)", "0"), Is.EqualTo(NumericCheckOutcome.PASSED), "Against 0, the tolerance stays absolute, since terms which cancel leave their rounding noise.");
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("1.32521403e-33 * x", "derivative"), MathExpressionReader.Read("6.62607015e-34 * x^2", "function"), x), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("6.62607015e-34 * x", "derivative"), MathExpressionReader.Read("6.62607015e-34 * x^2", "function"), x), Is.EqualTo(NumericCheckOutcome.FAILED));
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
    public void ChecksADerivativeOfAFunctionFarLargerThanIt()
    {
        var x = MathS.Var("x");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("cos(x)", "derivative"), MathExpressionReader.Read("100! + sin(x)", "function"), x), Is.EqualTo(NumericCheckOutcome.PASSED), "100! has 158 digits, so 60 of them leave nothing of sin(x).");
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("-x^99 * e^(-x)", "derivative"), MathExpressionReader.Read("gamma(100, x)", "function"), x), Is.EqualTo(NumericCheckOutcome.PASSED), "gamma(100, x) is about 99!, and its derivative about 1e-21.");
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("cos(x) + 10^-15", "derivative"), MathExpressionReader.Read("100! + sin(x)", "function"), x), Is.EqualTo(NumericCheckOutcome.FAILED));
            Assert.That(NumericCheck.IsDerivativeOf(MathExpressionReader.Read("cos(x)", "derivative"), MathExpressionReader.Read("10^2000 + sin(x)", "function"), x), Is.EqualTo(NumericCheckOutcome.NOT_POSSIBLE), "It would take over 2,000 digits.");
        }
    }

    [Test]
    public void GivesAVariableNamedLikeAnIndexNoIntegers()
    {
        // The index of a family is known by its family, not by its name:
        var parameter = MathS.Var("n_1");
        var values = Enumerable.Range(0, 6).Select(point => NumericCheck.SampleAssignment(point, [parameter])[parameter]).Distinct().ToList();
        Assert.That(values, Has.None.InstanceOf<Entity.Number.Integer>());
    }

    [Test]
    public void GivesAnIndexIntegersOfItsSet()
    {
        var n = MathS.Var("n");
        var integers = new SolutionIndex(n, MathS.Sets.Z);
        var withoutZero = new SolutionIndex(n, (Entity.Set)MathExpressionReader.Read("ZZ \\ { 0 }", "set"));
        var none = new SolutionIndex(n, (Entity.Set)MathExpressionReader.Read("{ 1/2 }", "set"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ValuesOf(integers), Is.EquivalentTo(new Entity[] { -1, 0, 1 }));
            Assert.That(ValuesOf(withoutZero), Is.EquivalentTo(new Entity[] { -1, 1, 2 }));
            Assert.That(ValuesOf(none), Is.Empty);
            Assert.That(NumericCheck.AreEqual(MathExpressionReader.Read("sin(pi/6 + 2*pi*n)", "left"), MathExpressionReader.Read("1/2", "right"), [integers]), Is.EqualTo(NumericCheckOutcome.PASSED));
            Assert.That(NumericCheck.AreEqual(MathExpressionReader.Read("sin(pi/6 + pi*n)", "left"), MathExpressionReader.Read("1/2", "right"), [integers]), Is.EqualTo(NumericCheckOutcome.FAILED), "At n = -1 and 1, the sine is -1/2.");
            Assert.That(NumericCheck.AreEqual(MathExpressionReader.Read("n", "left"), MathExpressionReader.Read("n", "right"), [none]), Is.EqualTo(NumericCheckOutcome.NOT_POSSIBLE), "An index which takes none of the integers tried leaves no point.");
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