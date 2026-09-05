namespace AIStudio.Settings;

/// <summary>
/// A data structure to map a name to a value.
/// </summary>
/// <param name="Name">The name of the value, to be displayed in the UI.</param>
/// <param name="Value">The value to be stored.</param>
/// <typeparam name="T">The type of the value to store.</typeparam>
public readonly record struct ConfigurationSelectData<T>(string Name, T Value);