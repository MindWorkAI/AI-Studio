using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class EmptyStringAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.EMPTY_STRING_ANALYZER;
    
    private static readonly string TITLE = """
                                           Use string.Empty instead of ""
                                           """;
    
    private static readonly string MESSAGE_FORMAT = """
                                                    Use string.Empty instead of ""
                                                    """;
    
    private static readonly string DESCRIPTION = """Empty string literals ("") should be replaced with string.Empty for better code consistency and readability except in const contexts.""";
    
    private const string CATEGORY = "Usage";
    
    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeEmptyStringLiteral, SyntaxKind.StringLiteralExpression);
    }
    
    private static void AnalyzeEmptyStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var stringLiteral = (LiteralExpressionSyntax)context.Node;
        if (stringLiteral.Token.ValueText != string.Empty)
            return;
        
        if (IsInConstContext(stringLiteral))
            return;
        
        if (IsInParameterDefaultValue(stringLiteral))
            return;
        
        var diagnostic = Diagnostic.Create(RULE, stringLiteral.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
    
    private static bool IsInConstContext(LiteralExpressionSyntax stringLiteral)
    {
        var variableDeclarator = stringLiteral.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (variableDeclarator is null)
            return false;
            
        var declaration = variableDeclarator.Parent?.Parent;
        return declaration switch
        {
            FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword),
            LocalDeclarationStatementSyntax localDeclaration => localDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword),
            
            _ => false
        };
    }
    
    private static bool IsInParameterDefaultValue(LiteralExpressionSyntax stringLiteral)
    {
        // Prüfen, ob das String-Literal Teil eines Parameter-Defaults ist
        var parameter = stringLiteral.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter is null)
            return false;
        
        // Überprüfen, ob das String-Literal im Default-Wert des Parameters verwendet wird
        if (parameter.Default is not null && 
            parameter.Default.Value == stringLiteral)
        {
            return true;
        }
    
        return false;
    }
}