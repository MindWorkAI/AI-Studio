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

    private static readonly string DESCRIPTION = """Empty string literals ("") should be replaced with string.Empty for better code consistency and readability except in contexts requiring compile-time constants.""";

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

        if (RequiresCompileTimeConstant(stringLiteral))
            return;

        var diagnostic = Diagnostic.Create(RULE, stringLiteral.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private static bool RequiresCompileTimeConstant(LiteralExpressionSyntax stringLiteral)
    {
        return IsInConstDeclarationInitializer(stringLiteral)
            || IsInParameterDefaultValue(stringLiteral)
            || IsInAttributeArgument(stringLiteral)
            || IsInSwitchCaseLabel(stringLiteral)
            || IsInConstantPattern(stringLiteral);
    }

    private static bool IsInConstDeclarationInitializer(LiteralExpressionSyntax stringLiteral)
    {
        var variableDeclarator = stringLiteral.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (variableDeclarator?.Initializer is null || !ContainsNode(variableDeclarator.Initializer.Value, stringLiteral))
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
        var parameter = stringLiteral.FirstAncestorOrSelf<ParameterSyntax>();
        return parameter?.Default is not null && ContainsNode(parameter.Default.Value, stringLiteral);
    }

    private static bool IsInAttributeArgument(LiteralExpressionSyntax stringLiteral)
    {
        var attributeArgument = stringLiteral.FirstAncestorOrSelf<AttributeArgumentSyntax>();
        return attributeArgument is not null && ContainsNode(attributeArgument.Expression, stringLiteral);
    }

    private static bool IsInSwitchCaseLabel(LiteralExpressionSyntax stringLiteral)
    {
        var caseSwitchLabel = stringLiteral.FirstAncestorOrSelf<CaseSwitchLabelSyntax>();
        return caseSwitchLabel is not null && ContainsNode(caseSwitchLabel.Value, stringLiteral);
    }

    private static bool IsInConstantPattern(LiteralExpressionSyntax stringLiteral)
    {
        var constantPattern = stringLiteral.FirstAncestorOrSelf<ConstantPatternSyntax>();
        return constantPattern is not null && ContainsNode(constantPattern.Expression, stringLiteral);
    }

    private static bool ContainsNode(SyntaxNode parent, SyntaxNode child)
    {
        return parent.SpanStart <= child.SpanStart && child.Span.End <= parent.Span.End;
    }
}