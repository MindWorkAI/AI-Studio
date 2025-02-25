using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public class RandomInstantiationAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.RANDOM_INSTANTIATION_ANALYZER;
    
    private static readonly string TITLE = "Direct instantiation of Random is not allowed";
    
    private static readonly string MESSAGE_FORMAT = "Do not use 'new Random()'. Instead, inject and use the ThreadSafeRandom service from the DI container.";
    
    private static readonly string DESCRIPTION = "Using 'new Random()' can lead to issues in multi-threaded scenarios. Use the ThreadSafeRandom service instead.";
    
    private const string CATEGORY = "Usage";
    
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
        context.RegisterSyntaxNodeAction(this.AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }
    
    private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(objectCreation.Type).Symbol is not ITypeSymbol typeSymbol)
            return;
        
        if (typeSymbol.ToString() == "System.Random" || typeSymbol is { Name: "Random", ContainingNamespace.Name: "System" })
        {
            var diagnostic = Diagnostic.Create(RULE, objectCreation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}