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
    
    private static readonly string TITLE = "Method or property with switch expression should use inline expression body";
    
    private static readonly string MESSAGE_FORMAT = "{0} with a switch expression should use inline expression body syntax with the switch keyword on the same line";
    
    private static readonly string DESCRIPTION = "Methods and properties that only return a switch expression should use the expression body syntax (=>) with the switch keyword on the same line for better readability.";
    
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
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
    }
    
    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        if (NeedsInlineExpressionBody(methodDeclaration.Body, methodDeclaration.ExpressionBody))
            context.ReportDiagnostic(Diagnostic.Create(RULE, methodDeclaration.Identifier.GetLocation(), "Method"));
    }
    
    private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        if (propertyDeclaration.ExpressionBody is not null)
        {
            if (NeedsInlineExpressionBody(null, propertyDeclaration.ExpressionBody))
                context.ReportDiagnostic(Diagnostic.Create(RULE, propertyDeclaration.Identifier.GetLocation(), "Property"));
            
            return;
        }
        
        //
        // A property with nothing but a getter which returns a switch expression says the same as one with
        // an expression body. Its getter is no place for the switch, wherever the keyword stands:
        //
        if (propertyDeclaration.AccessorList is not { Accessors.Count: 1 } accessorList)
            return;
        
        var getter = accessorList.Accessors[0];
        if (!getter.IsKind(SyntaxKind.GetAccessorDeclaration) || getter.AttributeLists.Count > 0 || getter.Modifiers.Count > 0)
            return;
        
        if (getter.ExpressionBody?.Expression is SwitchExpressionSyntax || ReturnsOnlySwitchExpression(getter.Body))
            context.ReportDiagnostic(Diagnostic.Create(RULE, propertyDeclaration.Identifier.GetLocation(), "Property"));
    }
    
    private static bool NeedsInlineExpressionBody(BlockSyntax? body, ArrowExpressionClauseSyntax? expressionBody)
    {
        // Case 1: a block body with nothing but a return of a switch expression:
        if (body is not null && expressionBody is null)
            return ReturnsOnlySwitchExpression(body);
        
        // Case 2: an expression body whose switch expression starts on a new line:
        if (expressionBody?.Expression is not SwitchExpressionSyntax switchExpression)
            return false;
        
        return expressionBody.ArrowToken.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia) || switchExpression.SwitchKeyword.LeadingTrivia.Any(SyntaxKind.EndOfLineTrivia);
    }
    
    private static bool ReturnsOnlySwitchExpression(BlockSyntax? body) => body is { Statements.Count: 1 } && body.Statements[0] is ReturnStatementSyntax { Expression: SwitchExpressionSyntax };
}