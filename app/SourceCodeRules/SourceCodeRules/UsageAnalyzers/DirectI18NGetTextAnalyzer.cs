using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class DirectI18NGetTextAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.DIRECT_I18N_GET_TEXT_ANALYZER;

    private const string TITLE = "Direct translation lookup is not allowed";

    private const string MESSAGE_FORMAT = "Call GetText only from a T or TB wrapper whose first string parameter is forwarded as the fallback text";

    private const string DESCRIPTION = "Translation calls must use collector-compatible T or TB wrappers so that every fallback text is included in the generated I18N resources.";

    private const string CATEGORY = "Usage";

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
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return;

        var targetMethod = method.ReducedFrom ?? method;
        if (targetMethod.Name != "GetText" || targetMethod.ContainingType.Name != "ILangExtensions" || targetMethod.ContainingNamespace.ToDisplayString() != "AIStudio.Tools.PluginSystem")
            return;

        if (context.SemanticModel.GetOperation(invocation) is IInvocationOperation operation
            && IsCollectorCompatibleWrapper(context.ContainingSymbol as IMethodSymbol, operation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, invocation.GetLocation()));
    }

    private static bool IsCollectorCompatibleWrapper(IMethodSymbol? containingMethod, IInvocationOperation invocation)
    {
        if (containingMethod?.Name is not ("T" or "TB")
            || containingMethod.ReturnType.SpecialType != SpecialType.System_String
            || containingMethod.Parameters.Length == 0
            || containingMethod.Parameters[0].Type.SpecialType != SpecialType.System_String)
            return false;

        var fallbackArgument = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "fallbackEN");
        return fallbackArgument?.Value is IParameterReferenceOperation parameterReference
               && SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, containingMethod.Parameters[0]);
    }
}