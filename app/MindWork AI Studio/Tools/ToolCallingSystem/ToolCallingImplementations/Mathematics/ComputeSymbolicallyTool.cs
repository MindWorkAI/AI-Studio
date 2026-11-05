using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mathematics;
using AIStudio.Tools.PluginSystem;

using AngouriMath;
using AngouriMath.Core;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

/// <summary>
/// Computes with formulas: simplifies, expands, factors, differentiates, integrates, takes limits, and solves.
/// </summary>
/// <remarks>
/// AngouriMath does the algebra locally, and the tool checks each answer numerically wherever it can,
/// see NumericCheck: a symbolic answer is only as good as the library which gave it, and AngouriMath
/// changes answers between releases. Where AngouriMath finds no closed form, the result says so
/// instead of passing the unsolved problem off as an answer.<br/><br/>
/// The arguments are checked strictly against the operation: an argument which the operation does
/// not use is refused rather than ignored, since the model meant something by it.<br/><br/>
/// Like Calculate, its result holds nothing but the expression of the model and what was computed
/// from it, so it needs no prompt injection filter, and nothing leaves AI Studio. It belongs to the
/// Mathematics collection, see MathematicsToolCollection.
/// </remarks>
public sealed class ComputeSymbolicallyTool(SettingsManager settingsManager, MathComputationService computationService) : IToolImplementation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ComputeSymbolicallyTool).Namespace, nameof(ComputeSymbolicallyTool));

    private const string OPERATION_ARGUMENT = "operation";
    private const string EXPRESSION_ARGUMENT = "expression";
    private const string VARIABLE_ARGUMENT = "variable";
    private const string ORDER_ARGUMENT = "order";
    private const string LOWER_BOUND_ARGUMENT = "lower_bound";
    private const string UPPER_BOUND_ARGUMENT = "upper_bound";
    private const string APPROACH_ARGUMENT = "approach";
    private const string DIRECTION_ARGUMENT = "direction";

    private const int MAX_ORDER = 10;

    private const int SOLUTION_DIGITS = 15;

    private const int MAX_ECHO_LENGTH = 40;

    /// <summary>
    /// The relative difference within which a definite integral agrees with its numerical integration,
    /// at least: a double carries no more.
    /// </summary>
    private const double MIN_QUADRATURE_TOLERANCE = 1e-12;

    /// <summary>
    /// The precision of a value which ends up as a double, such as a bound of an integral.
    /// </summary>
    private const int DOUBLE_PRECISION = 30;

    private static readonly Dictionary<string, SymbolicOperation> OPERATIONS = new(StringComparer.Ordinal)
    {
        ["simplify"] = SymbolicOperation.SIMPLIFY,
        ["expand"] = SymbolicOperation.EXPAND,
        ["factor"] = SymbolicOperation.FACTOR,
        ["differentiate"] = SymbolicOperation.DIFFERENTIATE,
        ["integrate"] = SymbolicOperation.INTEGRATE,
        ["limit"] = SymbolicOperation.LIMIT,
        ["solve"] = SymbolicOperation.SOLVE,
    };

    private static readonly Dictionary<string, ApproachFrom> DIRECTIONS = new(StringComparer.Ordinal)
    {
        ["both"] = ApproachFrom.BothSides,
        ["left"] = ApproachFrom.Left,
        ["right"] = ApproachFrom.Right,
    };

    private static readonly string[] OPERATION_NAMES = [..OPERATIONS.Keys];

    private static readonly string[] DIRECTION_NAMES = [..DIRECTIONS.Keys];

    public string ImplementationKey => ToolSelectionRules.COMPUTE_SYMBOLICALLY_TOOL_ID;

    public ToolDefinition GetDefinition() => new()
    {
        Id = ToolSelectionRules.COMPUTE_SYMBOLICALLY_TOOL_ID,
        ImplementationKey = ToolSelectionRules.COMPUTE_SYMBOLICALLY_TOOL_ID,

        // No minimum confidence of its own: the Mathematics collection states it, see MathematicsToolCollection.
        SystemPromptInstructions = """
                                   Use `compute_symbolically` to simplify, expand, or factor an expression, to differentiate or integrate it, to take a limit, or to solve an equation or an inequality, instead of doing the algebra yourself.
                                   - It reads the same plain syntax as `calculate`, not LaTeX.
                                   - `result` is the answer in plain syntax, and `result_latex` the same for your reply. A `condition` says where the result holds, e.g., `not x - 1 = 0`; mention it.
                                   - When `complete` is false, the tool found no closed form, and `result` still holds the integral, the limit, or the equation. Never present it as an answer; `calculate` gives a definite integral numerically.
                                   - `verification` says whether the result was checked numerically: `passed`, `failed`, or `not_possible`. Never present a result with `failed` as correct, and say when the check was `not_possible`.
                                   - Solutions may be complex; `real` tells for each one with a value. n_1 stands for any integer, r_1 for any real number, and C for any constant.
                                   """,
        Function = new()
        {
            Name = ToolSelectionRules.COMPUTE_SYMBOLICALLY_TOOL_ID,
            DescriptionForLLM = "Compute with formulas locally: simplify, expand, or factor an expression, differentiate it, integrate it with or without bounds, take a limit, or solve an equation or inequality. Each result is checked numerically where possible, and says when no closed form was found.",
            Parameters = ToolParameterSchemaBuilder.Create()
                .RequiredEnum(OPERATION_ARGUMENT, "What to do with the expression.", OPERATION_NAMES)
                .RequiredString(EXPRESSION_ARGUMENT, $"The expression, or for solve the equation or inequality. {CalculateTool.SYNTAX} Example: x^2 - 5*x + 6 = 0")
                .OptionalString(VARIABLE_ARGUMENT, "For differentiate, integrate, limit, and solve: the variable to differentiate by, integrate over, approach in, or solve for, e.g., x. Leave it out when the expression has only one variable, and always for simplify, expand, and factor.")
                .OptionalInteger(ORDER_ARGUMENT, $"Only for differentiate: how often to differentiate, from 1 to {MAX_ORDER}. Leave it out for the first derivative.")
                .OptionalString(LOWER_BOUND_ARGUMENT, "Only for integrate: the lower bound of a definite integral, such as 0 or -oo. Give both bounds or none; leave both out for an antiderivative.")
                .OptionalString(UPPER_BOUND_ARGUMENT, "Only for integrate: the upper bound of a definite integral, such as pi or +oo.")
                .OptionalString(APPROACH_ARGUMENT, "Only for limit, and required there: the point the variable approaches, such as 0 or +oo.")
                .OptionalEnum(DIRECTION_ARGUMENT, "Only for limit: approach from both sides, from the left, or from the right. Leave it out for both sides.", DIRECTION_NAMES)
                .Build(),
        },
    };

    public string Icon => Icons.Material.Filled.Functions;

    public bool IsAvailable => PreviewFeatures.PRE_MATHEMATICS_2026.IsEnabled(settingsManager);

    // Everything is computed locally:
    public ToolOutboundData OutboundData => ToolOutboundData.NONE;

    public IReadOnlySet<string> SensitiveTraceArgumentNames => new HashSet<string>(StringComparer.Ordinal);

    public string GetDisplayName() => TB("Compute Symbolically");

    public string GetDescription() => TB("Lets the AI work with formulas: simplify and factor them, differentiate and integrate them, take limits, and solve equations. Every result is checked numerically where possible, right on your computer.");

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default)
    {
        var request = ReadRequest(arguments);
        var result = await computationService.ComputeAsync(_ => Compute(request), token);
        return new ToolExecutionResult { JsonContent = result };
    }

    /// <summary>
    /// Reads the arguments, and refuses each one which the operation does not use.
    /// </summary>
    internal static SymbolicRequest ReadRequest(JsonElement arguments)
    {
        var operationName = ToolArgumentReader.ReadRequiredString(arguments, OPERATION_ARGUMENT);
        if (!OPERATIONS.TryGetValue(operationName, out var operation))
            throw new ArgumentException($"Argument '{OPERATION_ARGUMENT}' must be one of {string.Join(", ", OPERATION_NAMES)}, but was '{operationName.Shorten(MAX_ECHO_LENGTH)}'.");

        var expression = ToolArgumentReader.ReadRequiredString(arguments, EXPRESSION_ARGUMENT);
        var variable = ToolArgumentReader.ReadOptionalString(arguments, VARIABLE_ARGUMENT, "to take the only variable of the expression");
        var order = ToolArgumentReader.ReadOptionalPositiveInt(arguments, ORDER_ARGUMENT, "for the first derivative");
        var lowerBound = ToolArgumentReader.ReadOptionalString(arguments, LOWER_BOUND_ARGUMENT, "together with the upper bound for an antiderivative");
        var upperBound = ToolArgumentReader.ReadOptionalString(arguments, UPPER_BOUND_ARGUMENT, "together with the lower bound for an antiderivative");
        var approach = ToolArgumentReader.ReadOptionalString(arguments, APPROACH_ARGUMENT, "for any operation but limit");
        var direction = ToolArgumentReader.ReadOptionalChoice(arguments, DIRECTION_ARGUMENT, DIRECTION_NAMES, "to approach from both sides");

        if (variable is not null && operation is SymbolicOperation.SIMPLIFY or SymbolicOperation.EXPAND or SymbolicOperation.FACTOR)
            throw NotUsed(VARIABLE_ARGUMENT, operationName);

        if (variable is "")
            throw new ArgumentException($"Argument '{VARIABLE_ARGUMENT}' must name a variable, such as x, but was empty. Leave it out to take the only variable of the expression.");

        if (order is not null && operation is not SymbolicOperation.DIFFERENTIATE)
            throw NotUsed(ORDER_ARGUMENT, operationName);

        if (order > MAX_ORDER)
            throw new ArgumentException($"Argument '{ORDER_ARGUMENT}' must be at most {MAX_ORDER}, but was {order}. Leave it out for the first derivative.");

        if ((lowerBound is not null || upperBound is not null) && operation is not SymbolicOperation.INTEGRATE)
            throw NotUsed(lowerBound is not null ? LOWER_BOUND_ARGUMENT : UPPER_BOUND_ARGUMENT, operationName);

        if ((lowerBound is null) != (upperBound is null))
            throw new ArgumentException($"Argument '{(lowerBound is null ? UPPER_BOUND_ARGUMENT : LOWER_BOUND_ARGUMENT)}' needs argument '{(lowerBound is null ? LOWER_BOUND_ARGUMENT : UPPER_BOUND_ARGUMENT)}' as well, since a definite integral has two bounds. Leave both out for an antiderivative.");

        if (approach is not null && operation is not SymbolicOperation.LIMIT)
            throw NotUsed(APPROACH_ARGUMENT, operationName);

        if (approach is null && operation is SymbolicOperation.LIMIT)
            throw new ArgumentException($"Argument '{APPROACH_ARGUMENT}' is required for the operation 'limit': it names the point the variable approaches, such as 0 or +oo.");

        if (direction is not null && operation is not SymbolicOperation.LIMIT)
            throw NotUsed(DIRECTION_ARGUMENT, operationName);

        return new(operation, expression, variable, order ?? 1, lowerBound, upperBound, approach, direction is null ? ApproachFrom.BothSides : DIRECTIONS[direction]);
    }

    private static ArgumentException NotUsed(string argument, string operationName) => new($"Argument '{argument}' is not used by the operation '{operationName}'. Leave it out.");

    /// <summary>
    /// Carries out the operation and writes the result for the model.
    /// </summary>
    internal static JsonObject Compute(SymbolicRequest request)
    {
        var expression = MathExpressionReader.Read(request.Expression, EXPRESSION_ARGUMENT);
        MathGuards.CheckExactSize(expression);
        MathGuards.CheckExpansion(expression);
        if (expression is Entity.Statement && request.Operation is not SymbolicOperation.SOLVE)
            throw new MathComputationException($"Argument '{EXPRESSION_ARGUMENT}' is a statement, such as an equation or a comparison, which only the operation 'solve' takes.");

        var json = new JsonObject
        {
            ["operation"] = OPERATIONS.First(pair => pair.Value == request.Operation).Key,
            ["expression"] = MathFormatting.ToText(expression, "The expression as read"),
            ["latex"] = MathFormatting.ToLatex(expression, "The LaTeX of the expression"),
        };

        var issues = new List<string>();
        switch (request.Operation)
        {
            case SymbolicOperation.SIMPLIFY:
                Transformed(json, issues, expression, expression.Simplify(), "The tool found nothing to simplify.");
                break;

            case SymbolicOperation.EXPAND:
                Transformed(json, issues, expression, expression.Expand(), "The tool left the expression as it is, e.g., since its expansion would have more than 2,000 terms.");
                break;

            case SymbolicOperation.FACTOR:
                Transformed(json, issues, expression, expression.Factorize(), "The tool found no factorization.");
                break;

            case SymbolicOperation.DIFFERENTIATE:
                Differentiate(json, issues, expression, VariableOf(request, expression, "differentiate by"), request.Order);
                break;

            case SymbolicOperation.INTEGRATE:
                Integrate(json, issues, expression, VariableOf(request, expression, "integrate over"), request);
                break;

            case SymbolicOperation.LIMIT:
                Limit(json, issues, expression, VariableOf(request, expression, "take the limit in"), request);
                break;

            case SymbolicOperation.SOLVE:
                Solve(json, issues, expression, VariableOf(request, expression, "solve for"));
                break;
        }

        if (issues.Count > 0)
            json["issues"] = new JsonArray([..issues.Select(issue => JsonValue.Create(issue))]);

        return json;
    }

    /// <summary>
    /// The variable of the operation: the one the model named, or else the only one of the expression.
    /// </summary>
    private static Entity.Variable VariableOf(SymbolicRequest request, Entity expression, string verb)
    {
        if (request.Variable is not null)
        {
            var named = MathExpressionReader.Read(request.Variable, VARIABLE_ARGUMENT);
            if (named is not Entity.Variable variable || named is Entity.Constant)
                throw new MathComputationException($"Argument '{VARIABLE_ARGUMENT}' must name a variable, such as x, but was '{request.Variable.Shorten(MAX_ECHO_LENGTH)}'.");

            return variable;
        }

        var variables = MathVariables.FreeIn(expression);
        return variables.Count switch
        {
            1 => variables[0],
            0 => throw new MathComputationException($"Argument '{EXPRESSION_ARGUMENT}' has no variable to {verb}."),
            _ => throw new MathComputationException($"Argument '{EXPRESSION_ARGUMENT}' has the variables {string.Join(", ", variables.Select(variable => variable.Name))}, so argument '{VARIABLE_ARGUMENT}' has to say which one to {verb}."),
        };
    }

    private static void Transformed(JsonObject json, List<string> issues, Entity expression, Entity result, string unchanged)
    {
        if (result.Equals(expression))
            issues.Add(unchanged);

        Result(json, issues, expression, result);
        json["complete"] = true;
        json["verification"] = Verification(NumericCheck.AreEqual(expression, result));
    }

    private static void Differentiate(JsonObject json, List<string> issues, Entity expression, Entity.Variable variable, int order)
    {
        // One order at a time, so each step is checked against the one before:
        var derivative = expression;
        var outcome = NumericCheckOutcome.PASSED;
        for (var step = 0; step < order; step++)
        {
            var next = derivative.Differentiate(variable).Simplify();
            outcome = Combined(outcome, NumericCheck.IsDerivativeOf(next, derivative, variable));
            derivative = next;
        }

        json["variable"] = variable.Name;
        json["order"] = order;
        Result(json, issues, expression, derivative);
        json["complete"] = true;
        json["verification"] = Verification(outcome);
    }

    private static void Integrate(JsonObject json, List<string> issues, Entity expression, Entity.Variable variable, SymbolicRequest request)
    {
        json["variable"] = variable.Name;
        if (request.LowerBound is null || request.UpperBound is null)
        {
            var antiderivative = expression.Integrate(variable);
            var complete = !Contains<Entity.Integralf>(antiderivative);
            Result(json, issues, expression, antiderivative);
            json["complete"] = complete;
            json["verification"] = Verification(complete ? NumericCheck.IsDerivativeOf(expression, antiderivative, variable) : NumericCheckOutcome.NOT_POSSIBLE);
            if (!complete)
                issues.Add("The tool found no antiderivative in closed form.");

            return;
        }

        var lower = MathExpressionReader.Read(request.LowerBound, LOWER_BOUND_ARGUMENT);
        var upper = MathExpressionReader.Read(request.UpperBound, UPPER_BOUND_ARGUMENT);
        MathGuards.CheckExactSize(lower);
        MathGuards.CheckExactSize(upper);
        json["lower_bound"] = MathFormatting.ToText(lower, "The lower bound");
        json["upper_bound"] = MathFormatting.ToText(upper, "The upper bound");

        var integral = MathS.Integral(expression, variable, lower, upper).InnerSimplified.Simplify();
        var closed = !Contains<Entity.Integralf>(integral);
        Result(json, issues, expression, integral);
        json["complete"] = closed;
        if (!closed)
            issues.Add("The tool found no closed form for this integral.");
        else if (IsNaN(integral))
            issues.Add("The result is NaN: the integral has no value, e.g., since it diverges across a pole.");

        json["verification"] = Verification(closed ? CheckByQuadrature(integral, expression, variable, lower, upper) : NumericCheckOutcome.NOT_POSSIBLE);
    }

    /// <summary>
    /// Compares the value of a definite integral with its numerical integration, where both are numbers.
    /// </summary>
    private static NumericCheckOutcome CheckByQuadrature(Entity integral, Entity integrand, Entity.Variable variable, Entity lower, Entity upper)
    {
        if (MathVariables.FreeIn(integrand).Any(free => free != variable) || MathVariables.HasAny(lower) || MathVariables.HasAny(upper) || MathVariables.HasAny(integral))
            return NumericCheckOutcome.NOT_POSSIBLE;

        try
        {
            var value = NumericEvaluator.EvaluateAt(integral, DOUBLE_PRECISION);
            if (!value.ImaginaryPart.EDecimal.IsZero)
                return NumericCheckOutcome.NOT_POSSIBLE;

            var exact = value.RealPart.EDecimal.ToDouble();
            var numeric = DoubleExponentialQuadrature.Integrate(integrand.Compile<double, double>(variable), BoundOf(lower), BoundOf(upper));
            if (numeric.Status is not QuadratureStatus.CONVERGED || !double.IsFinite(exact))
                return NumericCheckOutcome.NOT_POSSIBLE;

            var tolerance = Math.Max(10 * numeric.EstimatedError, MIN_QUADRATURE_TOLERANCE * Math.Max(1, Math.Abs(exact)));
            return Math.Abs(exact - numeric.Value) <= tolerance ? NumericCheckOutcome.PASSED : NumericCheckOutcome.FAILED;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return NumericCheckOutcome.NOT_POSSIBLE;
        }
    }

    private static double BoundOf(Entity bound)
    {
        var value = NumericEvaluator.EvaluateAt(bound, DOUBLE_PRECISION).RealPart.EDecimal;
        if (value.IsInfinity())
            return value.IsNegative ? double.NegativeInfinity : double.PositiveInfinity;

        return value.ToDouble();
    }

    private static void Limit(JsonObject json, List<string> issues, Entity expression, Entity.Variable variable, SymbolicRequest request)
    {
        var approach = MathExpressionReader.Read(request.Approach!, APPROACH_ARGUMENT);
        MathGuards.CheckExactSize(approach);
        var limit = expression.Limit(variable, approach, request.Direction);
        var complete = !Contains<Entity.Limitf>(limit);

        json["variable"] = variable.Name;
        json["approach"] = MathFormatting.ToText(approach, "The point approached");
        json["direction"] = DIRECTIONS.First(pair => pair.Value == request.Direction).Key;
        Result(json, issues, expression, limit);
        json["complete"] = complete;
        json["verification"] = Verification(NumericCheckOutcome.NOT_POSSIBLE);
        if (!complete)
            issues.Add("The tool could not determine this limit.");
        else if (IsNaN(limit))
            issues.Add("The result is NaN, which the tool gives where the limit does not exist, e.g., where the one-sided limits differ.");
    }

    private static void Solve(JsonObject json, List<string> issues, Entity expression, Entity.Variable variable)
    {
        var statement = expression;
        if (expression is not Entity.Statement)
        {
            statement = new Entity.Equalsf(expression, 0);
            issues.Add("The expression has no '=', so it was solved for expression = 0.");
        }

        var solutions = statement.Solve(variable);
        json["variable"] = variable.Name;
        json["result"] = MathFormatting.ToText(solutions, "The set of solutions");
        json["result_latex"] = MathFormatting.ToLatex(solutions, "The LaTeX of the solutions");
        ExplainParameters(issues, expression, solutions);

        if (solutions is Entity.Set.ConditionalSet)
        {
            json["complete"] = false;
            json["verification"] = Verification(NumericCheckOutcome.NOT_POSSIBLE);
            issues.Add("The tool found no solution in closed form; the result restates the equation as a set.");
            return;
        }

        json["complete"] = true;
        if (solutions is not Entity.Set.FiniteSet finite)
        {
            json["verification"] = Verification(NumericCheckOutcome.NOT_POSSIBLE);
            return;
        }

        var outcome = finite.Count == 0 || statement is not Entity.Equalsf ? NumericCheckOutcome.NOT_POSSIBLE : NumericCheckOutcome.PASSED;
        var list = new JsonArray();
        foreach (var solution in finite)
        {
            list.Add(Solution(solution));
            if (statement is Entity.Equalsf equation)
                outcome = Combined(outcome, NumericCheck.AreEqual(equation.Left.Substitute(variable, solution), equation.Right.Substitute(variable, solution)));
        }

        json["solutions"] = list;
        json["verification"] = Verification(outcome);
        if (finite.Count == 0)
            issues.Add("There is no solution.");
    }

    private static JsonObject Solution(Entity solution)
    {
        var json = new JsonObject
        {
            ["value"] = MathFormatting.ToText(solution, "A solution"),
            ["latex"] = MathFormatting.ToLatex(solution, "The LaTeX of a solution"),
        };

        if (MathVariables.HasAny(solution))
            return json;

        try
        {
            var value = NumericEvaluator.Evaluate(solution, SOLUTION_DIGITS, CancellationToken.None);
            json["decimal"] = MathFormatting.FormatNumber(value.Value, Math.Max(value.Digits, 1));
            json["real"] = value.Value.ImaginaryPart.EDecimal.IsZero;
        }
        catch (MathComputationException)
        {
            // A solution without a value, such as NaN, keeps only its exact form.
        }

        return json;
    }

    /// <summary>
    /// Writes a result, with its condition apart when AngouriMath states one.
    /// </summary>
    private static void Result(JsonObject json, List<string> issues, Entity expression, Entity result)
    {
        if (result is Entity.Providedf provided)
        {
            json["result"] = MathFormatting.ToText(provided.Expression, "The result");
            json["result_latex"] = MathFormatting.ToLatex(provided.Expression, "The LaTeX of the result");
            json["condition"] = MathFormatting.ToText(provided.Predicate, "The condition of the result");
        }
        else
        {
            json["result"] = MathFormatting.ToText(result, "The result");
            json["result_latex"] = MathFormatting.ToLatex(result, "The LaTeX of the result");
        }

        ExplainParameters(issues, expression, result);
    }

    /// <summary>
    /// Says what the names mean which AngouriMath brought into the result, such as C for the constant of integration.
    /// </summary>
    private static void ExplainParameters(List<string> issues, Entity expression, Entity result)
    {
        var original = MathVariables.FreeIn(expression).Select(variable => variable.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var variable in MathVariables.FreeIn(result).Where(variable => !original.Contains(variable.Name)))
        {
            if (NumericCheck.IsIntegerParameter(variable))
                issues.Add($"{variable.Name} stands for any integer.");
            else if (variable.Name.StartsWith("r_", StringComparison.Ordinal))
                issues.Add($"{variable.Name} stands for any real number.");
            else if (variable.Name == "C" || variable.Name.StartsWith("C_", StringComparison.Ordinal))
                issues.Add($"{variable.Name} stands for any constant.");
        }
    }

    private static bool Contains<TNode>(Entity expression) where TNode : Entity => expression.Nodes.Any(node => node is TNode);

    private static bool IsNaN(Entity expression) => expression is Entity.Number.Complex number && (number.RealPart.EDecimal.IsNaN() || number.ImaginaryPart.EDecimal.IsNaN());

    private static NumericCheckOutcome Combined(NumericCheckOutcome first, NumericCheckOutcome second) => (first, second) switch
    {
        (NumericCheckOutcome.FAILED, _) or (_, NumericCheckOutcome.FAILED) => NumericCheckOutcome.FAILED,
        (NumericCheckOutcome.NOT_POSSIBLE, _) or (_, NumericCheckOutcome.NOT_POSSIBLE) => NumericCheckOutcome.NOT_POSSIBLE,
        _ => NumericCheckOutcome.PASSED,
    };

    private static string Verification(NumericCheckOutcome outcome) => outcome switch
    {
        NumericCheckOutcome.PASSED => "passed",
        NumericCheckOutcome.FAILED => "failed",
        _ => "not_possible",
    };
}