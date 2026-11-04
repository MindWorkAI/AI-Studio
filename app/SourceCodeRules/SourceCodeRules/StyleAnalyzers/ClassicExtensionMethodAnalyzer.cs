using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.StyleAnalyzers;

/// <summary>
/// Refuses extension methods which are declared with a this parameter instead of in an extension block.
/// </summary>
/// <remarks>
/// AI Studio declares its extension members in extension blocks, one per receiver, so that all members of a
/// receiver read alike and extension properties can join them. A method with a params parameter stays a
/// classic one for now: for a member of an extension block, the compiler of the .NET 10.0.1xx SDK warns about
/// every argument passed one by one to a params parameter (CS8620, dotnet/roslyn#80024). Drop that exception
/// once the SDK in metadata.txt carries the fix.
/// </remarks>
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class ClassicExtensionMethodAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.CLASSIC_EXTENSION_METHOD_ANALYZER;

    private const string TITLE = "Extension methods belong in extension blocks";

    private const string MESSAGE_FORMAT = "Declare '{0}' as a member of an extension block for its receiver instead of with a this parameter";

    private const string DESCRIPTION = "AI Studio declares its extension members in extension blocks, one per receiver. Only a method with a params parameter stays a classic extension method, until the SDK fixes dotnet/roslyn#80024.";

    private const string CATEGORY = "Style";

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        var parameters = method.ParameterList.Parameters;
        if (parameters.Count == 0)
            return;

        var thisKeyword = parameters[0].Modifiers.FirstOrDefault(modifier => modifier.IsKind(SyntaxKind.ThisKeyword));
        if (thisKeyword.IsKind(SyntaxKind.None))
            return;

        if (parameters.Any(parameter => parameter.Modifiers.Any(SyntaxKind.ParamsKeyword)))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, thisKeyword.GetLocation(), method.Identifier.Text));
    }
}
