using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class CanonicalJsonShapeAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.CANONICAL_JSON_SHAPE_ANALYZER;

    private const string ATTRIBUTE_NAME = "CanonicalJsonShapeAttribute";

    private const string PROPERTY_NAME_ATTRIBUTE = "JsonPropertyNameAttribute";

    private const string IGNORE_ATTRIBUTE = "JsonIgnoreAttribute";

    private const string CONDITION_ARGUMENT = "Condition";

    private const string DEFAULT_CONDITION = "Always";

    private const string TITLE = "Canonical JSON shape must match its declared signature";

    private const string MESSAGE_FORMAT = "The JSON shape of '{0}' no longer matches its declared signature. Data that was hashed with the previous shape stops being readable, so update the attribute to \"{1}\" only once that is acceptable.";

    private const string DESCRIPTION = "The serialized form of this type is hashed into stored data. Adding, removing, renaming, or retyping a property changes those bytes and makes previously stored data fail its integrity check, which surfaces as unreadable data rather than as an error. The declared signature exists so that such a change cannot pass unnoticed.";

    private const string CATEGORY = "Usage";

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    /// <summary>
    /// Renders property types the way they are written in the source, including nullable annotations.
    /// </summary>
    private static readonly SymbolDisplayFormat TYPE_FORMAT = SymbolDisplayFormat.MinimallyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var declaration = type.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name == ATTRIBUTE_NAME);
        if (declaration is null)
            return;

        var declared = declaration.ConstructorArguments.Length > 0 ? declaration.ConstructorArguments[0].Value as string : null;
        var actual = ComputeSignature(type);
        if (declared == actual)
            return;

        var location = declaration.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? type.Locations.FirstOrDefault();
        if (location is not null)
            context.ReportDiagnostic(Diagnostic.Create(RULE, location, type.Name, actual));
    }

    /// <summary>
    /// Derives the shape signature from everything that changes the serialized bytes.
    /// </summary>
    /// <remarks>
    /// Entries are ordered by their JSON name rather than by declaration order, because the hashed JSON
    /// is canonicalized with ordinally sorted properties. Moving a property within its type therefore
    /// does not change any stored hash, and must not fail the build either.
    /// </remarks>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The signature of the serialized shape.</returns>
    private static string ComputeSignature(INamedTypeSymbol type)
    {
        List<string> entries = [];
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.IsIndexer || property.GetMethod is null || property.DeclaredAccessibility != Accessibility.Public)
                continue;

            entries.Add($"{JsonName(property)}|{property.Type.ToDisplayString(TYPE_FORMAT)}|{IgnoreMarker(property)}");
        }

        entries.Sort(System.StringComparer.Ordinal);
        return Fnv1A(string.Join("\n", entries));
    }

    /// <summary>
    /// Gets the JSON name a property is written with.
    /// </summary>
    private static string JsonName(IPropertySymbol property)
    {
        var attribute = property.GetAttributes().FirstOrDefault(candidate => candidate.AttributeClass?.Name == PROPERTY_NAME_ATTRIBUTE);
        if (attribute is not null && attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string name)
            return name;

        return property.Name;
    }

    /// <summary>
    /// Gets the ignore behavior of a property, which decides whether it appears at all.
    /// </summary>
    private static string IgnoreMarker(IPropertySymbol property)
    {
        var attribute = property.GetAttributes().FirstOrDefault(candidate => candidate.AttributeClass?.Name == IGNORE_ATTRIBUTE);
        if (attribute is null)
            return string.Empty;

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key != CONDITION_ARGUMENT)
                continue;

            var rendered = argument.Value.ToCSharpString();
            var separator = rendered.LastIndexOf('.');
            return separator < 0 ? rendered : rendered.Substring(separator + 1);
        }

        return DEFAULT_CONDITION;
    }

    /// <summary>
    /// Computes a stable 32-bit FNV-1a hash, rendered as eight lowercase hexadecimal digits.
    /// </summary>
    /// <remarks>
    /// The built-in string hash is randomized per process and would produce a different signature on
    /// every build, so the signature is computed explicitly here.
    /// </remarks>
    /// <param name="value">The text to hash.</param>
    /// <returns>The signature text.</returns>
    private static string Fnv1A(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash.ToString("x8");
    }
}