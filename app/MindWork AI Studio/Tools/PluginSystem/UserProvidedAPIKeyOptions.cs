using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// What a configuration plugin allows for the API key of a provider it manages: whether the user may set
/// their own key, and whether AI Studio asks the user for it on the home page.
/// </summary>
/// <param name="AllowUserProvidedAPIKey">The user may set their own API key for the otherwise locked provider.</param>
/// <param name="PromptForUserAPIKey">AI Studio asks the user for the API key on the home page as long as none is stored.</param>
public readonly record struct UserProvidedAPIKeyOptions(bool AllowUserProvidedAPIKey, bool PromptForUserAPIKey)
{
    /// <summary>
    /// Reads both options from the Lua table of a provider. An option which is missing or not a boolean counts as false.
    /// </summary>
    /// <remarks>
    /// Asking the user for the key only makes sense when the user may set it. Therefore, PromptForUserAPIKey
    /// implies AllowUserProvidedAPIKey, and a configuration needs to name only the one option. The implied
    /// option also keeps the key of the user in the OS keyring when the configuration is withdrawn.
    /// </remarks>
    /// <param name="table">The Lua table of the provider.</param>
    /// <returns>The options of the provider.</returns>
    public static UserProvidedAPIKeyOptions Read(LuaTable table)
    {
        var promptForUserAPIKey = ReadOption(table, "PromptForUserAPIKey");
        var allowUserProvidedAPIKey = promptForUserAPIKey || ReadOption(table, "AllowUserProvidedAPIKey");
        return new UserProvidedAPIKeyOptions(allowUserProvidedAPIKey, promptForUserAPIKey);
    }

    private static bool ReadOption(LuaTable table, string name) => table.TryGetValue(name, out var value) && value.TryRead<bool>(out var isSet) && isSet;
}