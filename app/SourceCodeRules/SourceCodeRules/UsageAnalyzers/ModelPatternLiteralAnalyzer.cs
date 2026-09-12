using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

/// <summary>
/// Reports a model pattern which is not written the way a model name arrives.
/// </summary>
/// <remarks>
/// Model names are brought into one form before any rule looks at them: lowercase, a single hyphen
/// between the parts, dots kept. A pattern carrying a capital letter, an underscore, a space, or a
/// double hyphen therefore matches nothing, ever. Nothing about that looks wrong at runtime -- the
/// family simply never answers, its models fall into the global default, and they look merely
/// unremarkable rather than broken. So it is caught while compiling.
///
/// The normalization below is deliberately a second copy of the one in ModelId, because an analyzer
/// cannot reference the app. The two have to be changed together; a test in the app compares them
/// against the same table of cases.
/// </remarks>
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class ModelPatternLiteralAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.MODEL_PATTERN_LITERAL_ANALYZER;
    private const string CATEGORY = "Usage";
    private const string FAMILY_BUILDER_TYPE = "AIStudio.Models.ModelFamilyBuilder";
    private const string RULE_BUILDER_TYPE = "AIStudio.Models.ModelRuleBuilder";

    private const string TITLE = "A model pattern has to be written the way a model name arrives";

    private const string MESSAGE_FORMAT = "The model pattern \"{0}\" can never match a model: {1}";

    private const string DESCRIPTION = "Model names are normalized to lowercase with single hyphens between their parts before any rule is asked. A pattern which is not in that form matches nothing and makes its family silently ineffective.";

    private static readonly string[] PATTERN_METHOD_NAMES = ["Rule", "Modifier", "AlsoContains", "NotContains", "InheritsFrom"];

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax) context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return;

        if (!StatesAPattern(method))
            return;

        foreach (var argument in invocation.ArgumentList.Arguments)
            CheckArgument(context, argument.Expression);
    }

    private static bool StatesAPattern(IMethodSymbol method)
    {
        var declaringType = method.ContainingType?.ToDisplayString();
        if (declaringType != FAMILY_BUILDER_TYPE && declaringType != RULE_BUILDER_TYPE)
            return false;

        foreach (var name in PATTERN_METHOD_NAMES)
            if (method.Name == name)
                return true;

        return false;
    }

    private static void CheckArgument(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        //
        // Asking for the constant value rather than for a literal, so that a pattern written once as
        // a constant and used in several rules is checked as well.
        //
        var constant = context.SemanticModel.GetConstantValue(expression);
        if (!constant.HasValue || constant.Value is not string text)
            return;

        var normalized = Normalize(text);
        if (normalized == text)
            return;

        var advice = normalized.Length == 0
            ? "nothing of it survives the way names are normalized"
            : $"write it as \"{normalized}\"";

        context.ReportDiagnostic(Diagnostic.Create(RULE, expression.GetLocation(), text, advice));
    }

    /// <summary>
    /// Brings a text into the form a model name arrives in.
    /// </summary>
    /// <remarks>
    /// The same rule as ModelId.Normalize in the app, written again here because an analyzer cannot
    /// reference the code it analyzes. Keep the two in step.
    /// </remarks>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The text in lowercase, with every separator written as a single hyphen.</returns>
    private static string Normalize(string text)
    {
        var normalized = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (IsKept(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
                continue;
            }

            // Anything else separates two parts of the name. A leading separator, and a repeated
            // one, say nothing:
            if (normalized.Length == 0 || normalized[normalized.Length - 1] == '-')
                continue;

            normalized.Append('-');
        }

        // A trailing separator carries no meaning either:
        if (normalized.Length > 0 && normalized[normalized.Length - 1] == '-')
            normalized.Length--;

        return normalized.ToString();
    }

    /// <summary>
    /// Whether a character survives normalization as itself.
    /// </summary>
    /// <remarks>
    /// Letters and digits, and the dot: it carries the version boundary, so llama3 and llama3.1 stay
    /// two different names.
    /// </remarks>
    /// <param name="character">The character to look at.</param>
    /// <returns>True, when it is kept.</returns>
    private static bool IsKept(char character) =>
        character is >= 'a' and <= 'z' ||
        character is >= 'A' and <= 'Z' ||
        character is >= '0' and <= '9' ||
        character is '.';
}