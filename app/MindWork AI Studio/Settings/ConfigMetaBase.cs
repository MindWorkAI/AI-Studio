namespace AIStudio.Settings;

public abstract record ConfigMetaBase : IConfig
{
    protected static SettingsManager SettingsManager => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
}