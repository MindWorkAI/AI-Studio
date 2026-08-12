using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class ProviderAccessAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.PROVIDER_ACCESS_ANALYZER;
    
    private static readonly string TITLE = "Direct access to `Providers` is not allowed";
    
    private static readonly string MESSAGE_FORMAT = "Direct access to `SettingsManager.ConfigurationData.Providers` is not allowed. Instead, use APIs like `SettingsManager.GetAllProviders`, `GetProviderById`, `GetConfidentProviders`, `GetPreselectedProvider`, or `GetChatProviderForLoadedChat`.";
    
    private static readonly string DESCRIPTION = MESSAGE_FORMAT;
    
    private const string CATEGORY = "Usage";

    /// <summary>
    /// The one type which owns the provider list and is therefore allowed to access it directly.
    /// </summary>
    private const string OWNING_TYPE = "AIStudio.Settings.SettingsManager";
    
    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(this.AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }
    
    private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Check if the member access is not on the `Providers` property:
        if (memberAccess.Name.Identifier.Text != "Providers")
            return;

        //
        // The settings manager owns the provider list: it implements the very APIs which all other
        // code is meant to use, so it must access `Providers` directly. Exempting it here keeps
        // those implementations free of suppression attributes, which would otherwise read as if
        // suppressing this rule was a normal thing to do:
        //
        if (IsOwningType(context.ContainingSymbol))
            return;

        // Get the full path of the member access:
        var fullPath = GetFullMemberAccessPath(memberAccess);
        
        // Check for the forbidden pattern:
        if (fullPath.EndsWith("ConfigurationData.Providers"))
        {
            var diagnostic = Diagnostic.Create(RULE, memberAccess.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
    
    /// <summary>
    /// Checks whether the analyzed node sits inside the type which owns the provider list.
    /// </summary>
    /// <remarks>
    /// The containing symbol is the member the node belongs to, e.g. a method or a property. We walk
    /// the chain of containing types so that nested types of the owning type are covered as well.
    /// </remarks>
    /// <param name="containingSymbol">The symbol containing the analyzed node, which may be null.</param>
    /// <returns>True, when the node belongs to the owning type.</returns>
    private static bool IsOwningType(ISymbol? containingSymbol)
    {
        var containingType = containingSymbol as INamedTypeSymbol ?? containingSymbol?.ContainingType;
        while (containingType != null)
        {
            if (containingType.ToDisplayString() == OWNING_TYPE)
                return true;

            containingType = containingType.ContainingType;
        }

        return false;
    }

    private static string GetFullMemberAccessPath(ExpressionSyntax expression)
    {
        var parts = new List<string>();
        while (expression is MemberAccessExpressionSyntax memberAccess)
        {
            parts.Add(memberAccess.Name.Identifier.Text);
            expression = memberAccess.Expression;
        }
        
        if (expression is IdentifierNameSyntax identifier)
            parts.Add(identifier.Identifier.Text);
        
        parts.Reverse();
        return string.Join(".", parts);
    }
}