using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SourceCodeRules.UsageAnalyzers;

/// <summary>
/// Refuses the line break of the system: StringBuilder.AppendLine and Environment.NewLine.
/// </summary>
/// <remarks>
/// Both write CR LF on Windows and LF everywhere else. A text built with them differs between the systems, it mixes
/// two kinds of line breaks as soon as one of its parts comes with LF, and a test which expects LF fails on Windows
/// only, where no pipeline runs the tests. AppendLineLF of SharedTools.Text and '\n' write LF on every system. Where a
/// format needs CR LF, such as a row of a CSV file, write it on purpose, as CsvWriter.ROW_END does.
/// </remarks>
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class SystemLineBreakAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The key of the properties of a diagnostic which tells the code fix what was found.
    /// </summary>
    public const string KIND_KEY = "Kind";

    /// <summary>
    /// The kind of a diagnostic about a call of StringBuilder.AppendLine.
    /// </summary>
    public const string KIND_APPEND_LINE = "AppendLine";

    /// <summary>
    /// The kind of a diagnostic about Environment.NewLine.
    /// </summary>
    public const string KIND_NEW_LINE = "NewLine";

    private const string DIAGNOSTIC_ID = Identifier.SYSTEM_LINE_BREAK_ANALYZER;

    private const string TITLE = "Line breaks are LF on every system";

    private const string MESSAGE_FORMAT = "'{0}' writes the line break of the system, which is CR LF on Windows; use {1} instead";

    private const string DESCRIPTION = "StringBuilder.AppendLine and Environment.NewLine write CR LF on Windows and LF elsewhere, so a text differs between the systems and mixes two kinds of line breaks. Use AppendLineLF of SharedTools.Text or '\\n'.";

    private const string CATEGORY = "Usage";

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    private static readonly ImmutableDictionary<string, string?> APPEND_LINE_PROPERTIES = ImmutableDictionary<string, string?>.Empty.Add(KIND_KEY, KIND_APPEND_LINE);

    private static readonly ImmutableDictionary<string, string?> NEW_LINE_PROPERTIES = ImmutableDictionary<string, string?>.Empty.Add(KIND_KEY, KIND_NEW_LINE);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        //
        // Razor markup ends up in generated code, and so does the code block of a .razor file. We analyze generated
        // code as well, and the Razor compiler maps the diagnostic back to the line of the .razor file:
        //
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name is not "AppendLine" || invocation.TargetMethod.ContainingType.ToDisplayString() is not "System.Text.StringBuilder")
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, invocation.Syntax.GetLocation(), APPEND_LINE_PROPERTIES, "StringBuilder.AppendLine", "AppendLineLF of SharedTools.Text"));
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (reference.Property.Name is not "NewLine" || reference.Property.ContainingType.ToDisplayString() is not "System.Environment")
            return;

        context.ReportDiagnostic(Diagnostic.Create(RULE, reference.Syntax.GetLocation(), NEW_LINE_PROPERTIES, "Environment.NewLine", "'\\n'"));
    }
}