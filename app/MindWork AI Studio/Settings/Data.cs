namespace AIStudio.Settings;

public sealed class Data
{
    public Version Version { get; init; }

    public List<Provider> Providers { get; init; } = new();
}