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
public sealed class ConstStaticAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.CONST_STATIC_ANALYZER;
    
    private static readonly string TITLE = "Constant and static fields must be in UPPER_CASE";
    
    private static readonly string MESSAGE_FORMAT = "Field '{0}' must be in UPPER_CASE";
    
    private static readonly string DESCRIPTION = "All constant and static fields should be named using UPPER_CASE.";
    
    private const string CATEGORY = "Naming";
    
    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(this.AnalyzeField, SyntaxKind.FieldDeclaration);
    }
    
    private void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
        
        // Prüfen ob das Feld static oder const ist
        if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)))
            return;
        
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            var fieldName = variable.Identifier.Text;
            
            // Prüfen ob der Name bereits in UPPER_CASE ist
            if (!IsUpperCase(fieldName))
            {
                var diagnostic = Diagnostic.Create(
                    RULE,
                    variable.Identifier.GetLocation(),
                    fieldName);
                
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private static bool IsUpperCase(string name)
    {
        // Erlaubt: Nur Großbuchstaben, Zahlen und Unterstriche
        return name.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');
    }
}