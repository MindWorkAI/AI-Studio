using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SourceCodeRules.UsageAnalyzers;

/// <summary>
/// Refuses icons taken from anywhere but the catalog AppIcons.
/// </summary>
/// <remarks>
/// AppIcons names every icon after its meaning in AI Studio and decides there which Lucide icon stands for it.
/// A component which picks an icon of LucideIcons or of MudBlazor on its own bypasses that decision: the same
/// meaning ends up with different icons, and an icon of another set looks out of place next to all the others.
/// </remarks>
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class IconCatalogAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.ICON_CATALOG_ANALYZER;

    private const string TITLE = "Icons come only from AppIcons";

    private const string MESSAGE_FORMAT = "Use the constant of AppIcons for what the icon means instead of '{0}'; when there is none yet, add one to AppIcons";

    private const string DESCRIPTION = "AppIcons in Tools/AppIcons.cs is the one place which picks the icons of the app, so every meaning keeps one icon and every icon stays a glyph of the Lucide font.";

    private const string CATEGORY = "Usage";

    /// <summary>
    /// The icon sets which only the catalog may use: MudBlazor's icons with all their nested classes, and the Lucide icons.
    /// </summary>
    private static readonly ImmutableHashSet<string> ICON_SETS = ImmutableHashSet.Create("MudBlazor.Icons", "AIStudio.Tools.LucideIcons");

    /// <summary>
    /// The types which may use the icon sets: the catalog itself and the two classes LucideIconGenerator writes.
    /// </summary>
    private static readonly ImmutableHashSet<string> ALLOWED_TYPES = ImmutableHashSet.Create("AIStudio.Tools.AppIcons", "AIStudio.Tools.LucideIcons", "AIStudio.Tools.LucideIconRegistry");

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        //
        // Razor markup ends up in generated code, and most icons are set there, e.g. Icon="@Icons.Material.Filled.Add".
        // We analyze generated code as well, and the Razor compiler maps the diagnostic back to the line of the .razor
        // file. The classes of LucideIconGenerator are generated, too; ALLOWED_TYPES lets them through:
        //
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
    }

    private static void AnalyzeFieldReference(OperationAnalysisContext context)
    {
        var reference = (IFieldReferenceOperation)context.Operation;
        if (!ICON_SETS.Contains(OutermostType(reference.Field.ContainingType).ToDisplayString()))
            return;

        if (IsAllowedType(context.ContainingSymbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, reference.Syntax.GetLocation(), reference.Field.ToDisplayString()));
    }

    /// <summary>
    /// The type which encloses the given one and is itself not nested, e.g. Icons for Icons.Material.Filled.
    /// </summary>
    /// <param name="type">The type to start from.</param>
    private static INamedTypeSymbol OutermostType(INamedTypeSymbol type)
    {
        while (type.ContainingType is { } containingType)
            type = containingType;

        return type;
    }

    /// <summary>
    /// Whether the analyzed code belongs to a type which may use the icon sets, or to a type nested in one.
    /// </summary>
    /// <param name="containingSymbol">The symbol containing the analyzed code, e.g. a method or a constant.</param>
    private static bool IsAllowedType(ISymbol? containingSymbol)
    {
        var containingType = containingSymbol as INamedTypeSymbol ?? containingSymbol?.ContainingType;
        while (containingType is not null)
        {
            if (ALLOWED_TYPES.Contains(containingType.ToDisplayString()))
                return true;

            containingType = containingType.ContainingType;
        }

        return false;
    }
}