using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

using SharedTools;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks the two variants of a text of TA(), which addresses the reader: their keys, and which of them the app shows.
/// </summary>
/// <remarks>
/// The informal variant has a key of its own. When the language in use lacks it, the person reads the formal variant
/// of that language, never the English text which the base language holds under the informal key.
/// </remarks>
[TestFixture]
public sealed class I18NAddressVariantTests
{
    private const string NAMESPACE = "AIStudio.Tests";
    private const string TYPE = "Texts";
    private const string TEXT = "Enter your name.";
    private const string FORMAL = "Geben Sie Ihren Namen ein.";
    private const string INFORMAL = "Gib deinen Namen ein.";

    private static readonly string KEY = $"root::AISTUDIO::TESTS::TEXTS::T{I18NKeyHash.Of(TEXT)}";
    private static readonly string INFORMAL_KEY = I18NAddressVariant.InformalKeyOf(KEY);

    [Test]
    public void TheKeyOfTheInformalVariantLeadsBackToTheUsualKey()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(INFORMAL_KEY, Is.EqualTo($"{KEY}::INFORMAL"));
            Assert.That(I18NAddressVariant.TryGetFormalKey(INFORMAL_KEY, out var formalKey), Is.True);
            Assert.That(formalKey, Is.EqualTo(KEY));
        }
    }

    [Test]
    public void AUsualKeyBelongsToNoInformalVariant()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(I18NAddressVariant.TryGetFormalKey(KEY, out var formalKey), Is.False);
            Assert.That(formalKey, Is.EqualTo(KEY));
        }
    }

    [Test]
    public void TheInformalVariantShowsWhenThePersonWantsIt()
    {
        var plugin = new FakeLanguagePlugin(new Dictionary<string, string> { [KEY] = FORMAL, [INFORMAL_KEY] = INFORMAL });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Lookup(plugin, AddressForm.INFORMAL), Is.EqualTo(INFORMAL));
            Assert.That(Lookup(plugin, AddressForm.FORMAL), Is.EqualTo(FORMAL));
            Assert.That(I18N.I.GetText(plugin, TEXT, NAMESPACE, TYPE), Is.EqualTo(FORMAL), "Texts of T() address the reader formally.");
        }
    }

    [Test]
    public void WithoutAnInformalVariantTheFormalTextOfTheLanguageShows()
    {
        // The English language plugin, the base of every other one, holds the informal variant in English:
        var plugin = new FakeLanguagePlugin(
            new Dictionary<string, string> { [KEY] = FORMAL },
            new Dictionary<string, string> { [KEY] = TEXT, [INFORMAL_KEY] = TEXT });

        Assert.That(Lookup(plugin, AddressForm.INFORMAL), Is.EqualTo(FORMAL));
    }

    [Test]
    public void AnEmptyInformalVariantDoesNotCount()
    {
        var plugin = new FakeLanguagePlugin(new Dictionary<string, string> { [KEY] = FORMAL, [INFORMAL_KEY] = " " });

        Assert.That(Lookup(plugin, AddressForm.INFORMAL), Is.EqualTo(FORMAL));
    }

    [Test]
    public void WithoutAnyTranslationTheEnglishTextShows()
    {
        var plugin = new FakeLanguagePlugin(new Dictionary<string, string>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Lookup(plugin, AddressForm.INFORMAL), Is.EqualTo(TEXT));
            Assert.That(Lookup(plugin, AddressForm.FORMAL), Is.EqualTo(TEXT));
            Assert.That(I18N.I.GetText(NoPluginLanguage.INSTANCE, TEXT, NAMESPACE, TYPE, AddressForm.INFORMAL), Is.EqualTo(TEXT));
        }
    }

    [Test]
    public void TheTextsAddressThePersonFormallyUntilTheAppSaysOtherwise()
    {
        Assert.That(I18N.I.AddressForm, Is.EqualTo(AddressForm.FORMAL), "Nothing in the tests sets where the form of address comes from.");
    }

    private static string Lookup(ILanguagePlugin plugin, AddressForm addressForm) => I18N.I.GetText(plugin, TEXT, NAMESPACE, TYPE, addressForm);
}