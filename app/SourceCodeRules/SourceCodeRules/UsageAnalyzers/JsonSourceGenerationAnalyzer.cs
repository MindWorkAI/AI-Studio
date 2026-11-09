using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SourceCodeRules.UsageAnalyzers;

/// <summary>
/// Reports JSON which System.Text.Json would read or write with contracts it builds by reflection.
/// </summary>
/// <remarks>
/// The options of AI Studio name a JsonSerializerContext as their TypeInfoResolver, so that the contracts of
/// their types are generated while compiling. A contract which System.Text.Json builds by reflection instead
/// may lack a property or fail altogether in a published build, since the trimmer removes code which only
/// reflection reaches. Neither a debug build nor the tests show that. Switching reflection off for the whole
/// app is no option, because Blazor Server needs it for JSON of its own. Hence, this analyzer refuses every
/// way into reflection within the code of AI Studio: a call without options, the options
/// JsonSerializerOptions.Default and .Web, a DefaultJsonTypeInfoResolver, and new options without a
/// TypeInfoResolver.
/// </remarks>
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class JsonSourceGenerationAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.JSON_SOURCE_GENERATION_ANALYZER;
    private const string CATEGORY = "Usage";

    private const string JSON_NAMESPACE = "System.Text.Json";
    private const string SERIALIZATION_NAMESPACE = "System.Text.Json.Serialization";
    private const string METADATA_NAMESPACE = "System.Text.Json.Serialization.Metadata";
    private const string OPTIONS_TYPE = "JsonSerializerOptions";
    private const string TYPE_INFO_RESOLVER = "TypeInfoResolver";

    private const string TITLE = "JSON must use the generated contracts of its options";

    private const string MESSAGE_FORMAT = "{0} A contract which System.Text.Json builds by reflection may break in a published build, since the trimmer removes what reflection needs";

    private const string DESCRIPTION = "AI Studio reads and writes JSON with options whose TypeInfoResolver is a JsonSerializerContext, so that every contract is generated while compiling. A call without options, the options JsonSerializerOptions.Default and .Web, a DefaultJsonTypeInfoResolver, and new options without a TypeInfoResolver all make System.Text.Json build the contracts by reflection, which only a published build shows to fail.";

    /// <summary>
    /// The types whose methods read or write JSON, and which fall back to reflection without options.
    /// </summary>
    private static readonly ImmutableHashSet<string> SERIALIZING_TYPES =
    [
        "System.Text.Json.JsonSerializer",
        "System.Net.Http.Json.HttpClientJsonExtensions",
        "System.Net.Http.Json.HttpContentJsonExtensions",
        "System.Net.Http.Json.JsonContent",
    ];

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    /// <summary>
    /// Reports reading or writing JSON without options, or with options which are null.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!SERIALIZING_TYPES.Contains(method.ContainingType.ToDisplayString()))
            return;

        //
        // A method which gets the contract itself, or the context which holds it, needs no options:
        //
        if (method.Parameters.Any(parameter => IsContract(parameter.Type)))
            return;

        var options = invocation.Arguments.FirstOrDefault(argument => Is(argument.Parameter?.Type, JSON_NAMESPACE, OPTIONS_TYPE));
        if (options is not null && options.ArgumentKind is not ArgumentKind.DefaultValue && !IsNull(options.Value))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, invocation.Syntax.GetLocation(), $"{method.ContainingType.Name}.{method.Name} gets no options, so it builds the contract of its type by reflection: pass the options whose context knows the type."));
    }

    /// <summary>
    /// Reports the shared options of System.Text.Json, which build every contract by reflection.
    /// </summary>
    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (reference.Property is not { IsStatic: true, Name: "Default" or "Web" } property || !Is(property.ContainingType, JSON_NAMESPACE, OPTIONS_TYPE))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, reference.Syntax.GetLocation(), $"JsonSerializerOptions.{property.Name} builds every contract by reflection: use the options whose context knows the type."));
    }

    /// <summary>
    /// Reports a resolver which builds contracts by reflection, and new options which name no resolver.
    /// </summary>
    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (IsReflectionResolver(creation.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(RULE, creation.Syntax.GetLocation(), $"{creation.Type!.Name} builds contracts by reflection: register the type in the JsonSerializerContext of its options instead."));
            return;
        }

        if (!Is(creation.Type, JSON_NAMESPACE, OPTIONS_TYPE) || NamesTypeInfoResolver(creation.Initializer))
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, creation.Syntax.GetLocation(), "These options name no TypeInfoResolver, so System.Text.Json builds their contracts by reflection: set it to the JsonSerializerContext which knows their types, right in the object initializer."));
    }

    /// <summary>
    /// Whether a parameter takes a contract, as a JsonTypeInfo, or the context which holds the contracts.
    /// </summary>
    private static bool IsContract(ITypeSymbol type) =>
        Is(type, METADATA_NAMESPACE, "JsonTypeInfo") ||
        Is(type, SERIALIZATION_NAMESPACE, "JsonSerializerContext");

    private static bool IsReflectionResolver(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (Is(current, METADATA_NAMESPACE, "DefaultJsonTypeInfoResolver"))
                return true;

        return false;
    }

    private static bool NamesTypeInfoResolver(IObjectOrCollectionInitializerOperation? initializer) =>
        initializer is not null &&
        initializer.Initializers.Any(item => item is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: TYPE_INFO_RESOLVER } } assignment && !IsNull(assignment.Value));

    private static bool IsNull(IOperation value) => value.ConstantValue is { HasValue: true, Value: null };

    private static bool Is(ITypeSymbol? type, string namespaceName, string typeName) =>
        type is not null &&
        type.Name == typeName &&
        type.ContainingNamespace?.ToDisplayString() == namespaceName;
}
