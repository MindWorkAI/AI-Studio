namespace AIStudio.Settings;

public abstract record ConfigMetaBase : IConfig
{
    protected static SettingsManager SETTINGS_MANAGER => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
}