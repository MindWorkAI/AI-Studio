namespace AIStudio.Settings.DataModel.PreviousModels;

public static class ProviderV4Extensions
{
    extension(IEnumerable<Provider> providers)
    {
        public List<AIStudio.Settings.Provider> MigrateFromV4ToV5()
        {
            return providers.Select(provider => provider.MigrateFromV4ToV5()).ToList();
        }
    }
    
    extension(Provider provider)
    {
        public AIStudio.Settings.Provider MigrateFromV4ToV5() => new()
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
}