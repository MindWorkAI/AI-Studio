using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Tools.Mathematics;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

using Microsoft.Extensions.Logging.Abstractions;

using PeterO.Numbers;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class CalculateToolTests
{
    private static JsonObject Calculate(string expression, int digits = 20) => CalculateTool.Calculate(expression, digits, CancellationToken.None);

    private static string Refusal(string expression) => Assert.Throws<MathComputationException>(() => Calculate(expression))!.Message;

    private static string Text(JsonObject result, string property) => result[property]!.GetValue<string>();

    private static double RelativeError(JsonObject result, string expected)
    {
        var reference = EDecimal.FromString(expected);
        return EDecimal.FromString(Text(result, "decimal")).Subtract(reference).Abs().Divide(reference.Abs(), EContext.ForPrecision(20)).ToDouble();
    }

    private static JsonElement Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Test]
    public void KeepsAFractionExact()
    {
        var result = Calculate("547 / 57576");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result["expression"]!.GetValue<string>(), Is.EqualTo("547 / 57576"), "The expression reads as written; its value is the fraction.");
            Assert.That(result["exact"]!.GetValue<string>(), Is.EqualTo("547/57576"));
            Assert.That(result["exact_latex"]!.GetValue<string>(), Is.EqualTo(@"\frac{547}{57576}"));
            Assert.That(result["decimal"]!.GetValue<string>(), Is.EqualTo("0.0095004863137418368765"));
            Assert.That(result["digits"]!.GetValue<int>(), Is.EqualTo(20));
            Assert.That(result["approximate"]!.GetValue<bool>(), Is.False);
            Assert.That(result.ContainsKey("estimated_error"), Is.False);
            Assert.That(result.ContainsKey("issues"), Is.False);
        }
    }

    [Test]
    public void GivesAnIrrationalValueWithAllDigitsAskedFor()
    {
        var root = Calculate("sqrt(2)", 30);
        var power = Calculate("pi^4/15", 60);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(root["decimal"]!.GetValue<string>(), Is.EqualTo("1.41421356237309504880168872421"));
            Assert.That(root.ContainsKey("exact"), Is.False);
            Assert.That(power["decimal"]!.GetValue<string>(), Is.EqualTo("6.49393940226682914909602217924700741664850571151236144609786"), "Without guard digits, the last digits would be off.");
            Assert.That(power["digits"]!.GetValue<int>(), Is.EqualTo(60));
        }
    }

    [Test]
    public void DoesNotTakeANumberCloseToAnIntegerForIt()
    {
        // e^(pi*sqrt(163)) is 262537412640768743.99999999999925...
        var result = Calculate("e^(pi*sqrt(163))", 30);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ContainsKey("exact"), Is.False);
            Assert.That(result["decimal"]!.GetValue<string>(), Is.EqualTo("262537412640768743.999999999999"));
        }
    }

    [Test]
    public void GivesComplexAndInfiniteValues()
    {
        var root = Calculate("sqrt(-4)");
        var logarithm = Calculate("ln(0)");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(root["decimal"]!.GetValue<string>(), Is.EqualTo("2i"));
            Assert.That(root.ContainsKey("exact"), Is.True);
            Assert.That(logarithm["decimal"]!.GetValue<string>(), Is.EqualTo("-oo"));
            Assert.That(logarithm["digits"]!.GetValue<int>(), Is.Zero);
            Assert.That(logarithm["issues"]![0]!.GetValue<string>(), Is.EqualTo("The value is infinite."));
        }
    }

    [Test]
    public void ReadsEveryDecimalLiteralAsItsFraction()
    {
        // AngouriMath alone reads 6.62607015e-34 as an inexact decimal and 1e-60 as 0, see AngouriMathBehaviorTests.DowncastingWhileParsingLosesTinyLiterals:
        var planck = Calculate("6.62607015e-34");
        var tiny = Calculate("1e-60");
        var equation = Calculate("1 + 1e-40 = 1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(planck, "exact"), Is.EqualTo("132521403/200000000000000000000000000000000000000000"));
            Assert.That(Text(planck, "decimal"), Is.EqualTo("6.62607015e-34"));
            Assert.That(Text(tiny, "exact"), Is.EqualTo("1/1" + new string('0', 60)));
            Assert.That(equation["exactly_equal"]!.GetValue<bool>(), Is.False, "Simplifying would drop an inexact 1e-40 from the sum.");
            Assert.That(equation["numerically_equal"]!.GetValue<bool>(), Is.True, "Both sides agree in the 20 digits compared.");
        }
    }

    [Test]
    public void KeepsTheDigitsOfTinyValues()
    {
        // Each of these came out as 0 while AngouriMath downcast, see AngouriMathBehaviorTests.DowncastingTurnsTinyValuesIntoZero:
        var photon = Calculate("6.62607015e-34 * 299792458 / 500e-9");
        var thermal = Calculate("1.380649e-23 * 300");
        var exponential = Calculate("e^(-100)");
        var tinier = Calculate("exp(-1000)");
        var fraction = Calculate("21/10^50 * pi");
        var sum = Calculate("21/10^50 + 1e-60");
        var exactFraction = Calculate("1/10^200");
        var integral = Calculate("integral(1e-30*x, x, 0, 1)");
        var quotient = Calculate("pi/10^200");
        var simplifiedToZero = Calculate("1e-2000 * pi");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RelativeError(quotient, "3.14159265358979323846264e-200"), Is.LessThan(1e-19), "AngouriMath rounds a quotient to decimal places, so the precision grows until the value shows.");
            Assert.That(simplifiedToZero.ContainsKey("exact"), Is.False, "AngouriMath simplifies the value to 0, see AngouriMathBehaviorTests.DowncastingWhileParsingLosesTinyLiterals.");
            Assert.That(RelativeError(simplifiedToZero, "3.14159265358979323846264e-2000"), Is.LessThan(1e-19));
            Assert.That(Text(photon, "decimal"), Is.EqualTo("3.9728917142978574e-19"));
            Assert.That(Text(thermal, "decimal"), Is.EqualTo("4.141947e-21"));
            Assert.That(RelativeError(exponential, "3.72007597602083596295969580386e-44"), Is.LessThan(1e-19));
            Assert.That(RelativeError(tinier, "5.07595889754945676529180947957e-435"), Is.LessThan(1e-19));
            Assert.That(RelativeError(fraction, "6.5973445725385658007715511049e-49"), Is.LessThan(1e-19), "The fraction is rounded to decimal places unless the precision grows by its leading zeros.");
            Assert.That(Text(sum, "decimal"), Is.EqualTo("2.10000000001e-49"));
            Assert.That(Text(exactFraction, "exact"), Is.EqualTo("1/1" + new string('0', 200)));
            Assert.That(Text(exactFraction, "decimal"), Is.EqualTo("1e-200"));
            Assert.That(RelativeError(integral, "5e-31"), Is.LessThan(1e-13));
            foreach (var result in new[] { photon, thermal, exponential, tinier, fraction, sum, exactFraction })
                Assert.That(result["digits"]!.GetValue<int>(), Is.EqualTo(20));
        }
    }

    [Test]
    public void SaysWhenAZeroMayBeATinyValue()
    {
        // Simplifying gives 0, but the fraction 1e-2000 needs more digits than NumericEvaluator.MAX_PRECISION for a numerical check:
        var result = Calculate("1e-2000 * pi - 1e-2000 * pi");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(result, "decimal"), Is.EqualTo("0"));
            Assert.That(result["digits"]!.GetValue<int>(), Is.Zero);
            Assert.That(result["issues"]!.AsArray().Select(issue => issue!.GetValue<string>()), Has.Some.StartsWith("AngouriMath evaluates the value to 0 with ").And.Some.Contains("cannot show that it is exactly 0"));
        }
    }

    [Test]
    public void IntegratesADefiniteIntegralNumerically()
    {
        var result = Calculate("integral(x^3/(e^x-1), x, 0, +oo)");
        var digits = result["digits"]!.GetValue<int>();
        var exact = Math.Pow(Math.PI, 4) / 15;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(digits, Is.InRange(12, 15), "A double carries no more, and the estimated error of the quadrature is conservative.");
            Assert.That(double.Parse(result["decimal"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(exact).Within(Math.Pow(10, 1 - digits) * exact), "Every digit reported is right.");
            Assert.That(result["approximate"]!.GetValue<bool>(), Is.True);
            Assert.That(result.ContainsKey("estimated_error"), Is.True);
            Assert.That(result.ContainsKey("exact"), Is.False);
            Assert.That(result["issues"]!.AsArray().Select(issue => issue!.GetValue<string>()), Has.Some.Contains("was integrated numerically"));
            Assert.That(result["issues"]!.AsArray().Select(issue => issue!.GetValue<string>()), Has.Some.StartsWith("Only "), "Fewer digits than asked for are certain.");
        }
    }

    [Test]
    public void ComputesAroundAnIntegral()
    {
        var result = Calculate("1 + integral(e^(-x^2), x, -oo, +oo)");
        Assert.That(double.Parse(result["decimal"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(1 + Math.Sqrt(Math.PI)).Within(1e-13));
    }

    [Test]
    public void ChecksTheExampleEquation()
    {
        var result = Calculate("integral(x^3/(e^x-1), x, 0, +oo) = pi^4/15");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result["numerically_equal"]!.GetValue<bool>(), Is.True);
            Assert.That(result["matching_digits"]!.GetValue<int>(), Is.InRange(12, 15));
            Assert.That(result["left"]!["approximate"]!.GetValue<bool>(), Is.True);
            Assert.That(result["right"]!["expression"]!.GetValue<string>(), Is.EqualTo("pi ^ 4 / 15"));
            Assert.That(result.ContainsKey("exactly_equal"), Is.False, "Only two exact sides settle an equation.");
        }
    }

    [Test]
    public void SettlesAnEquationOfExactValues()
    {
        var equal = Calculate("2 + 2 = 4");
        var close = Calculate("1/3 = 0.333");
        var beyondTheDigits = Calculate("1/3 = 333333333333333333333333333333/1000000000000000000000000000000");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(equal["exactly_equal"]!.GetValue<bool>(), Is.True);
            Assert.That(equal["numerically_equal"]!.GetValue<bool>(), Is.True);
            Assert.That(close["exactly_equal"]!.GetValue<bool>(), Is.False);
            Assert.That(close["numerically_equal"]!.GetValue<bool>(), Is.False);
            Assert.That(close["matching_digits"]!.GetValue<int>(), Is.EqualTo(3));
            Assert.That(beyondTheDigits["numerically_equal"]!.GetValue<bool>(), Is.True, "Twenty digits agree.");
            Assert.That(beyondTheDigits["exactly_equal"]!.GetValue<bool>(), Is.False, "The exact values differ all the same.");
        }
    }

    [Test]
    public void RefusesWhatHasNoNumericValue()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Refusal("1/0"), Does.Contain("has no value: AngouriMath evaluates it to NaN"));
            Assert.That(Refusal("x + 1"), Does.Contain("contains the variable x, so it has no numeric value. compute_symbolically works with variables."));
            Assert.That(Refusal("a*b"), Does.Contain("contains the variables a, b,"));
            Assert.That(Refusal("1 < 2"), Does.Contain("is a statement, such as a comparison"));
            Assert.That(Refusal("(10^9)!"), Does.Contain("digits as an exact number"));
            Assert.That(() => Calculate("integral(x^2, x)"), Throws.InstanceOf<MathComputationException>());
            Assert.That(Refusal("sum(1/k, k, 1, 1000)"), Is.EqualTo("'sum(1 / k, k, 1, 1000)' has no value AngouriMath can compute as a single number, as for a sum without a closed form, an infinite sum, or a matrix."), "AngouriMath sums only in closed form; its own message tells a developer to call EvaluableNumerical.");
            Assert.That(Refusal("sum(1/k^2, k, 1, +oo)"), Does.Contain("has no value AngouriMath can compute as a single number"));
            Assert.That(Refusal("[1, 2] + [3, 4]"), Does.Contain("has no value AngouriMath can compute as a single number"));
        }
    }

    [Test]
    public void ComputesDivisorsMultiplesAndSumsInClosedForm()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(Calculate("gcd(12, 18)"), "exact"), Is.EqualTo("6"));
            Assert.That(Text(Calculate("lcm(4, 6, 10)"), "exact"), Is.EqualTo("60"), "Three arguments nest into two calls.");
            Assert.That(Text(Calculate("gcd(2^100, 6^50)"), "exact"), Is.EqualTo("1125899906842624"));
            Assert.That(Text(Calculate("sum(k^2, k, 1, 100)"), "exact"), Is.EqualTo("338350"));
            Assert.That(Text(Calculate("sum(k, k, 1, 10^7)"), "exact"), Is.EqualTo("50000005000000"), "AngouriMath sums in closed form, never term by term.");
            Assert.That(Text(Calculate("sum(k, k, 10, 1)"), "exact"), Is.EqualTo("0"), "An empty sum is 0.");
        }
    }

    [Test]
    public void RefusesAnIntegralItCannotIntegrate()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Refusal("integral(1/x, x, -1, 1)"), Does.StartWith("The integrand of 'integral(1 / x, x, -1, 1)' has no finite value at x = 0, inside the interval of integration."));
            Assert.That(Refusal("integral(sin(x)/x, x, 0, +oo)"), Does.StartWith("The numerical integration of 'integral(sin(x) / x, x, 0, +oo)' did not settle."));
            Assert.That(Refusal("integral(integral(x*y, y, 0, 1), x, 0, 1)"), Does.Contain("depends on the variable of an outer integral"));
        }
    }

    [Test]
    public async Task ExecutesThroughTheComputationService()
    {
        var tool = new CalculateTool(null!, new MathComputationService(NullLogger<MathComputationService>.Instance));
        var result = await tool.ExecuteAsync(Arguments("""{"expression": "547 / 57576", "digits": 5}"""), null!);

        Assert.That(result.JsonContent!["decimal"]!.GetValue<string>(), Is.EqualTo("0.0095005"));
    }

    [Test]
    public void RefusesDigitsOutsideTheRange()
    {
        var tool = new CalculateTool(null!, new MathComputationService(NullLogger<MathComputationService>.Instance));
        var tooMany = Arguments("""{"expression": "1/3", "digits": 101}""");
        var none = Arguments("""{"expression": "1/3", "digits": 0}""");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => tool.ExecuteAsync(tooMany, null!), Throws.ArgumentException.With.Message.EqualTo("Argument 'digits' must be at most 100, but was 101. Leave it out to get 20 significant digits."));
            Assert.That(() => tool.ExecuteAsync(none, null!), Throws.ArgumentException);
        }
    }
}