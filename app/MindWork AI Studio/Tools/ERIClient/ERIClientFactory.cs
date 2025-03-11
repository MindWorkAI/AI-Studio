using AIStudio.Assistants.ERI;
using AIStudio.Settings;

namespace AIStudio.Tools.ERIClient;

public static class ERIClientFactory
{
    public static IERIClient? Get(ERIVersion version, IERIDataSource dataSource) => version switch
    {
        ERIVersion.V1 => new ERIClientV1(dataSource),
        
        _ => null
    };
}