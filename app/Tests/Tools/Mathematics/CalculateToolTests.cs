using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Tools.Mathematics;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class CalculateToolTests
{
    private static JsonObject Calculate(string expression, int digits = 20) => CalculateTool.Calculate(expression, digits, CancellationToken.None);

    private static string Refusal(string expression) => Assert.Throws<MathComputationException>(() => Calculate(expression))!.Message;

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
            Assert.That(result["expression"]!.GetValue<string>(), Is.EqualTo("547/57576"), "AngouriMath reads a quotient of two integers as a rational number.");
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