using AIStudio.Tools.Mathematics;

using AngouriMath;

namespace AIStudio.Tests.Tools.Mathematics;

/// <summary>
/// Checks that the guards refuse what AngouriMath could not stop, and nothing it computes quickly.
/// </summary>
/// <remarks>
/// None of the refused expressions is computed here: each of them would run on long past the test.
/// </remarks>
[TestFixture]
public sealed class MathGuardsTests
{
    private static Entity Read(string text) => MathExpressionReader.Read(text, "expression");

    private static string ExactSizeRefusal(string text) => Assert.Throws<MathComputationException>(() => MathGuards.CheckExactSize(Read(text)))!.Message;

    [Test]
    public void RefusesAPowerWithTooManyDigits()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ExactSizeRefusal("9^9^9"), Is.EqualTo("'9 ^ 9 ^ 9' would have about 369,693,100 digits as an exact number; its decimal logarithm is about 369693099.632. The tool computes such a number in full and cannot stop midway, so exact numbers are limited to 20,000 digits."));
            Assert.That(ExactSizeRefusal("9^9^9^9"), Does.StartWith("'9 ^ 9 ^ 9' would have"), "The inner power is judged first, before its value is needed.");
            Assert.That(ExactSizeRefusal("1 + 10^20001"), Does.StartWith("'10 ^ 20001' would have about 20,001 digits"));
            Assert.That(ExactSizeRefusal("(1/2)^100000"), Does.Contain("would have about 30,103 digits"), "Numerator and denominator count.");
            Assert.That(ExactSizeRefusal("sqrt(2)^1000000"), Does.Contain("would have about"), "sqrt(2)^1000000 is the exact number 2^500000.");
            Assert.That(ExactSizeRefusal("x^(9^9^9)"), Does.StartWith("'9 ^ 9 ^ 9' would have"), "An exponent is a number even when the base is a variable.");
            Assert.That(ExactSizeRefusal("(e^0 + 1)^(10^9)"), Does.Contain("would have about 301,029,996 digits"), "A constant is no variable: the base is the number 2.");
        }
    }

    [Test]
    public void RefusesAFactorialWithTooManyDigits()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ExactSizeRefusal("(10^9)!"), Does.StartWith("'(10 ^ 9)!' would have about 8,565,705,"));
            Assert.That(ExactSizeRefusal("10000!"), Does.StartWith("'10000!' would have about 35,660 digits"));
            Assert.That(ExactSizeRefusal("(10!)!"), Does.StartWith("'(10!)!' would have about"));
        }
    }

    [Test]
    public void LetsThroughWhatIsComputedQuickly()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var text in new[] { "2^10000", "10^19999", "5000!", "0.5!", "(3!)!", "x^1000000", "2^x", "pi^1000000", "e^(10^6)", "1^(10^100)", "0^(10^100)", "547 / 57576", "integral(x^3/(e^x-1), x, 0, +oo)" })
                Assert.DoesNotThrow(() => MathGuards.CheckExactSize(Read(text)), text);
        }
    }

    [Test]
    public void RefusesTheExpansionOfAHighPowerOfASum()
    {
        using (Assert.EnterMultipleScope())
        {
            var refusal = Assert.Throws<MathComputationException>(() => MathGuards.CheckExpansion(Read("1 + (x + y)^500")))!;
            Assert.That(refusal.Message, Is.EqualTo("'(x + y) ^ 500' raises a sum to the power of 500. Where the tool may expand, a sum is raised to exponents up to 200 only, since it cannot stop a larger expansion midway."));
            Assert.Throws<MathComputationException>(() => MathGuards.CheckExpansion(Read("(x - 1)^(-300)")));

            foreach (var text in new[] { "(x + y)^200", "x^500", "(x * y)^500", "(x + y)^n" })
                Assert.DoesNotThrow(() => MathGuards.CheckExpansion(Read(text)), text);
        }
    }
}