using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Tools.Mathematics;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

using AngouriMath.Core;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Mathematics;

/// <summary>
/// Checks compute_symbolically through the computation service, so it runs with the settings of the tools: explicit
/// parsing, and solving without Newton's method.
/// </summary>
[TestFixture]
public sealed class ComputeSymbolicallyToolTests
{
    private static readonly MathComputationService SERVICE = new(NullLogger<MathComputationService>.Instance);

    private static Task<JsonObject> Compute(SymbolicOperation operation, string expression, string? variable = null, int order = 1, string? lowerBound = null, string? upperBound = null, string? approach = null, ApproachFrom direction = ApproachFrom.BothSides) =>
        SERVICE.ComputeAsync(_ => ComputeSymbolicallyTool.Compute(new SymbolicRequest(operation, expression, variable, order, lowerBound, upperBound, approach, direction)));

    private static string Text(JsonObject json, string property) => json[property]!.GetValue<string>();

    private static IEnumerable<string> Issues(JsonObject json) => json["issues"]?.AsArray().Select(issue => issue!.GetValue<string>()) ?? [];

    private static JsonElement Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string RequestRefusal(string json) => Assert.Throws<ArgumentException>(() => ComputeSymbolicallyTool.ReadRequest(Arguments(json)))!.Message;

    [Test]
    public async Task SimplifiesAndStatesTheCondition()
    {
        var identity = await Compute(SymbolicOperation.SIMPLIFY, "sin(x)^2 + cos(x)^2");
        var quotient = await Compute(SymbolicOperation.SIMPLIFY, "(x^2 - 1)/(x - 1)");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(identity, "result"), Is.EqualTo("1"));
            Assert.That(Text(identity, "verification"), Is.EqualTo("passed"));
            Assert.That(identity["complete"]!.GetValue<bool>(), Is.True);
            Assert.That(Text(quotient, "result"), Is.EqualTo("x + 1"));
            Assert.That(Text(quotient, "condition"), Is.EqualTo("not x - 1 = 0"));
            Assert.That(Text(quotient, "verification"), Is.EqualTo("passed"));
        }
    }

    [Test]
    public async Task ExpandsAndFactors()
    {
        var expanded = await Compute(SymbolicOperation.EXPAND, "(x + 1)^3");
        var unchanged = await Compute(SymbolicOperation.EXPAND, "(x + y + z + w)^60");
        var factored = await Compute(SymbolicOperation.FACTOR, "x^4 - 1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(expanded, "result"), Is.EqualTo("1 + 3 * x + 3 * x ^ 2 + x ^ 3"));
            Assert.That(Text(expanded, "verification"), Is.EqualTo("passed"));
            Assert.That(Issues(unchanged), Has.Some.StartsWith("AngouriMath left the expression as it is"));
            Assert.That(Text(factored, "result"), Is.EqualTo("(x + 1) * (x - 1) * (x ^ 2 + 1)"));
            Assert.That(Text(factored, "verification"), Is.EqualTo("passed"));
        }
    }

    [Test]
    public async Task DifferentiatesAndChecksEachOrder()
    {
        var product = await Compute(SymbolicOperation.DIFFERENTIATE, "x^2*sin(x)");
        var third = await Compute(SymbolicOperation.DIFFERENTIATE, "x^5", order: 3);
        var parameter = await Compute(SymbolicOperation.DIFFERENTIATE, "a*x^2", "x");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(product, "result"), Is.EqualTo("x ^ 2 * cos(x) + 2 * x * sin(x)"));
            Assert.That(Text(product, "variable"), Is.EqualTo("x"));
            Assert.That(Text(product, "verification"), Is.EqualTo("passed"));
            Assert.That(Text(third, "result"), Is.EqualTo("60 * x ^ 2"));
            Assert.That(third["order"]!.GetValue<int>(), Is.EqualTo(3));
            Assert.That(Text(third, "verification"), Is.EqualTo("passed"));
            Assert.That(Text(parameter, "verification"), Is.EqualTo("passed"));
        }
    }

    [Test]
    public async Task IntegratesWithoutBounds()
    {
        var power = await Compute(SymbolicOperation.INTEGRATE, "x^2");
        var withoutClosedForm = await Compute(SymbolicOperation.INTEGRATE, "sin(sin(x))");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(power, "result"), Is.EqualTo("x ^ 3 / 3 + C"));
            Assert.That(Text(power, "verification"), Is.EqualTo("passed"));
            Assert.That(Issues(power), Has.Member("C stands for any constant."));
            Assert.That(withoutClosedForm["complete"]!.GetValue<bool>(), Is.False);
            Assert.That(Text(withoutClosedForm, "verification"), Is.EqualTo("not_possible"));
            Assert.That(Issues(withoutClosedForm), Has.Member("AngouriMath found no antiderivative in closed form."));
        }
    }

    [Test]
    public async Task IntegratesWithBoundsAndChecksByQuadrature()
    {
        var polynomial = await Compute(SymbolicOperation.INTEGRATE, "x^2", lowerBound: "0", upperBound: "1");
        var arctangent = await Compute(SymbolicOperation.INTEGRATE, "1/(1 + x^2)", lowerBound: "0", upperBound: "+oo");
        var bose = await Compute(SymbolicOperation.INTEGRATE, "x^3/(e^x - 1)", lowerBound: "0", upperBound: "+oo");
        var pole = await Compute(SymbolicOperation.INTEGRATE, "1/x", lowerBound: "-1", upperBound: "1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(polynomial, "result"), Is.EqualTo("1/3"));
            Assert.That(Text(polynomial, "verification"), Is.EqualTo("passed"));
            Assert.That(Text(arctangent, "result"), Is.EqualTo("pi / 2"));
            Assert.That(Text(arctangent, "verification"), Is.EqualTo("passed"));
            Assert.That(bose["complete"]!.GetValue<bool>(), Is.False, "AngouriMath finds no closed form for pi^4/15.");
            Assert.That(Issues(bose), Has.Member("AngouriMath found no closed form for this integral."));
            Assert.That(Text(pole, "result"), Is.EqualTo("NaN"));
            Assert.That(Issues(pole), Has.Some.StartsWith("AngouriMath answers NaN"));
            Assert.That(Text(pole, "verification"), Is.EqualTo("not_possible"));
        }
    }

    [Test]
    public async Task TakesLimits()
    {
        var sinc = await Compute(SymbolicOperation.LIMIT, "sin(x)/x", approach: "0");
        var fromTheRight = await Compute(SymbolicOperation.LIMIT, "1/x", approach: "0", direction: ApproachFrom.Right);
        var fromBothSides = await Compute(SymbolicOperation.LIMIT, "1/x", approach: "0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Text(sinc, "result"), Is.EqualTo("1"));
            Assert.That(Text(sinc, "direction"), Is.EqualTo("both"));
            Assert.That(Text(sinc, "verification"), Is.EqualTo("not_possible"));
            Assert.That(Text(fromTheRight, "result"), Is.EqualTo("+oo"));
            Assert.That(Text(fromTheRight, "direction"), Is.EqualTo("right"));
            Assert.That(Text(fromBothSides, "result"), Is.EqualTo("NaN"));
            Assert.That(Issues(fromBothSides), Has.Some.StartsWith("AngouriMath answers NaN, which it gives where the limit does not exist"));
        }
    }

    [Test]
    public async Task SolvesAndChecksEverySolution()
    {
        var quadratic = await Compute(SymbolicOperation.SOLVE, "x^2 - 5*x + 6 = 0");
        var complex = await Compute(SymbolicOperation.SOLVE, "x^2 + 1 = 0");
        var periodic = await Compute(SymbolicOperation.SOLVE, "sin(x) = 1/2");
        var withoutEquals = await Compute(SymbolicOperation.SOLVE, "x^2 - 4");
        var tiny = await Compute(SymbolicOperation.SOLVE, "1.5e30*x = 3");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(quadratic["solutions"]!.AsArray().Select(solution => solution!["value"]!.GetValue<string>()), Is.EqualTo(new[] { "2", "3" }));
            Assert.That(tiny["solutions"]![0]!["decimal"]!.GetValue<string>(), Is.EqualTo("2e-30"), "The decimal of a tiny solution keeps its digits, see NumericEvaluator.EvaluateAt.");
            Assert.That(quadratic["solutions"]![0]!["real"]!.GetValue<bool>(), Is.True);
            Assert.That(Text(quadratic, "verification"), Is.EqualTo("passed"));
            Assert.That(complex["solutions"]![0]!["real"]!.GetValue<bool>(), Is.False);
            Assert.That(Text(complex, "verification"), Is.EqualTo("passed"));
            Assert.That(Text(periodic, "verification"), Is.EqualTo("passed"), "n_1 takes integer values in the check.");
            Assert.That(Issues(periodic), Has.Member("n_1 stands for any integer."));
            Assert.That(Issues(withoutEquals), Has.Member("The expression has no '=', so it was solved for expression = 0."));
            Assert.That(withoutEquals["solutions"]!.AsArray(), Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task SaysWhenThereIsNoSolutionInClosedForm()
    {
        var quintic = await Compute(SymbolicOperation.SOLVE, "x^5 + 3*x + 1 = 0");
        var none = await Compute(SymbolicOperation.SOLVE, "x + 1 = x");
        var inequality = await Compute(SymbolicOperation.SOLVE, "x^2 < 4");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(quintic["complete"]!.GetValue<bool>(), Is.False, "Without Newton's method, there are no approximate roots.");
            Assert.That(Text(quintic, "verification"), Is.EqualTo("not_possible"));
            Assert.That(none["solutions"]!.AsArray(), Is.Empty);
            Assert.That(Issues(none), Has.Member("There is no solution."));
            Assert.That(Text(inequality, "result"), Is.EqualTo("(-2; 2)"));
            Assert.That(inequality.ContainsKey("solutions"), Is.False);
        }
    }

    [Test]
    public void RefusesWhatTheOperationCannotTake()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await Compute(SymbolicOperation.SIMPLIFY, "x = 2"), Throws.InstanceOf<MathComputationException>().With.Message.EqualTo("Argument 'expression' is a statement, such as an equation or a comparison, which only the operation 'solve' takes."));
            Assert.That(async () => await Compute(SymbolicOperation.DIFFERENTIATE, "a*x^2"), Throws.InstanceOf<MathComputationException>().With.Message.EqualTo("Argument 'expression' has the variables a, x, so argument 'variable' has to say which one to differentiate by."));
            Assert.That(async () => await Compute(SymbolicOperation.INTEGRATE, "2"), Throws.InstanceOf<MathComputationException>().With.Message.EqualTo("Argument 'expression' has no variable to integrate over."));
            Assert.That(async () => await Compute(SymbolicOperation.SOLVE, "x^2 = 2", "pi"), Throws.InstanceOf<MathComputationException>().With.Message.EqualTo("Argument 'variable' must name a variable, such as x, but was 'pi'."));
            Assert.That(async () => await Compute(SymbolicOperation.EXPAND, "(x + y)^500"), Throws.InstanceOf<MathComputationException>().With.Message.Contains("raises a sum to the power of 500"));
        }
    }

    [Test]
    public void RefusesArgumentsTheOperationDoesNotUse()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RequestRefusal("""{"operation": "simplify", "expression": "x", "variable": "x"}"""), Is.EqualTo("Argument 'variable' is not used by the operation 'simplify'. Leave it out."));
            Assert.That(RequestRefusal("""{"operation": "integrate", "expression": "x", "order": 2}"""), Is.EqualTo("Argument 'order' is not used by the operation 'integrate'. Leave it out."));
            Assert.That(RequestRefusal("""{"operation": "integrate", "expression": "x", "lower_bound": "0"}"""), Is.EqualTo("Argument 'lower_bound' needs argument 'upper_bound' as well, since a definite integral has two bounds. Leave both out for an antiderivative."));
            Assert.That(RequestRefusal("""{"operation": "limit", "expression": "1/x"}"""), Does.StartWith("Argument 'approach' is required for the operation 'limit'"));
            Assert.That(RequestRefusal("""{"operation": "solve", "expression": "x = 1", "direction": "left"}"""), Is.EqualTo("Argument 'direction' is not used by the operation 'solve'. Leave it out."));
            Assert.That(RequestRefusal("""{"operation": "differentiate", "expression": "x", "order": 11}"""), Is.EqualTo("Argument 'order' must be at most 10, but was 11. Leave it out for the first derivative."));
            Assert.That(RequestRefusal("""{"operation": "derive", "expression": "x"}"""), Is.EqualTo("Argument 'operation' must be one of simplify, expand, factor, differentiate, integrate, limit, solve, but was 'derive'."));
        }
    }

    [Test]
    public void ReadsTheArgumentsOfALimit()
    {
        var request = ComputeSymbolicallyTool.ReadRequest(Arguments("""{"operation": "limit", "expression": "1/x", "variable": null, "approach": "0", "direction": "left"}"""));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(request.Operation, Is.EqualTo(SymbolicOperation.LIMIT));
            Assert.That(request.Variable, Is.Null, "In strict mode, a model passes null for an argument it leaves out.");
            Assert.That(request.Approach, Is.EqualTo("0"));
            Assert.That(request.Direction, Is.EqualTo(ApproachFrom.Left));
            Assert.That(request.Order, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task ExecutesThroughTheComputationService()
    {
        var tool = new ComputeSymbolicallyTool(null!, SERVICE);
        var result = await tool.ExecuteAsync(Arguments("""{"operation": "factor", "expression": "x^2 - 5*x + 6"}"""), null!);
        Assert.That(result.JsonContent!["result"]!.GetValue<string>(), Is.EqualTo("(x - 3) * (x - 2)"));
    }
}