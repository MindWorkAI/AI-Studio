using AIStudio.Tools.Mathematics;

using AngouriMath;

using PeterO.Numbers;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class MathFormattingTests
{
    private static string Format(string value, int digits) => MathFormatting.FormatDecimal(EDecimal.FromString(value), digits);

    [Test]
    public void WritesDecimalsWithTheirSignificantDigits()
    {
        var quotient = Entity.Number.Rational.Create(547, 57576).EDecimal;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathFormatting.FormatDecimal(quotient, 20), Is.EqualTo("0.0095004863137418368765"));
            Assert.That(MathFormatting.FormatDecimal(quotient, 3), Is.EqualTo("0.0095"), "0.00950 at three digits; trailing zeros are left out.");
            Assert.That(Format("0.5", 20), Is.EqualTo("0.5"));
            Assert.That(Format("120", 20), Is.EqualTo("120"));
            Assert.That(Format("1234567.891", 4), Is.EqualTo("1235000"));
            Assert.That(Format("-0.000001234", 20), Is.EqualTo("-0.000001234"));
            Assert.That(Format("0", 20), Is.EqualTo("0"));
        }
    }

    [Test]
    public void WritesVeryLargeAndVerySmallDecimalsInScientificNotation()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Format("602214076000000000000000", 20), Is.EqualTo("6.02214076e+23"));
            Assert.That(Format("100000000000000000000", 20), Is.EqualTo("100000000000000000000"), "1e20 is still written out.");
            Assert.That(Format("1E-60", 20), Is.EqualTo("1e-60"));
            Assert.That(Format("-1.5E-7", 20), Is.EqualTo("-1.5e-7"));
        }
    }

    [Test]
    public void WritesValuesWithoutAMeaningAsTheToolsReadThem()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathFormatting.FormatDecimal(double.NaN, 15), Is.EqualTo("NaN"));
            Assert.That(MathFormatting.FormatDecimal(double.PositiveInfinity, 15), Is.EqualTo("+oo"));
            Assert.That(MathFormatting.FormatDecimal(EDecimal.NegativeInfinity, 15), Is.EqualTo("-oo"));
            Assert.That(MathFormatting.FormatDecimal(Math.PI, 15), Is.EqualTo("3.14159265358979"));
        }
    }

    [Test]
    public void CountsTheDigitsTwoValuesShare()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathFormatting.CountMatchingDigits(EDecimal.FromString("1.0000001"), EDecimal.FromString("1.0000002"), 20), Is.EqualTo(7));
            Assert.That(MathFormatting.CountMatchingDigits(EDecimal.FromString("0.99999"), EDecimal.FromString("1.00001"), 20), Is.EqualTo(4), "No digit is the same, but the values are close.");
            Assert.That(MathFormatting.CountMatchingDigits(EDecimal.FromString("2.5"), EDecimal.FromString("2.5"), 20), Is.EqualTo(20));
            Assert.That(MathFormatting.CountMatchingDigits(EDecimal.FromString("1"), EDecimal.FromString("-1"), 20), Is.Zero);
            Assert.That(MathFormatting.CountMatchingDigits(EDecimal.NaN, EDecimal.NaN, 20), Is.Zero);
        }
    }

    [Test]
    public void RefusesAResultBeyondTheLimit()
    {
        var text = new string('1', MathLimits.MAX_RESULT_LENGTH + 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathFormatting.EnsureLength(new string('1', MathLimits.MAX_RESULT_LENGTH), "The result"), Has.Length.EqualTo(MathLimits.MAX_RESULT_LENGTH));
            Assert.That(() => MathFormatting.EnsureLength(text, "The result"), Throws.InstanceOf<MathComputationException>().With.Message.EqualTo("The result has 20,001 characters, but at most 20,000 are returned."));
            Assert.That(MathFormatting.ToLatex(MathExpressionReader.Read("547 / 57576", "expression"), "The LaTeX"), Is.EqualTo(@"\frac{547}{57576}"));
            Assert.That(MathFormatting.ToText(MathExpressionReader.Read("x^3/(e^x-1)", "expression"), "The result"), Is.EqualTo("x ^ 3 / (e ^ x - 1)"));
        }
    }
}