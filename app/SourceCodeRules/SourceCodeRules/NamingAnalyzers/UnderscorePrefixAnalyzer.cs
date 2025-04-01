using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.NamingAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class UnderscorePrefixAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.UNDERSCORE_PREFIX_ANALYZER;
    
    private static readonly string TITLE = "Variable names cannot start with underscore";
    
    private static readonly string MESSAGE_FORMAT = "The variable name '{0}' starts with an underscore which is not allowed";
    
    private static readonly string DESCRIPTION = "Variable names cannot start with an underscore prefix.";
    
    private const string CATEGORY = "Naming";
    
    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration, SyntaxKind.VariableDeclarator);
    }
    
    private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
    {
        var variableDeclarator = (VariableDeclaratorSyntax)context.Node;
        var variableName = variableDeclarator.Identifier.Text;
        if (variableName.StartsWith("_"))
        {
            var diagnostic = Diagnostic.Create(RULE, variableDeclarator.Identifier.GetLocation(), variableName);
            context.ReportDiagnostic(diagnostic);
        }
    }
}