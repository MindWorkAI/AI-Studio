using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class ThisUsageAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.THIS_USAGE_ANALYZER;
    
    private static readonly string TITLE = "`this.` must be used";
    
    private static readonly string MESSAGE_FORMAT = "`this.` must be used to access variables, methods, and properties";
    
    private static readonly string DESCRIPTION = MESSAGE_FORMAT;
    
    private const string CATEGORY = "Usage";
    
    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(this.AnalyzeIdentifier, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(this.AnalyzeGenericName, SyntaxKind.GenericName);
    }
    
    private void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
    {
        var genericNameSyntax = (GenericNameSyntax)context.Node;
        
        // Skip if already part of a 'this' expression:
        if (IsAccessedThroughThis(genericNameSyntax))
            return;
            
        if (IsWithinInitializer(genericNameSyntax))
            return;
        
        if (IsPartOfMemberAccess(genericNameSyntax))
            return;
        
        // Skip if it's the 'T' translation method
        if (IsTranslationMethod(genericNameSyntax))
            return;
        
        // Get symbol info for the generic name:
        var symbolInfo = context.SemanticModel.GetSymbolInfo(genericNameSyntax);
        var symbol = symbolInfo.Symbol;
        
        if (symbol == null)
            return;
            
        // Skip static methods
        if (symbol.IsStatic)
            return;
            
        // Skip local functions
        if (symbol is IMethodSymbol methodSymbol && IsLocalFunction(methodSymbol))
            return;
        
        // Get the containing type of the current context
        var containingSymbol = context.ContainingSymbol;
        var currentType = containingSymbol?.ContainingType;
        
        // If we're in a static context, allow accessing members without this
        if (IsInStaticContext(containingSymbol))
            return;
        
        if (symbol is IMethodSymbol)
        {
            var containingType = symbol.ContainingType;
            
            // If the symbol is a member of the current type or a base type, then require this
            if (currentType != null && (SymbolEqualityComparer.Default.Equals(containingType, currentType) || 
                                        IsBaseTypeOf(containingType, currentType)))
            {
                var diagnostic = Diagnostic.Create(RULE, genericNameSyntax.Identifier.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private static bool IsTranslationMethod(SyntaxNode node)
    {
        // Check if this is a method called 'T' (translation method)
        if (node is IdentifierNameSyntax { Identifier.Text: "T" })
            return true;
        
        return false;
    }
    
    private void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifierNameSyntax = (IdentifierNameSyntax)context.Node;
        
        // Skip if this identifier is part of a generic name - we'll handle that separately:
        if (identifierNameSyntax.Parent is GenericNameSyntax)
            return;
        
        // Skip if already part of a 'this' expression:
        if (IsAccessedThroughThis(identifierNameSyntax))
            return;
            
        if (IsWithinInitializer(identifierNameSyntax))
            return;
        
        if (IsPartOfMemberAccess(identifierNameSyntax))
            return;
        
        // Also skip if it's part of static import statements:
        if (IsPartOfUsingStaticDirective(identifierNameSyntax))
            return;
            
        // Skip if it's part of a namespace or type name:
        if (IsPartOfNamespaceOrTypeName(identifierNameSyntax))
            return;
        
        // Skip if it's the 'T' translation method:
        if (IsTranslationMethod(identifierNameSyntax))
            return;
        
        // Get symbol info:
        var symbolInfo = context.SemanticModel.GetSymbolInfo(identifierNameSyntax);
        var symbol = symbolInfo.Symbol;
        
        if (symbol == null)
            return;
            
        // Skip local variables, parameters, and range variables:
        if (symbol.Kind is SymbolKind.Local or SymbolKind.Parameter or SymbolKind.RangeVariable or SymbolKind.TypeParameter)
            return;
            
        // Skip types and namespaces:
        if (symbol.Kind is SymbolKind.NamedType or SymbolKind.Namespace)
            return;
        
        // Explicitly check if this is a local function:
        if (symbol is IMethodSymbol methodSymbol && IsLocalFunction(methodSymbol))
            return;
            
        // Get the containing type of the current context:
        var containingSymbol = context.ContainingSymbol;
        var currentType = containingSymbol?.ContainingType;
        
        // If we're in a static context, allow accessing members without this:
        if (IsInStaticContext(containingSymbol))
            return;
            
        // Now check if the symbol is an instance member of the current class:
        if (symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol)
        {
            // Skip static members:
            if (symbol.IsStatic)
                return;
                
            // Skip constants:
            if (symbol is IFieldSymbol { IsConst: true })
                return;
            
            var containingType = symbol.ContainingType;
            
            // If the symbol is a member of the current type or a base type, then require this:
            if (currentType != null && (SymbolEqualityComparer.Default.Equals(containingType, currentType) || 
                                        IsBaseTypeOf(containingType, currentType)))
            {
                var diagnostic = Diagnostic.Create(RULE, identifierNameSyntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
    
    private static bool IsLocalFunction(IMethodSymbol methodSymbol) => methodSymbol.MethodKind is MethodKind.LocalFunction;

    private static bool IsBaseTypeOf(INamedTypeSymbol baseType, INamedTypeSymbol derivedType)
    {
        var currentType = derivedType.BaseType;
        while (currentType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentType, baseType))
                return true;
                
            currentType = currentType.BaseType;
        }
        
        return false;
    }
    
    private static bool IsInStaticContext(ISymbol? containingSymbol) => containingSymbol?.IsStatic is true;

    private static bool IsAccessedThroughThis(SyntaxNode node)
    {
        if (node.Parent is MemberAccessExpressionSyntax memberAccess)
            if (memberAccess.Expression is ThisExpressionSyntax && memberAccess.Name == node)
                return true;
        
        return false;
    }
    
    private static bool IsWithinInitializer(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
            if (current is InitializerExpressionSyntax)
                return true;
        
        return false;
    }
    
    private static bool IsPartOfMemberAccess(SyntaxNode node)
    {
        // Check if the node is part of a member access expression where the expression is not 'this':
        if (node.Parent is MemberAccessExpressionSyntax memberAccess)
        {
            // If the member access expression is 'this', it's allowed:
            if (memberAccess.Expression is ThisExpressionSyntax)
                return false;
            
            // If the member access expression is something else (e.g., instance.Member), skip:
            if (memberAccess.Name == node)
                return true;
        }
        
        // Also check for conditional access expressions (e.g., instance?.Member):
        if (node.Parent is ConditionalAccessExpressionSyntax)
            return true;
        
        return false;
    }
    
    private static bool IsPartOfUsingStaticDirective(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
            if (current is UsingDirectiveSyntax)
                return true;
        
        return false;
    }
    
    private static bool IsPartOfNamespaceOrTypeName(SyntaxNode node)
    {
        // Check if a node is part of a namespace, class, or type declaration:
        if (node.Parent is NameSyntax && node.Parent is not MemberAccessExpressionSyntax)
            return true;
            
        return false;
    }
}