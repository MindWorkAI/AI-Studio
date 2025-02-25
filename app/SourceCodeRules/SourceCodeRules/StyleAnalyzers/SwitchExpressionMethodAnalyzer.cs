using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.StyleAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public class SwitchExpressionMethodAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.SWITCH_EXPRESSION_METHOD_ANALYZER;
    
    private static readonly string TITLE = "Method with switch expression should use inline expression body";
    
    private static readonly string MESSAGE_FORMAT = "Method with a switch expression should use inline expression body syntax with the switch keyword on the same line";
    
    private static readonly string DESCRIPTION = "Methods that only return a switch expression should use the expression body syntax (=>) with the switch keyword on the same line for better readability.";
    
    private const string CATEGORY = "Style";
    
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
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }
    
    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        
        // Fall 1: Methode hat Block-Body mit einem Return-Statement, das eine Switch-Expression ist
        if (methodDeclaration is { Body: not null, ExpressionBody: null })
        {
            var statements = methodDeclaration.Body.Statements;
            if (statements.Count == 1 && statements[0] is ReturnStatementSyntax { Expression: SwitchExpressionSyntax })
            {
                var diagnostic = Diagnostic.Create(RULE, methodDeclaration.Identifier.GetLocation());
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }
        
        // Fall 2: Methode hat Expression-Body, aber die Switch-Expression beginnt auf einer neuen Zeile
        var expressionBody = methodDeclaration.ExpressionBody;
        if (expressionBody?.Expression is SwitchExpressionSyntax switchExpr)
        {
            var arrowToken = expressionBody.ArrowToken;
            var switchToken = switchExpr.SwitchKeyword;
            bool hasNewLineBetweenArrowAndSwitch = false;
                
            foreach (var trivia in arrowToken.TrailingTrivia)
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    hasNewLineBetweenArrowAndSwitch = true;
                    break;
                }
            }
            
            // Prüfe Leading Trivia des Switch-Keywords, falls notwendig
            if (!hasNewLineBetweenArrowAndSwitch)
            {
                foreach (var trivia in switchToken.LeadingTrivia)
                {
                    if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        hasNewLineBetweenArrowAndSwitch = true;
                        break;
                    }
                }
            }
            
            if (hasNewLineBetweenArrowAndSwitch)
            {
                var diagnostic = Diagnostic.Create(RULE, methodDeclaration.Identifier.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}