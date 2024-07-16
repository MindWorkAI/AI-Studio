using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings;

public static class SettingsMigrations
{
    public static Data Migrate(Data previousData)
    {
        switch (previousData.Version)
        {
            case Version.V1:
                previousData = MigrateV1ToV2(previousData);
                return MigrateV2ToV3(previousData);

            case Version.V2:
                return MigrateV2ToV3(previousData);
            
            default:
                Console.WriteLine("No migration needed.");
                return previousData;
        }
    }

    private static Data MigrateV1ToV2(Data previousData)
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
    
    private static Data MigrateV2ToV3(Data previousData)
    {
        //
        // Summary:
        // In v2, self-hosted providers had no host (LM Studio, llama.cpp, ollama, etc.)
        //

        Console.WriteLine("Migrating from v2 to v3...");
        return new()
        {
            Version = Version.V3,
            Providers = previousData.Providers.Select(provider =>
            {
                if(provider.IsSelfHosted)
                    return provider with { Host = Host.LM_STUDIO };
                
                return provider with { Host = Host.NONE };
            }).ToList(),

            EnableSpellchecking = previousData.EnableSpellchecking,
            IsSavingEnergy = previousData.IsSavingEnergy,
            NextProviderNum = previousData.NextProviderNum,
            ShortcutSendBehavior = previousData.ShortcutSendBehavior,
            UpdateBehavior = previousData.UpdateBehavior,
            WorkspaceStorageBehavior = previousData.WorkspaceStorageBehavior,
            WorkspaceStorageTemporaryMaintenancePolicy = previousData.WorkspaceStorageTemporaryMaintenancePolicy,
        };
    }
}