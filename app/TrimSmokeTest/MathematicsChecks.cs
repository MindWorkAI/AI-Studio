using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Tools.Mathematics;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

using AngouriMath.Core;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Lets the Mathematics tools compute what their tests compute, because AngouriMath is marked as trimmable.
/// </summary>
/// <remarks>
/// The trimmer therefore removes every part of AngouriMath which nothing but reflection reaches, and
/// Trimming.props suppresses the warnings which would name such a part. Each check takes another part of it:
/// the parser, exact and precise arithmetic, LaTeX, Compile for the numerical integration, and the symbolic
/// operations together with their numerical check. The expected values are those of the tests in
/// app/Tests/Tools/Mathematics, which run untrimmed.
/// </remarks>
public static class MathematicsChecks
{
    private static readonly MathComputationService SERVICE = new(NullLogger<MathComputationService>.Instance);

    /// <summary>
    /// Returns a description of every computation which fails or gives another result than untrimmed.
    /// </summary>
    /// <returns>One line per failure, naming the computation and what went wrong.</returns>
    public static IReadOnlyList<string> FindFailures()
    {
        var failures = new List<string>();

        //
        // The parser, exact arithmetic, the decimal expansion, and LaTeX:
        //
        Expect(failures, "calculate 547 / 57576", Calculation("547 / 57576", 20), new()
        {
            ["exact"] = "547/57576",
            ["exact_latex"] = @"\frac{547}{57576}",
            ["decimal"] = "0.0095004863137418368765",
        });

        //
        // Arithmetic with many digits, which takes the guard digits:
        //
        Expect(failures, "calculate pi^4/15 to 60 digits", Calculation("pi^4/15", 60), new()
        {
            ["decimal"] = "6.49393940226682914909602217924700741664850571151236144609786",
            ["digits"] = "60",
        });

        //
        // A tiny value, which AngouriMath's downcasting would turn into 0, and one evaluated without it:
        //
        Expect(failures, "calculate the energy of a photon of 500 nm", Calculation("6.62607015e-34 * 299792458 / 500e-9", 20), new()
        {
            ["decimal"] = "3.9728917142978574e-19",
            ["digits"] = "20",
        });

        Expect(failures, "calculate e^(-100) * pi", Calculation("e^(-100) * pi", 20), new()
        {
            ["decimal"] = "1.1686963357062938093e-43",
        });

        //
        // The numerical integration, whose integrand AngouriMath compiles:
        //
        Expect(failures, "check the integral of x^3/(e^x-1) from 0 to infinity", Calculation("integral(x^3/(e^x-1), x, 0, +oo) = pi^4/15", 20), new()
        {
            ["numerically_equal"] = "true",
            ["left/approximate"] = "true",
            ["right/expression"] = "pi ^ 4 / 15",
        });

        //
        // The symbolic operations, each with its numerical check:
        //
        Expect(failures, "simplify (x^2 - 1)/(x - 1)", Symbolic(SymbolicOperation.SIMPLIFY, "(x^2 - 1)/(x - 1)"), new()
        {
            ["result"] = "x + 1",
            ["condition"] = "not x - 1 = 0",
            ["verification"] = "passed",
        });

        Expect(failures, "expand (x + 1)^3", Symbolic(SymbolicOperation.EXPAND, "(x + 1)^3"), new()
        {
            ["result"] = "1 + 3 * x + 3 * x ^ 2 + x ^ 3",
            ["verification"] = "passed",
        });

        Expect(failures, "factor x^4 - 1", Symbolic(SymbolicOperation.FACTOR, "x^4 - 1"), new()
        {
            ["result"] = "(x + 1) * (x - 1) * (x ^ 2 + 1)",
            ["verification"] = "passed",
        });

        Expect(failures, "differentiate x^2*sin(x)", Symbolic(SymbolicOperation.DIFFERENTIATE, "x^2*sin(x)"), new()
        {
            ["result"] = "x ^ 2 * cos(x) + 2 * x * sin(x)",
            ["verification"] = "passed",
        });

        Expect(failures, "integrate x^2", Symbolic(SymbolicOperation.INTEGRATE, "x^2"), new()
        {
            ["result"] = "x ^ 3 / 3 + C",
            ["verification"] = "passed",
        });

        Expect(failures, "integrate 1/(1 + x^2) from 0 to infinity", Symbolic(SymbolicOperation.INTEGRATE, "1/(1 + x^2)", lowerBound: "0", upperBound: "+oo"), new()
        {
            ["result"] = "pi / 2",
            ["verification"] = "passed",
        });

        Expect(failures, "take the limit of sin(x)/x at 0", Symbolic(SymbolicOperation.LIMIT, "sin(x)/x", approach: "0"), new()
        {
            ["result"] = "1",
        });

        Expect(failures, "solve x^2 - 5*x + 6 = 0", Symbolic(SymbolicOperation.SOLVE, "x^2 - 5*x + 6 = 0"), new()
        {
            ["solutions/0/value"] = "2",
            ["solutions/1/value"] = "3",
            ["verification"] = "passed",
        });

        return failures;
    }

    private static Func<JsonObject> Calculation(string expression, int digits) => () => SERVICE.ComputeAsync(token => CalculateTool.Calculate(expression, digits, token)).GetAwaiter().GetResult();

    private static Func<JsonObject> Symbolic(SymbolicOperation operation, string expression, string? lowerBound = null, string? upperBound = null, string? approach = null) => () => SERVICE.ComputeAsync(_ => ComputeSymbolicallyTool.Compute(new SymbolicRequest(operation, expression, null, 1, lowerBound, upperBound, approach, ApproachFrom.BothSides))).GetAwaiter().GetResult();

    /// <summary>
    /// Runs a computation and compares the values of its result with the expected ones.
    /// </summary>
    /// <param name="failures">Where to add a failure.</param>
    /// <param name="task">What the computation does, completing "The Mathematics tools could not ...".</param>
    /// <param name="compute">The computation.</param>
    /// <param name="expected">The expected values, by their path in the result, e.g., solutions/0/value.</param>
    private static void Expect(List<string> failures, string task, Func<JsonObject> compute, Dictionary<string, string> expected)
    {
        JsonObject result;
        try
        {
            result = compute();
        }
        catch (Exception exception)
        {
            //
            // A member the trimmer removed surfaces as the innermost exception, e.g., below a TypeInitializationException:
            //
            var cause = exception.GetBaseException();
            failures.Add($"The Mathematics tools could not {task}: {cause.GetType().Name}: {cause.Message}");
            return;
        }

        foreach (var (path, value) in expected)
        {
            var actual = TextAt(result, path);
            if (actual != value)
                failures.Add($"The Mathematics tools gave '{actual ?? "nothing"}' instead of '{value}' for {path} when asked to {task}.");
        }
    }

    private static string? TextAt(JsonNode? node, string path)
    {
        foreach (var step in path.Split('/'))
        {
            if (int.TryParse(step, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                node = node is JsonArray array && index < array.Count ? array[index] : null;
            else
                node = node is JsonObject json ? json[step] : null;
        }

        if (node is null)
            return null;

        return node.GetValueKind() switch
        {
            JsonValueKind.String => node.GetValue<string>(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => node.ToString(),
        };
    }
}