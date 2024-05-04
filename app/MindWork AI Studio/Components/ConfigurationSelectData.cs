using AIStudio.Settings;

namespace AIStudio.Components;

/// <summary>
/// A data structure to map a name to a value.
/// </summary>
/// <param name="Name">The name of the value, to be displayed in the UI.</param>
/// <param name="Value">The value to be stored.</param>
/// <typeparam name="T">The type of the value to store.</typeparam>
public readonly record struct ConfigurationSelectData<T>(string Name, T Value);

/// <summary>
/// A static factory class to get the lists of selectable values.
/// </summary>
public static class ConfigurationSelectDataFactory
{
    public static IEnumerable<ConfigurationSelectData<SendBehavior>> GetSendBehaviorData()
    {
        yield return new("No key is sending the input", SendBehavior.NO_KEY_IS_SENDING);
        yield return new("Modifier key + enter is sending the input", SendBehavior.MODIFER_ENTER_IS_SENDING);
        yield return new("Enter is sending the input", SendBehavior.ENTER_IS_SENDING);
    }
}