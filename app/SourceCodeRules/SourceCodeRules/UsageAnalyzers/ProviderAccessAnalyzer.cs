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
    private const string DIAGNOSTIC_ID = $"{Tools.ID_PREFIX}0001";
    
    private static readonly string TITLE = "Direct access to `Providers` is not allowed";
    
    private static readonly string MESSAGE_FORMAT = "Direct access to `SettingsManager.ConfigurationData.Providers` is not allowed. Instead, use APIs like `SettingsManager.GetPreselectedProvider`, etc.";
    
    private static readonly string DESCRIPTION = MESSAGE_FORMAT;
    
    private const string CATEGORY = "Usage";
    
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
        
        // Prüfen, ob wir eine Kette von Zugriffen haben, die auf "Providers" endet
        if (memberAccess.Name.Identifier.Text != "Providers")
            return;
        
        // Den kompletten Zugriffspfad aufbauen
        var fullPath = this.GetFullMemberAccessPath(memberAccess);
        
        // Prüfen, ob der Pfad unserem verbotenen Muster entspricht
        if (fullPath.EndsWith("ConfigurationData.Providers"))
        {
            var diagnostic = Diagnostic.Create(RULE, memberAccess.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
    
    private string GetFullMemberAccessPath(ExpressionSyntax expression)
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