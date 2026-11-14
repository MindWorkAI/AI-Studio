using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

/// <summary>
/// How messages to the user name a provider and its model, see IProvider.HidesIdentity.
/// </summary>
public static class ProviderIdentityExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderIdentityExtensions).Namespace, nameof(ProviderIdentityExtensions));

    /// <param name="provider">The provider of a request.</param>
    extension(IProvider provider)
    {
        /// <summary>
        /// The name of the provider in a message to the user: its instance name, or a neutral word while it hides
        /// which provider it is.
        /// </summary>
        public string NameForUser => provider.HidesIdentity ? TB("hidden") : provider.InstanceName;

        /// <summary>
        /// The type of the provider in a message to the user, or a neutral word while it hides which provider it is.
        /// </summary>
        public string TypeForUser => provider.HidesIdentity ? TB("hidden") : provider.Provider.ToString();

        /// <summary>
        /// Gets the name of a model in a message to the user, or a neutral word while the provider hides which
        /// provider it is.
        /// </summary>
        /// <param name="model">The model of the request.</param>
        /// <returns>The name of the model to show.</returns>
        public string ModelNameForUser(Model model) => provider.HidesIdentity ? TB("hidden") : model.Id;
    }
}