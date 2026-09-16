using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGeneratedMappings;

/// <summary>
/// Collects every model family and every model host of the compilation into one list.
/// </summary>
/// <remarks>
/// Adding a family has to be one action, not two. A registry which somebody has to remember to add
/// to is a registry which will be incomplete, and the failure it produces is the quietest one there
/// is: a family which simply never answers, so its models fall into the global default and look
/// merely unremarkable.
///
/// Searching for the types at startup through reflection would do the same job, but this app
/// publishes trimmed and uses reflection nowhere else. So the search happens while compiling, and
/// what ships is a plain array.
/// </remarks>
[Generator]
#pragma warning disable RS1036
public sealed class ModelRegistryGenerator : IIncrementalGenerator
#pragma warning restore RS1036
{
    private const string GENERATED_NAMESPACE = "AIStudio.Models.Registry";
    private const string GENERATED_TYPE_NAME = "ModelRegistrations";
    private const string FAMILY_BASE_TYPE = "AIStudio.Models.ModelFamily";
    private const string HOST_INTERFACE = "AIStudio.Models.Hosting.IModelHost";

    private static readonly DiagnosticDescriptor CANNOT_BE_REGISTERED = new(
        id: "MDR001",
        title: "A model family or host cannot be registered",
        messageFormat: "'{0}' is a model family or host, but the generated registry cannot create it: {1}. It will answer for no model at all.",
        category: "SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The generated registry creates every family and host with its parameterless constructor. A type it cannot create is left out, which makes it silently ineffective.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => CouldBeARegistration(node), static (syntax, _) => Inspect(syntax))
            .Where(static candidate => candidate.FullName is not null)
            .Collect();

        context.RegisterSourceOutput(candidates, Generate);
    }

    /// <summary>
    /// Whether a syntax node is worth asking the semantic model about.
    /// </summary>
    /// <remarks>
    /// Runs on every node of every keystroke, so it only looks at the syntax: a class with a base
    /// list which is neither abstract nor static. Everything else is decided once a symbol exists.
    /// </remarks>
    /// <param name="node">The node to look at.</param>
    /// <returns>True, when the node could be a family or a host.</returns>
    private static bool CouldBeARegistration(SyntaxNode node) =>
        node is ClassDeclarationSyntax declaration &&
        declaration.BaseList is { Types.Count: > 0 } &&
        !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
        !declaration.Modifiers.Any(SyntaxKind.StaticKeyword);

    private static Candidate Inspect(GeneratorSyntaxContext context)
    {
        var declaration = (ClassDeclarationSyntax) context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not { } symbol)
            return default;

        var isFamily = DerivesFrom(symbol, FAMILY_BASE_TYPE);
        var isHost = symbol.AllInterfaces.Any(candidate => candidate.ToDisplayString() == HOST_INTERFACE);
        if (!isFamily && !isHost)
            return default;

        return new Candidate(symbol.ToDisplayString(), isFamily, isHost, WhyItCannotBeCreated(symbol), declaration.Identifier.GetLocation());
    }

    private static bool DerivesFrom(INamedTypeSymbol symbol, string baseTypeName)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
            if (current.ToDisplayString() == baseTypeName)
                return true;

        return false;
    }

    /// <summary>
    /// Why the generated registry could not create this type, or null when it can.
    /// </summary>
    /// <param name="symbol">The type to look at.</param>
    /// <returns>A phrase which completes the diagnostic message, or null.</returns>
    private static string? WhyItCannotBeCreated(INamedTypeSymbol symbol)
    {
        if (symbol.IsAbstract)
            return "it is abstract";

        if (symbol.IsGenericType)
            return "it is generic";

        if (symbol.ContainingType is not null)
            return "it is nested inside another type";

        if (symbol.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            return "the registry cannot reach it from outside its own type";

        var hasParameterlessConstructor = symbol.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0 &&
            constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);

        return hasParameterlessConstructor ? null : "it has no parameterless constructor the registry can reach";
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<Candidate> candidates)
    {
        var families = new List<string>();
        var hosts = new List<string>();

        foreach (var candidate in candidates)
        {
            if (candidate.FullName is null)
                continue;

            if (candidate.Problem is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(CANNOT_BE_REGISTERED, candidate.Location ?? Location.None, candidate.FullName, candidate.Problem));
                continue;
            }

            if (candidate.IsFamily)
                families.Add(candidate.FullName);

            if (candidate.IsHost)
                hosts.Add(candidate.FullName);
        }

        //
        // Sorted by name and without repeats, so that the same sources produce the same file: a
        // partial class arrives here once per part, and the order syntax nodes are visited in is
        // not something to build a shipped artefact on.
        //
        var source = RenderSource(Ordered(families), Ordered(hosts));
        context.AddSource("ModelFamilies.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static IReadOnlyList<string> Ordered(IEnumerable<string> typeNames) => typeNames.Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToList();

    private static string RenderSource(IReadOnlyList<string> families, IReadOnlyList<string> hosts)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.Append("namespace ").Append(GENERATED_NAMESPACE).AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Every model family and every model host this assembly declares.");
        builder.AppendLine("/// </summary>");
        builder.Append("public static class ").AppendLine(GENERATED_TYPE_NAME);
        builder.AppendLine("{");

        AppendFactory(builder, "CreateFamilies", FAMILY_BASE_TYPE, families);
        builder.AppendLine();
        AppendFactory(builder, "CreateHosts", HOST_INTERFACE, hosts);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendFactory(StringBuilder builder, string methodName, string typeName, IReadOnlyList<string> typeNames)
    {
        builder.Append("    public static global::System.Collections.Generic.IReadOnlyList<global::").Append(typeName).Append("> ").Append(methodName).AppendLine("() =>");
        builder.Append("        new global::").Append(typeName).AppendLine("[]");
        builder.AppendLine("        {");

        foreach (var name in typeNames)
            builder.Append("            new global::").Append(name).AppendLine("(),");

        builder.AppendLine("        };");
    }

    /// <summary>
    /// What the syntax pass found out about one type.
    /// </summary>
    /// <remarks>
    /// A struct with value equality, because this travels through the incremental pipeline: two
    /// runs finding the same types have to compare as equal, or nothing downstream is ever cached.
    /// </remarks>
    private readonly struct Candidate(string? fullName, bool isFamily, bool isHost, string? problem, Location? location) : IEquatable<Candidate>
    {
        public string? FullName { get; } = fullName;

        public bool IsFamily { get; } = isFamily;

        public bool IsHost { get; } = isHost;

        public string? Problem { get; } = problem;

        public Location? Location { get; } = location;

        public bool Equals(Candidate other) =>
            this.FullName == other.FullName &&
            this.IsFamily == other.IsFamily &&
            this.IsHost == other.IsHost &&
            this.Problem == other.Problem &&
            Equals(this.Location, other.Location);

        public override bool Equals(object? obj) => obj is Candidate other && this.Equals(other);

        public override int GetHashCode() => this.FullName?.GetHashCode() ?? 0;
    }
}