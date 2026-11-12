using System.Collections.Immutable;

using AIStudio.Tests.Models.Generation;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using SourceGeneratedMappings;

namespace AIStudio.Tests.Icons;

/// <summary>
/// Checks that the code points of the Lucide font become icon strings which MudBlazor shows as font icons.
/// </summary>
/// <remarks>
/// The generator runs against code points written for the purpose. Whether it also ran while the app was
/// built, against the file the app ships, is asked in LucideIconsTests.
/// </remarks>
[TestFixture]
public sealed class LucideIconGeneratorTests
{
    private const string CODEPOINTS_PATH = "/repository/app/SourceGeneratedMappings/Lucide/codepoints.json";

    /// <summary>
    /// Three names in the wrong order, two of which share their glyph, as aliases do in the real file.
    /// </summary>
    private const string THREE_ICONS =
        """
        {
          "trash-2": 57742,
          "house": 57589,
          "home": 57589
        }
        """;

    [Test]
    public void EveryNameBecomesAConstantHoldingItsGlyph()
    {
        Generate(THREE_ICONS, CODEPOINTS_PATH, out var updated, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ConstantOf(updated, "TRASH_2"), Is.EqualTo(FontIcon(0xE18E)));
            Assert.That(ConstantOf(updated, "HOUSE"), Is.EqualTo(FontIcon(0xE0F5)));
            Assert.That(ConstantOf(updated, "HOME"), Is.EqualTo(FontIcon(0xE0F5)), "An alias is a name of its own.");
        }
    }

    [Test]
    public void AnAliasNamesTheIconsItSharesItsGlyphWith()
    {
        var generated = Generate(THREE_ICONS, CODEPOINTS_PATH, out _, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated["LucideIcons.g.cs"], Does.Contain("The Lucide icon \"home\", the same glyph as \"house\"."));
            Assert.That(generated["LucideIcons.g.cs"], Does.Contain("The Lucide icon \"trash-2\"."));
        }
    }

    [Test]
    public void TheRegistryKnowsEveryNameAliasesIncluded()
    {
        var generated = Generate(THREE_ICONS, CODEPOINTS_PATH, out _, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated["LucideIconRegistry.g.cs"], Does.Contain("[\"trash-2\"] = LucideIcons.TRASH_2,"));
            Assert.That(generated["LucideIconRegistry.g.cs"], Does.Contain("[\"house\"] = LucideIcons.HOUSE,"));
            Assert.That(generated["LucideIconRegistry.g.cs"], Does.Contain("[\"home\"] = LucideIcons.HOME,"));
        }
    }

    [Test]
    public void TheIconsAreWrittenInTheSameOrderEveryTime()
    {
        //
        // The order of the file is not something the generated code may depend on: the same icons have
        // to produce the same bytes, or an update of the font shows up as a reshuffled class.
        //
        var icons = Generate(THREE_ICONS, CODEPOINTS_PATH, out _, out _)["LucideIcons.g.cs"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(icons.IndexOf(" HOME = ", StringComparison.Ordinal), Is.LessThan(icons.IndexOf(" HOUSE = ", StringComparison.Ordinal)));
            Assert.That(icons.IndexOf(" HOUSE = ", StringComparison.Ordinal), Is.LessThan(icons.IndexOf(" TRASH_2 = ", StringComparison.Ordinal)));
        }
    }

    [Test]
    public void WhatIsGeneratedCompiles()
    {
        Generate(THREE_ICONS, CODEPOINTS_PATH, out var updated, out _);

        Assert.That(CompilationHarness.ErrorsOf(updated), Is.Empty);
    }

    [Test]
    public void WithoutTheFileNothingIsGeneratedAndTheReasonIsReported()
    {
        var generated = Generate(null, CODEPOINTS_PATH, out _, out var diagnostics);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated, Is.Empty);
            Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "LUC001" }));
        }
    }

    [Test]
    public void AFileOfTheSameNameInAnotherFolderIsNotTaken()
    {
        var generated = Generate(THREE_ICONS, "/repository/app/SomethingElse/codepoints.json", out _, out var diagnostics);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated, Is.Empty);
            Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "LUC001" }));
        }
    }

    [Test]
    public void AFileWithoutIconsIsReported()
    {
        var generated = Generate("{}", CODEPOINTS_PATH, out _, out var diagnostics);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generated, Is.Empty);
            Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "LUC002" }));
        }
    }

    [Test]
    public void AnEntryWhichCannotBeAnIconIsReportedRatherThanSkippedQuietly()
    {
        const string CODEPOINTS =
            """
            {
              "Not A Name": 57400,
              "3d-box": 57401,
              "plain-letter": 65,
              "trash-2": 57742
            }
            """;

        var generated = Generate(CODEPOINTS, CODEPOINTS_PATH, out var updated, out var diagnostics);
        var skipped = diagnostics.Where(diagnostic => diagnostic.Id is "LUC003").Select(diagnostic => diagnostic.GetMessage()).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(skipped, Has.Count.EqualTo(3));
            Assert.That(skipped, Has.Some.Contains("'Not A Name'"));
            Assert.That(skipped, Has.Some.Contains("'3d-box'"));
            Assert.That(skipped, Has.Some.Contains("'plain-letter'").And.Contains("private use"));
            Assert.That(ConstantOf(updated, "TRASH_2"), Is.EqualTo(FontIcon(0xE18E)), "The valid entries still count.");
            Assert.That(generated["LucideIcons.g.cs"], Does.Not.Contain("PLAIN_LETTER"));
        }
    }

    private static string FontIcon(int codepoint) => "lucide/" + char.ConvertFromUtf32(codepoint);

    //
    // Asked of the snippet's own assembly: the app assembly, which the snippet references, has a class of
    // the same name, generated from the real file.
    //
    private static object? ConstantOf(Compilation compilation, string name) => compilation.Assembly
        .GetTypeByMetadataName("AIStudio.Tools.LucideIcons")!
        .GetMembers(name)
        .OfType<IFieldSymbol>()
        .Single()
        .ConstantValue;

    /// <summary>
    /// Runs the generator, with the code points as the only additional file, or without any file at all.
    /// </summary>
    /// <returns>The generated sources by their hint names.</returns>
    private static IReadOnlyDictionary<string, string> Generate(string? codepoints, string path, out Compilation updated, out ImmutableArray<Diagnostic> diagnostics)
    {
        var compilation = CompilationHarness.Compile("namespace Sample;\n\npublic sealed class NothingToDoWithIcons;");
        AdditionalText[] additionalTexts = codepoints is null ? [] : [new InMemoryAdditionalText(path, codepoints)];

        var driver = CSharpGeneratorDriver.Create([new LucideIconGenerator().AsSourceGenerator()], additionalTexts);
        var afterwards = driver.RunGeneratorsAndUpdateCompilation(compilation, out updated, out diagnostics);

        return afterwards.GetRunResult().Results.Single().GeneratedSources.ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}