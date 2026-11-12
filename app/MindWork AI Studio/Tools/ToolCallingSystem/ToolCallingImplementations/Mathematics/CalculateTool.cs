using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mathematics;
using AIStudio.Tools.PluginSystem;

using AngouriMath;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

/// <summary>
/// Evaluates an expression numerically, exactly where it can, and checks an equation by comparing its sides.
/// </summary>
/// <remarks>
/// Models guess digits, slip with fractions, and assert identities they never checked. This tool
/// computes locally with AngouriMath: a rational value stays exact, every other decimal carries only
/// the digits the computation vouches for, and a definite integral is integrated numerically, also
/// over an infinite interval, see NumericEvaluator.<br/><br/>
/// Its result holds nothing but the expression of the model, as AngouriMath read it, and values
/// computed from it, so it opens no way for content from outside and needs no prompt injection
/// filter. Nothing leaves AI Studio either. It belongs to the Mathematics collection, see
/// MathematicsToolCollection.
/// </remarks>
public sealed class CalculateTool(SettingsManager settingsManager, MathComputationService computationService) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(CalculateTool).Namespace, nameof(CalculateTool));

    private const string EXPRESSION_ARGUMENT = "expression";
    private const string DIGITS_ARGUMENT = "digits";

    private const int DEFAULT_DIGITS = 20;

    /// <summary>
    /// The syntax both Mathematics tools read, described for the model.
    /// </summary>
    internal const string SYNTAX = "Plain syntax, not LaTeX: + - * / ^ and ! for the factorial, with every multiplication written as *, e.g., 2*pi*x. Functions take their arguments in parentheses: sqrt, exp, ln, log(x) to base 10, log(b, x) to base b, sin, cos, tan, arcsin, arccos, arctan, sinh, cosh, tanh, abs, floor, ceil, gamma, binomial(n, k), gcd(a, b), lcm(a, b), erf. Constants are pi, e, and i; infinity is +oo or -oo. A definite integral is integral(f, x, a, b), a limit limit(f, x, a), and a sum sum(f, k, a, b) over the integers k from a to b. A sum is computed only in closed form, such as for powers of k; any other sum has to be written out term by term. Not-equal is written <>, and | is not an operator.";

    public string ImplementationKey => ToolSelectionRules.CALCULATE_TOOL_ID;

    public ToolDefinition GetDefinition() => new()
    {
        Id = ToolSelectionRules.CALCULATE_TOOL_ID,
        ImplementationKey = ToolSelectionRules.CALCULATE_TOOL_ID,

        // No minimum confidence of its own: the Mathematics collection states it, see MathematicsToolCollection.
        SystemPromptInstructions = """
                                   Use `calculate` for every number beyond simple mental arithmetic, instead of working out digits yourself, and to check a numerical claim before you make it.
                                   - It reads plain syntax, not LaTeX. Translate LaTeX first, write every multiplication with `*`, and give each function its arguments in parentheses, e.g., `2*pi*sqrt(x)` for `2\pi\sqrt{x}`.
                                   - `expression` in the result shows how your input was read. Compare it with what you meant.
                                   - Give the decimal with the digits returned, never more. `exact` is the exact value of a rational result.
                                   - When `approximate` is true, a definite integral was integrated numerically: say so, together with its `estimated_error`.
                                   - For an equation, `numerically_equal` means that both sides agree in all `matching_digits`. That supports the equation, but does not prove it; say so. Only when both sides are exact, `exactly_equal` settles it.
                                   - When the tool refuses an expression, its message says how the tool reads it and how it is written instead.
                                   """,
        Function = new()
        {
            Name = ToolSelectionRules.CALCULATE_TOOL_ID,
            DescriptionForLLM = "Evaluate a mathematical expression locally, exactly where possible: fractions stay exact, and a decimal carries only the digits the computation vouches for. Definite integrals, also over infinite intervals, are integrated numerically. An expression with one '=' is an equation, whose two sides are evaluated and compared.",
            Parameters = ToolParameterSchemaBuilder.Create()
                .RequiredString(EXPRESSION_ARGUMENT, $"The expression, without variables except those bound by an integral or a limit. {SYNTAX} Example: integral(x^3/(e^x - 1), x, 0, +oo) = pi^4/15")
                .OptionalInteger(DIGITS_ARGUMENT, $"Optional: how many significant digits the decimal should have, from 1 to {MathLimits.MAX_DIGITS}. Fewer come back when the computation cannot vouch for more, e.g., about 14 after a numerical integration. Leave it out to get {DEFAULT_DIGITS}.")
                .Build(),
        },
    };

    public string Icon => AppIcons.MATHEMATICS;

    public bool IsAvailable => PreviewFeatures.PRE_MATHEMATICS_2026.IsEnabled(settingsManager);

    // Everything is computed locally:
    public ToolOutboundData OutboundData => ToolOutboundData.NONE;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => TB("Calculate");

    public string GetDescription() => TB("Lets the AI calculate exactly instead of guessing digits, e.g., fractions, roots, or integrals, and check an equation. Everything is computed on your computer.");

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var text = ToolArgumentReader.ReadRequiredString(arguments, EXPRESSION_ARGUMENT);
        var digits = ReadDigits(arguments);
        var result = await computationService.ComputeAsync(computationToken => Calculate(text, digits, computationToken), token);
        return new ToolExecutionResult { JsonContent = result };
    }

    private static int ReadDigits(JsonElement arguments)
    {
        var digits = ToolArgumentReader.ReadOptionalPositiveInt(arguments, DIGITS_ARGUMENT, $"to get {DEFAULT_DIGITS} significant digits");
        if (digits is null)
            return DEFAULT_DIGITS;

        if (digits > MathLimits.MAX_DIGITS)
            throw new ArgumentException($"Argument '{DIGITS_ARGUMENT}' must be at most {MathLimits.MAX_DIGITS}, but was {digits}. Leave it out to get {DEFAULT_DIGITS} significant digits.");

        return digits.Value;
    }

    /// <summary>
    /// Evaluates the expression, or both sides of an equation, and writes the result for the model.
    /// </summary>
    internal static JsonObject Calculate(string text, int digits, CancellationToken token)
    {
        var expression = MathExpressionReader.Read(text, EXPRESSION_ARGUMENT);
        var result = new JsonObject
        {
            ["expression"] = MathFormatting.ToText(expression, "The expression as read"),
            ["latex"] = MathFormatting.ToLatex(expression, "The LaTeX of the expression"),
        };

        if (expression is not Entity.Equalsf equation)
        {
            var value = NumericEvaluator.Evaluate(expression, digits, token);
            Describe(value, result);
            return result;
        }

        var left = NumericEvaluator.Evaluate(equation.Left, digits, token);
        var right = NumericEvaluator.Evaluate(equation.Right, digits, token);
        var certainDigits = Math.Min(left.Digits, right.Digits);
        var matchingDigits = Math.Min(MathFormatting.CountMatchingDigits(left.Value, right.Value, digits), certainDigits);
        result["left"] = Describe(left, new JsonObject { ["expression"] = MathFormatting.ToText(equation.Left, "The left side") });
        result["right"] = Describe(right, new JsonObject { ["expression"] = MathFormatting.ToText(equation.Right, "The right side") });
        result["numerically_equal"] = certainDigits > 0 && matchingDigits >= certainDigits;
        result["matching_digits"] = matchingDigits;

        // Two exact values settle the question, also where they differ only beyond the digits compared:
        if (left.Exact is not null && right.Exact is not null)
            result["exactly_equal"] = left.Exact.Equals(right.Exact);

        return result;
    }

    private static JsonObject Describe(NumericValue value, JsonObject json)
    {
        if (value.Exact is not null)
        {
            json["exact"] = MathFormatting.ToText(value.Exact, "The exact value");
            json["exact_latex"] = MathFormatting.ToLatex(value.Exact, "The LaTeX of the exact value");
        }

        json["decimal"] = MathFormatting.FormatNumber(value.Value, Math.Max(value.Digits, 1));
        json["digits"] = value.Digits;
        json["approximate"] = value.Approximate;
        if (value.EstimatedError is { } estimatedError)
            json["estimated_error"] = MathFormatting.FormatDecimal(estimatedError, 2);

        if (value.Issues.Count > 0)
            json["issues"] = new JsonArray([..value.Issues.Select(issue => JsonValue.Create(issue))]);

        return json;
    }
}