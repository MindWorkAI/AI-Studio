using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AIStudio.Tests.Models.Generation;

/// <summary>
/// Compiles a snippet in memory so that a generator or an analyzer can be asked what it makes of it.
/// </summary>
/// <remarks>
/// Both of them are code which runs while the app is being built, and both fail quietly when they
/// are wrong: a generator which finds nothing produces an empty registry, and an analyzer which
/// recognizes nothing reports nothing. Neither shows up as a broken build, so neither can be
/// checked by building the app. It has to be done here, against source written for the purpose.
/// </remarks>
public static class CompilationHarness
{
    /// <summary>
    /// Everything the test process itself was loaded with, which includes the app assembly.
    /// </summary>
    /// <remarks>
    /// Gathered once. Reading a couple of hundred assemblies off disk per test case would make
    /// these tests slow enough that somebody stops running them.
    /// </remarks>
    private static readonly Lazy<MetadataReference[]> REFERENCES = new(GatherReferences);

    /// <summary>
    /// Compiles a snippet against the same assemblies the app is built against.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <returns>The compilation.</returns>
    public static CSharpCompilation Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

        return CSharpCompilation.Create("SnippetUnderTest", [tree], REFERENCES.Value, options);
    }

    /// <summary>
    /// Compiles a snippet and reports what it does not even parse or bind.
    /// </summary>
    /// <remarks>
    /// Worth asking before believing a generator found nothing: a snippet with a typo in it also
    /// produces an empty result, and the two look exactly alike from the outside.
    /// </remarks>
    /// <param name="compilation">The compilation to check.</param>
    /// <returns>The errors, each on its own line, or an empty string.</returns>
    public static string ErrorsOf(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString());

        return string.Join('\n', errors);
    }

    /// <summary>
    /// Runs one analyzer over a snippet.
    /// </summary>
    /// <param name="source">The C# source to analyze.</param>
    /// <param name="analyzer">The analyzer to run.</param>
    /// <returns>What the analyzer reported.</returns>
    public static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string source, DiagnosticAnalyzer analyzer)
    {
        var compilation = Compile(source);
        Assert.That(ErrorsOf(compilation), Is.Empty, "The snippet has to compile, or the analyzer is being asked about code which does not exist.");

        var reported = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        return reported;
    }

    /// <summary>
    /// Runs one analyzer over a snippet and applies its code fix to the first diagnostic it reports.
    /// </summary>
    /// <remarks>
    /// The code fix runs in a workspace of its own, as it does in the IDE. The fixed source still has to
    /// compile, since a code fix which breaks the code is worse than none.
    /// </remarks>
    /// <param name="source">The C# source to fix.</param>
    /// <param name="analyzer">The analyzer which reports what to fix.</param>
    /// <param name="codeFix">The code fix to apply.</param>
    /// <returns>The fixed source.</returns>
    public static async Task<string> FixAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider codeFix)
    {
        using var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "SnippetUnderTest",
            "SnippetUnderTest",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: REFERENCES.Value);

        var project = workspace.AddProject(projectInfo);
        var document = workspace.AddDocument(project.Id, "Snippet.cs", SourceText.From(source));

        var compilation = await document.Project.GetCompilationAsync();
        Assert.That(ErrorsOf(compilation!), Is.Empty, "The snippet has to compile, or the code fix is being asked about code which does not exist.");

        var reported = await compilation!.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        Assert.That(reported, Is.Not.Empty, "The analyzer has to report something, or there is nothing for the code fix to do.");

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, reported[0], (action, _) => actions.Add(action), CancellationToken.None);
        await codeFix.RegisterCodeFixesAsync(context);
        Assert.That(actions, Has.Count.EqualTo(1), "The code fix has to offer exactly one action.");

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var fixedText = await changedSolution.GetDocument(document.Id)!.GetTextAsync();
        var fixedSource = fixedText.ToString();

        Assert.That(ErrorsOf(Compile(fixedSource)), Is.Empty, "The fixed source has to compile.");
        return fixedSource;
    }

    private static MetadataReference[] GatherReferences()
    {
        //
        // The set the runtime resolves types from, which is exactly what this test assembly was
        // built against: the framework, the NuGet packages, and the app itself.
        //
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string assemblyPaths)
            throw new InvalidOperationException("The test host did not say which assemblies it trusts, so no compilation can be built against them.");

        return assemblyPaths
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            .Select(path => (MetadataReference) MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}