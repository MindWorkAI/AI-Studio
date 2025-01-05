namespace AIStudio.Settings.DataModel.PreviousModels;

public static class ProviderV4Extensions
{
    public static List<AIStudio.Settings.Provider> MigrateFromV4ToV5(this IEnumerable<Provider> providers)
    {
        return providers.Select(provider => provider.MigrateFromV4ToV5()).ToList();
    }
    
    public static AIStudio.Settings.Provider MigrateFromV4ToV5(this Provider provider) => new()
    {
        Num = provider.Num,
        Id = provider.Id,
        InstanceName = provider.InstanceName,
        UsedLLMProvider = provider.UsedProvider,
        Model = provider.Model,
        IsSelfHosted = provider.IsSelfHosted,
        Hostname = provider.Hostname,
        Host = provider.Host,
    };
}