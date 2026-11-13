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
        /// <remarks>
        /// The settings offer the choice only in languages which have both forms, so the translations name the
        /// pronoun of their language, such as "Sie" and "du" in German.
        /// </remarks>
        public string Name => addressForm switch
        {
            AddressForm.FORMAL => TB("Formal address"),
            AddressForm.INFORMAL => TB("Informal address"),

            _ => TB("Formal address"),
        };
    }
}