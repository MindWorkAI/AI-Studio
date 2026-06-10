using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class StaticServiceProviderCacheAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.STATIC_SERVICE_PROVIDER_CACHE_ANALYZER;

    private static readonly string TITLE = "Services from Program.SERVICE_PROVIDER must not be cached in static state";

    private static readonly string MESSAGE_FORMAT = "Do not cache services from Program.SERVICE_PROVIDER in static state. Use constructor injection, method-local resolution, or a non-caching get-only property.";

    private static readonly string DESCRIPTION = MESSAGE_FORMAT;

    private const string CATEGORY = "Usage";

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(this.AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(this.AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
        context.RegisterSyntaxNodeAction(this.AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(this.AnalyzeAssignmentExpression, SyntaxKind.SimpleAssignmentExpression);
    }

    private void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
        foreach (var variable in fieldDeclaration.Declaration.Variables)
            this.AnalyzeStaticFieldInitializer(context, variable);
    }

    private void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var variable = (VariableDeclaratorSyntax)context.Node;
        if (variable.Parent?.Parent is FieldDeclarationSyntax)
            return;

        this.AnalyzeStaticFieldInitializer(context, variable);
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        if (propertyDeclaration.Initializer is null)
            return;

        if (context.SemanticModel.GetDeclaredSymbol(propertyDeclaration) is not { IsStatic: true })
            return;

        if (!this.IsProgramServiceProviderGetCall(propertyDeclaration.Initializer.Value))
            return;

        var diagnostic = Diagnostic.Create(RULE, propertyDeclaration.Initializer.Value.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (!this.IsProgramServiceProviderGetCall(assignment.Right))
            return;

        var targetSymbol = context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
        if (targetSymbol is not IFieldSymbol { IsStatic: true } && targetSymbol is not IPropertySymbol { IsStatic: true })
            return;

        var diagnostic = Diagnostic.Create(RULE, assignment.Right.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private void AnalyzeStaticFieldInitializer(SyntaxNodeAnalysisContext context, VariableDeclaratorSyntax variable)
    {
        if (variable.Initializer is null)
            return;

        if (context.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol { IsStatic: true })
            return;

        if (!this.IsProgramServiceProviderGetCall(variable.Initializer.Value))
            return;

        var diagnostic = Diagnostic.Create(RULE, variable.Initializer.Value.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private bool IsProgramServiceProviderGetCall(ExpressionSyntax expression)
    {
        if (this.UnwrapSimpleExpression(expression) is not InvocationExpressionSyntax invocation)
            return false;

        if (this.UnwrapSimpleExpression(invocation.Expression) is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (!this.IsServiceProviderGetMethod(memberAccess.Name))
            return false;

        return this.IsProgramServiceProviderAccess(memberAccess.Expression);
    }

    private bool IsServiceProviderGetMethod(SimpleNameSyntax name) => name switch
    {
        GenericNameSyntax genericName when genericName.TypeArgumentList.Arguments.Count == 1 =>
            genericName.Identifier.Text is "GetService" or "GetRequiredService",
        _ => false,
    };

    private bool IsProgramServiceProviderAccess(ExpressionSyntax expression)
    {
        if (this.UnwrapSimpleExpression(expression) is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "SERVICE_PROVIDER")
            return false;

        return this.UnwrapSimpleExpression(memberAccess.Expression) is IdentifierNameSyntax { Identifier.Text: "Program" };
    }

    private ExpressionSyntax UnwrapSimpleExpression(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } postfixUnary:
                    expression = postfixUnary.Operand;
                    continue;

                case CastExpressionSyntax castExpression:
                    expression = castExpression.Expression;
                    continue;

                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression:
                    expression = asExpression.Left;
                    continue;

                default:
                    return expression;
            }
        }
    }
}