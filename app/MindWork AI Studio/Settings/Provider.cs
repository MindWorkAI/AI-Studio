using AIStudio.Provider;

namespace AIStudio.Settings;

public readonly record struct Provider(string Id, string InstanceName, Providers UsedProvider);