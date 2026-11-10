using AIStudio.Assistants.I18N;

using SharedTools;

namespace AIStudio.Tests.Assistants.I18N;

/// <summary>
/// Checks how the localization assistant translates the two variants of a text of TA(), which addresses the reader.
/// </summary>
/// <remarks>
/// The informal variant starts from the formal translation, so that both read alike and a reviewed formal translation
/// keeps its wording. The formal translations of a run therefore come first.
/// </remarks>
[TestFixture]
public sealed class I18NAddressPromptTests
{
    private const string KEY = "root::AISTUDIO::TESTS::TEXTS::T123";
    private static readonly string INFORMAL_KEY = I18NAddressVariant.InformalKeyOf(KEY);

    [Test]
    public void TheInformalVariantsComeAfterAllOtherTexts()
    {
        var content = new Dictionary<string, string>
        {
            [INFORMAL_KEY] = "Enter your name.",
            ["root::A::T1"] = "Save",
            [KEY] = "Enter your name.",
            ["root::A::T2::INFORMAL"] = "Check your input.",
            ["root::A::T2"] = "Check your input.",
        };

        var order = I18NAddressPrompt.FormalFirst(content).Select(entry => entry.Key);

        Assert.That(order, Is.EqualTo(new[] { "root::A::T1", KEY, "root::A::T2", INFORMAL_KEY, "root::A::T2::INFORMAL" }));
    }

    [Test]
    public void TheFormalTranslationOfThisRunComesFirst()
    {
        var translatedNow = new Dictionary<string, string> { [KEY] = "Geben Sie Ihren Namen ein." };
        var translatedBefore = new Dictionary<string, string> { [KEY] = "Ihr Name, bitte." };

        Assert.That(I18NAddressPrompt.FindFormalTranslation(INFORMAL_KEY, translatedNow, translatedBefore), Is.EqualTo("Geben Sie Ihren Namen ein."));
    }

    [Test]
    public void TheFormalTranslationOfTheLanguagePluginCountsAsWell()
    {
        var translatedBefore = new Dictionary<string, string> { [KEY] = "Geben Sie Ihren Namen ein." };

        Assert.That(I18NAddressPrompt.FindFormalTranslation(INFORMAL_KEY, new Dictionary<string, string>(), translatedBefore), Is.EqualTo("Geben Sie Ihren Namen ein."));
    }

    [Test]
    public void WithoutAFormalTranslationThereIsNone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(I18NAddressPrompt.FindFormalTranslation(INFORMAL_KEY, new Dictionary<string, string>(), new Dictionary<string, string> { [KEY] = " " }), Is.Null);
            Assert.That(I18NAddressPrompt.FindFormalTranslation(KEY, new Dictionary<string, string> { [KEY] = "Geben Sie Ihren Namen ein." }, new Dictionary<string, string>()), Is.Null, "A usual key is no informal variant.");
        }
    }

    [Test]
    public void AUsualTextAddressesTheReaderFormally()
    {
        var section = I18NAddressPrompt.Section("German (Germany)", false, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(section, Does.Contain("formal address of German (Germany)"));
            Assert.That(section, Does.Not.Contain("informal"));
        }
    }

    [Test]
    public void AnInformalVariantChangesOnlyTheAddressOfTheFormalTranslation()
    {
        const string FORMAL = "## Willkommen!\n\nGeben Sie Ihren Namen ein.";

        // The line endings of the prompt are those of the checkout:
        var section = I18NAddressPrompt.Section("German (Germany)", true, FORMAL).ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(section, Does.Contain("informal address of German (Germany)"));
            Assert.That(section, Does.Contain($"<formal_translation>\n{FORMAL}\n</formal_translation>"));
            Assert.That(section, Does.Contain("change only what the informal address"));
        }
    }

    [Test]
    public void AnInformalVariantWithoutAFormalTranslationIsTranslatedInformally()
    {
        var section = I18NAddressPrompt.Section("French (France)", true, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(section, Does.Contain("informal address of French (France)"));
            Assert.That(section, Does.Not.Contain("formal_translation"));
        }
    }
}