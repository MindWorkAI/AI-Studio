using AIStudio.Tools.Mathematics;

using AngouriMath;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class MathExpressionReaderTests
{
    private static string Refusal(string text, string argumentName = "expression") => Assert.Throws<MathComputationException>(() => MathExpressionReader.Read(text, argumentName))!.Message;

    [Test]
    public void ReadsTheSyntaxOfTheTools()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathExpressionReader.Read("integral(x^3/(e^x-1), x, 0, +oo)", "expression"), Is.TypeOf<Entity.Integralf>());
            Assert.That(MathExpressionReader.Read("x^2 = 4", "expression"), Is.TypeOf<Entity.Equalsf>());
            Assert.That(MathExpressionReader.Read("x <= 2", "expression"), Is.TypeOf<Entity.LessOrEqualf>(), "The equals sign of <= is no equation.");
            Assert.That(MathExpressionReader.Read("x >= 2", "expression"), Is.TypeOf<Entity.GreaterOrEqualf>());
            Assert.That(MathExpressionReader.Read("a <> b", "expression").ToString(), Is.EqualTo("not a = b"));
            Assert.That(MathExpressionReader.Read(new string('1', MathLimits.MAX_EXPRESSION_LENGTH), "expression"), Is.InstanceOf<Entity.Number.Integer>());
        }
    }

    [Test]
    public void RefusesWhatItCannotReadAsMeant()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Refusal("  "), Is.EqualTo("Argument 'expression' is empty."));
            Assert.That(Refusal(new string('1', MathLimits.MAX_EXPRESSION_LENGTH + 1)), Is.EqualTo("Argument 'expression' has 2,001 characters, but at most 2,000 are read."));
            Assert.That(Refusal("a != b"), Is.EqualTo("Argument 'expression' contains '!=', which the tool reads as a factorial followed by '='. Not-equal is written '<>'."));
            Assert.That(Refusal("|x - 1|"), Does.StartWith("Argument 'expression' contains '|', which the tool reads as divisibility."));
            Assert.That(Refusal("2 = 2 = 2"), Does.StartWith("Argument 'expression' contains 2 equals signs, but an expression holds one equation at most."));
            Assert.That(Refusal("oo", "approach"), Is.EqualTo("Argument 'approach' uses 'oo', which the tool reads as a variable. Infinity is written +oo or -oo."));
            Assert.That(Refusal("1/oo"), Does.Contain("uses 'oo'"));
        }
    }

    [Test]
    public void ReadsTheFunctionsTheToolsReturn()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathExpressionReader.Read("lambertw(0, 2)", "expression"), Is.TypeOf<Entity.LambertWf>());
            Assert.That(MathExpressionReader.Read("productlog(2)", "expression"), Is.TypeOf<Entity.LambertWf>());
            Assert.That(MathExpressionReader.Read("gamma(2, 1)", "expression"), Is.TypeOf<Entity.IncompleteGammaf>());
            Assert.That(MathExpressionReader.Read("polygamma(1, 1)", "expression"), Is.TypeOf<Entity.Polygammaf>());
            Assert.That(MathExpressionReader.Read("digamma(1)", "expression"), Is.TypeOf<Entity.Polygammaf>());
            Assert.That(MathExpressionReader.Read("real + zeta_2", "expression"), Is.TypeOf<Entity.Sumf>(), "A variable may carry the name of a function the tools do not support.");
        }
    }

    [Test]
    public void RefusesTheFunctionsTheToolsDoNotSupport()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var name in MathExpressionReader.UNSUPPORTED_FUNCTIONS)
                Assert.That(Refusal($"1 + {name}(2)"), Is.EqualTo($"Argument 'expression' uses the function '{name}', which the tool does not support."));

            Assert.That(Refusal("det([[1, 2], [3, 4]])", "lower_bound"), Is.EqualTo("Argument 'lower_bound' uses the function 'det', which the tool does not support."), "AngouriMath computes the determinant of a matrix written out while it parses.");
            Assert.That(Refusal("n!!"), Is.EqualTo("Argument 'expression' contains 'n!!', which the tool does not support."));
            Assert.That(Refusal("A[1, 2] + 1"), Is.EqualTo("Argument 'expression' contains 'A[1, 2]', which the tool does not support."));
            Assert.That(Refusal("product(k, k in { 1, 2, 3 })"), Is.EqualTo("Argument 'expression' contains 'product(k, k in { 1, 2, 3 })', which the tool does not support."));
        }
    }

    [Test]
    public void NamesTheMissingOperator()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Refusal("2x"), Is.EqualTo("Argument 'expression' has no operator between '2' and 'x' at column 2. Multiplication is written '*', as in 2*x, and a function takes its argument in parentheses, as in sin(x)."));
            Assert.That(Refusal("sin x"), Does.StartWith("Argument 'expression' has no operator between 'sin' and 'x' at column 5."));
            Assert.That(Refusal("foo(3)"), Is.EqualTo("Argument 'expression' has no operator between 'foo' and '(' at column 4. The tool knows no function named 'foo', so it would read the name as a variable times the parentheses. Multiplication is written '*'."));
        }
    }

    [Test]
    public void ShortensAParseErrorToItsColumnAndCause()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Refusal("2 + * 3"), Is.EqualTo("Argument 'expression' cannot be read at column 5: extraneous input '*'."));
            Assert.That(Refusal("(2 + 3"), Is.EqualTo("Argument 'expression' cannot be read at column 7: no viable alternative at input '(2+3'."));
            Assert.That(Refusal(@"\frac{1}{2}"), Does.StartWith("Argument 'expression' cannot be read at column 1:"));
            Assert.That(Refusal("sin(1, 2)"), Is.EqualTo("Argument 'expression' cannot be read: sin should have exactly 1 argument but 2 arguments are provided."));
            Assert.That(Refusal("integral(x^2, x, 2)", "lower_bound"), Does.StartWith("Argument 'lower_bound' cannot be read: integral should have exactly 4 arguments or 2 arguments"));
        }
    }
}