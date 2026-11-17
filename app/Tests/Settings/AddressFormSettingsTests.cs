using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the setting for how the texts of AI Studio address the person.
/// </summary>
/// <remarks>
/// Only languages with a formal and an informal address, such as German, offer the choice; see
/// LanguagePluginExtensions.HasInformalAddress.
/// </remarks>
[TestFixture]
public sealed class AddressFormSettingsTests
{
    [Test]
    public void NewSettingsAddressFormally()
    {
        Assert.That(new DataApp().AddressForm, Is.EqualTo(AddressForm.FORMAL), "The texts of AI Studio addressed everybody formally before the setting existed.");
    }

    [Test]
    public void TheSettingsOfferEveryFormOfAddress()
    {
        var forms = ConfigurationSelectDataFactory.GetAddressFormData().Select(data => data.Value);

        Assert.That(forms, Is.EquivalentTo(Enum.GetValues<AddressForm>()));
    }
}