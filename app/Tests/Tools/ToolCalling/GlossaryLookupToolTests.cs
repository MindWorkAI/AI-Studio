using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Glossaries;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Glossaries;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Glossary Lookup describes the glossaries and returns what it found.
/// </summary>
/// <remarks>
/// The model learns from the description which glossaries there are and what they are for, and the
/// schema lets it name exactly those. In the result, it has to tell a binding translation from an
/// explanation, and it pages with has_more rather than a total.
/// </remarks>
[TestFixture]
public sealed class GlossaryLookupToolTests
{
    private static readonly Glossary TRANSLATION_GLOSSARY = new()
    {
        Id = "11111111-1111-1111-1111-111111111111",
        Name = "HR terms",
        Type = GlossaryType.TRANSLATION,
        SourceLanguage = CommonLanguages.DE_DE,
        TargetLanguage = CommonLanguages.EN_US,
    };

    private static readonly Glossary REFERENCE_GLOSSARY = new()
    {
        Id = "22222222-2222-2222-2222-222222222222",
        Name = "Harry Potter",
        Type = GlossaryType.REFERENCE,
        Description = "Invented words\nof the books.",
    };

    [Test]
    public void TheFunctionListsTheGlossariesAndOffersExactlyTheirIds()
    {
        var function = GlossaryLookupTool.DescribeGlossaries(new GlossaryLookupTool(null!, null!).GetDefinition().Function, [TRANSLATION_GLOSSARY, REFERENCE_GLOSSARY]);

        Assert.Multiple(() =>
        {
            Assert.That(function.Name, Is.EqualTo(ToolSelectionRules.GLOSSARY_LOOKUP_TOOL_ID));
            Assert.That(function.DescriptionForLLM, Does.Contain($"- `{TRANSLATION_GLOSSARY.Id}`: HR terms (translation, de-DE -> en-US)"));
            Assert.That(function.DescriptionForLLM, Does.Contain($"- `{REFERENCE_GLOSSARY.Id}`: Harry Potter (reference): Invented words of the books."));
            Assert.That(OfferedIds(function), Is.EqualTo(new[] { TRANSLATION_GLOSSARY.Id, REFERENCE_GLOSSARY.Id }));
        });
    }

    [Test]
    public void TheResultTellsTranslationsFromExplanations()
    {
        var result = GlossaryLookupTool.BuildResult(
        [
            new(TRANSLATION_GLOSSARY, [new GlossaryEntry("Arbeitszeit", "working time")]),
            new(REFERENCE_GLOSSARY, [new GlossaryEntry("Muggel", "A person without magical abilities.", ["Muggels"], "Book 1")]),
        ], page: 1);

        var entries = result["entries"]!.AsArray();
        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0]!["type"]!.GetValue<string>(), Is.EqualTo("translation"));
            Assert.That(entries[0]!["translation"]!.GetValue<string>(), Is.EqualTo("working time"));
            Assert.That(entries[0]!["target_language"]!.GetValue<string>(), Is.EqualTo("en-US"));
            Assert.That(entries[1]!["type"]!.GetValue<string>(), Is.EqualTo("reference"));
            Assert.That(entries[1]!["explanation"]!.GetValue<string>(), Is.EqualTo("A person without magical abilities."));
            Assert.That(entries[1]!["variants"]!.AsArray().Select(variant => variant!.GetValue<string>()), Is.EqualTo(new[] { "Muggels" }));
            Assert.That(entries[1]!["note"]!.GetValue<string>(), Is.EqualTo("Book 1"));
            Assert.That(result["has_more"]!.GetValue<bool>(), Is.False);
        });
    }

    [Test]
    public void ManyMatchesComeInPages()
    {
        var entries = Enumerable.Range(1, 60).Select(index => new GlossaryEntry($"Begriff{index}", $"term{index}")).ToList();
        IReadOnlyList<GlossaryMatches> matches = [new(TRANSLATION_GLOSSARY, entries)];

        var firstPage = GlossaryLookupTool.BuildResult(matches, page: 1);
        var secondPage = GlossaryLookupTool.BuildResult(matches, page: 2);

        Assert.Multiple(() =>
        {
            Assert.That(firstPage["entries"]!.AsArray(), Has.Count.EqualTo(50));
            Assert.That(firstPage["has_more"]!.GetValue<bool>(), Is.True);
            Assert.That(secondPage["entries"]!.AsArray(), Has.Count.EqualTo(10));
            Assert.That(secondPage["has_more"]!.GetValue<bool>(), Is.False);
            Assert.That(firstPage.ContainsKey("total"), Is.False, "Paging reports has_more, never a total.");
        });
    }

    [Test]
    public void NothingFoundSaysSo()
    {
        var result = GlossaryLookupTool.BuildResult([new(TRANSLATION_GLOSSARY, [])], page: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result["entries"]!.AsArray(), Is.Empty);
            Assert.That(result["message"]!.GetValue<string>(), Is.Not.Empty);
            Assert.That(result.ContainsKey("unavailable_glossaries"), Is.False);
        });
    }

    [Test]
    public void AnUnreadableGlossaryIsNoGlossaryWithoutMatches()
    {
        var result = GlossaryLookupTool.BuildResult(
        [
            new(TRANSLATION_GLOSSARY, [], LoadFailed: true),
            new(REFERENCE_GLOSSARY, [new GlossaryEntry("Muggel", "A person without magical abilities.")]),
        ], page: 1);

        var unavailable = result["unavailable_glossaries"]!.AsArray();
        Assert.Multiple(() =>
        {
            Assert.That(unavailable, Has.Count.EqualTo(1));
            Assert.That(unavailable[0]!["glossary_id"]!.GetValue<string>(), Is.EqualTo(TRANSLATION_GLOSSARY.Id));
            Assert.That(unavailable[0]!["glossary"]!.GetValue<string>(), Is.EqualTo(TRANSLATION_GLOSSARY.Name));
            Assert.That(result["entries"]!.AsArray(), Has.Count.EqualTo(1), "The readable glossary still counts.");
            Assert.That(result["message"]!.GetValue<string>(), Does.Contain("could not be read"));
        });
    }

    [Test]
    public void AnUnreadableGlossaryNeverPassesForNoMatches()
    {
        var result = GlossaryLookupTool.BuildResult([new(TRANSLATION_GLOSSARY, [], LoadFailed: true)], page: 1);

        Assert.That(result["message"]!.GetValue<string>(), Does.Not.Contain("No glossary term occurs"));
    }

    private static IReadOnlyList<string?> OfferedIds(ToolFunctionDefinition function) => function.Parameters
        .GetProperty("properties")
        .GetProperty("glossary_ids")
        .GetProperty("items")
        .GetProperty("enum")
        .EnumerateArray()
        .Select(id => id.GetString())
        .ToList();
}