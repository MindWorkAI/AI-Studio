using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

        return string.Join(Environment.NewLine, errors);
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