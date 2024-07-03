namespace AIStudio.Settings;

public static class SettingsMigrations
{
    public static Data Migrate(Data previousData)
    {
        switch (previousData.Version)
        {
            case Version.V1:
                return MigrateFromV1(previousData);
            
            default:
                Console.WriteLine("No migration needed.");
                return previousData;
        }
    }

    private static Data MigrateFromV1(Data previousData)
    {
        //
        // Summary:
        // In v1 we had no self-hosted providers. Thus, we had no hostnames.
        //

        Console.WriteLine("Migrating from v1 to v2...");
        return new()
        {
            Version = Version.V2,
            
            Providers = previousData.Providers.Select(provider => provider with { IsSelfHosted = false, Hostname = "" }).ToList(),
            
            EnableSpellchecking = previousData.EnableSpellchecking,
            IsSavingEnergy = previousData.IsSavingEnergy,
            NextProviderNum = previousData.NextProviderNum,
            ShortcutSendBehavior = previousData.ShortcutSendBehavior,
            UpdateBehavior = previousData.UpdateBehavior,
        };
    }
}