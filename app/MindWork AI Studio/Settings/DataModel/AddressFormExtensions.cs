using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class AddressFormExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AddressFormExtensions).Namespace, nameof(AddressFormExtensions));

    /// <param name="addressForm">The form of address.</param>
    extension(AddressForm addressForm)
    {
        /// <summary>
        /// Gets the name of the form of address, as the settings show it.
        /// </summary>
        public string Name => addressForm switch
        {
            AddressForm.FORMAL => TB("Formal address (Sie in German)"),
            AddressForm.INFORMAL => TB("Informal address (du in German)"),

            _ => TB("Formal address (Sie in German)"),
        };
    }
}