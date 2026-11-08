namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflAddressFormExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflAddressFormExtensions).Namespace, nameof(BflAddressFormExtensions));

    /// <param name="addressForm">The form of address.</param>
    extension(BflAddressForm addressForm)
    {
        /// <summary>
        /// Gets the name of the form of address, as the settings show it.
        /// </summary>
        public string Name => addressForm switch
        {
            BflAddressForm.FORMAL => TB("Formal address (Sie in German)"),
            BflAddressForm.INFORMAL => TB("Informal address (du in German)"),

            _ => TB("Formal address (Sie in German)"),
        };
    }
}