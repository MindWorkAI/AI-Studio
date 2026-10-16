using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class CanonicalJsonConfigurationAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.CANONICAL_JSON_CONFIGURATION_ANALYZER;

    private const string ATTRIBUTE_NAME = "CanonicalJsonConfigurationAttribute";

    private const string CONVERTERS = "Converters";

    private const string TITLE = "Canonical JSON options must stay frozen and self-contained";

    private const string MESSAGE_FORMAT = "{0} The byte output of these options is hashed into stored data, so any change to them makes previously stored data fail its integrity check";

    private const string DESCRIPTION = "Canonical JSON options are frozen because their exact byte output is hashed into stored data. They must be initialized inline at their own declaration, must not declare converters, and must not be reconfigured afterwards, so that a change meant for other serializer options cannot reach them through a shared factory.";

    private const string CATEGORY = "Usage";

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var declaration = (PropertyDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not { } symbol || !IsMarked(symbol))
            return;

        AnalyzeInitializer(context, declaration.Initializer?.Value, declaration.Identifier.GetLocation());
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        foreach (var variable in declaration.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable) is not { } symbol || !IsMarked(symbol))
                continue;

            AnalyzeInitializer(context, variable.Initializer?.Value, variable.Identifier.GetLocation());
        }
    }

    /// <summary>
    /// Requires the complete configuration to be visible at the declaration itself.
    /// </summary>
    private static void AnalyzeInitializer(SyntaxNodeAnalysisContext context, ExpressionSyntax? initializer, Location location)
    {
        if (initializer is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(RULE, location, "Canonical JSON options must be initialized where they are declared."));
            return;
        }

        if (initializer is not ObjectCreationExpressionSyntax and not ImplicitObjectCreationExpressionSyntax)
        {
            context.ReportDiagnostic(Diagnostic.Create(RULE, initializer.GetLocation(), "Canonical JSON options must be created inline instead of by a helper, so that every setting is visible here and cannot be changed through a shared factory."));
            return;
        }

        var settings = initializer switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.Initializer,
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Initializer,

            _ => null,
        };

        if (settings is null)
            return;

        foreach (var expression in settings.Expressions)
        {
            var name = expression switch
            {
                AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } => identifier.Identifier.Text,

                _ => null,
            };

            if (name == CONVERTERS)
                context.ReportDiagnostic(Diagnostic.Create(RULE, expression.GetLocation(), "Canonical JSON options must not declare converters."));
        }
    }

    /// <summary>
    /// Reports reaching for the converter collection of already declared canonical options.
    /// </summary>
    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        if (memberAccess.Name.Identifier.Text != CONVERTERS)
            return;

        if (!IsMarked(context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, memberAccess.GetLocation(), "Canonical JSON options must not gain converters after they were declared."));
    }

    /// <summary>
    /// Reports assigning any setting of already declared canonical options.
    /// </summary>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (!IsMarked(context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, assignment.GetLocation(), "Canonical JSON options must not be reconfigured after they were declared."));
    }

    private static bool IsMarked(ISymbol? symbol) =>
        symbol is IPropertySymbol or IFieldSymbol &&
        symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == ATTRIBUTE_NAME);
}