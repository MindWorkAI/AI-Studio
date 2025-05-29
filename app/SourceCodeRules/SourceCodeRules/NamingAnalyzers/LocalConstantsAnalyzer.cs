using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.NamingAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class LocalConstantsAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.LOCAL_CONSTANTS_ANALYZER;
    
    private static readonly string TITLE = "Local constant variables must be in UPPER_CASE";
    
    private static readonly string MESSAGE_FORMAT = "Local constant variable '{0}' must be in UPPER_CASE";
    
    private static readonly string DESCRIPTION = "All local constant variables should be named using UPPER_CASE with words separated by underscores.";
    
    private const string CATEGORY = "Naming";
    
    private static readonly DiagnosticDescriptor RULE = new(
        DIAGNOSTIC_ID, 
        TITLE, 
        MESSAGE_FORMAT, 
        CATEGORY, 
        DiagnosticSeverity.Error, 
        isEnabledByDefault: true, 
        description: DESCRIPTION);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
    }
    
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var localDeclaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!localDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            return;
        
        foreach (var variable in localDeclaration.Declaration.Variables)
        {
            var variableName = variable.Identifier.Text;
            if (!IsUpperCase(variableName))
            {
                var diagnostic = Diagnostic.Create(RULE, variable.Identifier.GetLocation(), variableName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private static bool IsUpperCase(string name) => name.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_') && 
                                                    !string.IsNullOrWhiteSpace(name) && name.Any(char.IsLetter);
}