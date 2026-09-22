using AIStudio.Models.Registry;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using SourceGeneratedMappings;

namespace AIStudio.Tests.Models.Generation;

/// <summary>
/// Checks that adding a family is one action, and that nothing else is needed to make it count.
/// </summary>
/// <remarks>
/// The whole point of generating the registry is that nobody has to remember a list. If the
/// generator misses a family, the family answers for nothing, its models fall into the global
/// default, and they look unremarkable rather than broken -- which is the hardest kind of defect to
/// notice. So the generator is asked directly, against source written for the purpose.
/// </remarks>
[TestFixture]
public sealed class ModelRegistryGeneratorTests
{
    /// <summary>
    /// Two families, one of them two levels down, one host, and an abstract class in between.
    /// </summary>
    private const string TWO_FAMILIES_AND_A_HOST =
        """
        using System;

        using AIStudio.Models;
        using AIStudio.Models.Hosting;
        using AIStudio.Models.Matching;
        using AIStudio.Provider;

        namespace Sample;

        public abstract class HalfAFamily : ModelFamily
        {
            public override ModelVendor Vendor => ModelVendor.OPEN_AI;

            public override ModelSource Source => new("https://example.invalid/half", new DateOnly(2026, 9, 11), "a note");
        }

        public sealed class SecondFamily : HalfAFamily
        {
            protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("second");
        }

        public sealed class FirstFamily : ModelFamily
        {
            public override ModelVendor Vendor => ModelVendor.ANTHROPIC;

            public override ModelSource Source => new("https://example.invalid/first", new DateOnly(2026, 9, 11), "a note");

            protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("first");
        }

        public sealed class SampleHost : IModelHost
        {
            public LLMProviders Provider => LLMProviders.NONE;

            public ModelSource Source => new("https://example.invalid/host", new DateOnly(2026, 9, 11), "a note");

            public bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
            {
                inner = id;
                declaredVendor = null;
                return false;
            }

            public ModelProfile ApplyTransport(in ModelProfile profile) => profile;
        }
        """;

    /// <summary>
    /// A family the registry cannot create, because it asks for something to be handed in.
    /// </summary>
    private const string A_FAMILY_NEEDING_AN_ARGUMENT =
        """
        using System;

        using AIStudio.Models;

        namespace Sample;

        public sealed class DemandingFamily(int somethingItNeeds) : ModelFamily
        {
            public override ModelVendor Vendor => ModelVendor.OPEN_AI;

            public override ModelSource Source => new("https://example.invalid/demanding", new DateOnly(2026, 9, 11), $"needs {somethingItNeeds}");

            protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("demanding");
        }
        """;

    [Test]
    public void EveryFamilyIsFoundWithoutBeingAddedToAnything()
    {
        var generated = Generate(TWO_FAMILIES_AND_A_HOST, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(generated, Does.Contain("new global::Sample.FirstFamily()"));
            Assert.That(generated, Does.Contain("new global::Sample.SecondFamily()"), "A family which inherits through another class is still a family.");
            Assert.That(generated, Does.Contain("new global::Sample.SampleHost()"));
        });
    }

    [Test]
    public void AClassWhichCannotBeAFamilyOnItsOwnIsNotRegistered()
    {
        var generated = Generate(TWO_FAMILIES_AND_A_HOST, out _, out _);

        Assert.That(generated, Does.Not.Contain("HalfAFamily"));
    }

    [Test]
    public void TheRegistryIsWrittenInTheSameOrderEveryTime()
    {
        //
        // The order syntax nodes are visited in is not something a shipped file may depend on: the
        // same sources have to produce the same bytes, or a rebuild shows up as a change.
        //
        var generated = Generate(TWO_FAMILIES_AND_A_HOST, out _, out _);

        Assert.That(generated.IndexOf("Sample.FirstFamily", StringComparison.Ordinal), Is.LessThan(generated.IndexOf("Sample.SecondFamily", StringComparison.Ordinal)));
    }

    [Test]
    public void WhatIsGeneratedCompiles()
    {
        Generate(TWO_FAMILIES_AND_A_HOST, out var updated, out _);

        Assert.That(CompilationHarness.ErrorsOf(updated), Is.Empty);
    }

    [Test]
    public void AnAssemblyWithoutAnyFamiliesStillGetsARegistry()
    {
        //
        // Otherwise the registry would fail to compile in exactly the situation where somebody is
        // about to write their first family.
        //
        var generated = Generate("namespace Sample;\n\npublic sealed class NothingToDoWithModels;", out var updated, out _);

        Assert.Multiple(() =>
        {
            Assert.That(generated, Does.Contain("public static class ModelRegistrations"));
            Assert.That(CompilationHarness.ErrorsOf(updated), Is.Empty);
        });
    }

    [Test]
    public void AFamilyTheRegistryCannotCreateIsReportedRatherThanSkippedQuietly()
    {
        var generated = Generate(A_FAMILY_NEEDING_AN_ARGUMENT, out _, out var diagnostics);
        var reported = diagnostics.Where(diagnostic => diagnostic.Id is "MDR001").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.Contain("DemandingFamily"));
            Assert.That(generated, Does.Not.Contain("DemandingFamily"));
        });
    }

    [Test]
    public void TheAppItselfHasARegistryTheGeneratorWrote()
    {
        //
        // The tests above run the generator by hand. This one asks whether it also ran while the app
        // was built, which is a different question and the one that actually matters.
        //
        Assert.Multiple(() =>
        {
            Assert.That(ModelRegistrations.CreateFamilies(), Is.Not.Null);
            Assert.That(ModelRegistrations.CreateHosts(), Is.Not.Null);
        });
    }

    private static string Generate(string source, out Compilation updated, out IReadOnlyList<Diagnostic> diagnostics)
    {
        var compilation = CompilationHarness.Compile(source);
        Assert.That(CompilationHarness.ErrorsOf(compilation), Is.Empty, "The snippet has to compile, or the generator is being asked about code which does not exist.");

        var driver = CSharpGeneratorDriver.Create(new ModelRegistryGenerator().AsSourceGenerator());
        var afterwards = driver.RunGeneratorsAndUpdateCompilation(compilation, out updated, out var reported);

        diagnostics = reported;
        return afterwards.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();
    }
}