namespace AIStudio.Settings;

public abstract record ConfigMetaBase : IConfig
{
    protected static SettingsManager CurrentSettingsManager => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
}
