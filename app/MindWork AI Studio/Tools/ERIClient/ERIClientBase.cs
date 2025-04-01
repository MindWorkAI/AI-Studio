using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Settings;

namespace AIStudio.Tools.ERIClient;

public abstract class ERIClientBase(IERIDataSource dataSource) : IDisposable
{
    protected readonly IERIDataSource dataSource = dataSource;
    
    protected static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
        }
    };
    
    protected readonly HttpClient httpClient = new()
    {
        BaseAddress = new Uri($"{dataSource.Hostname}:{dataSource.Port}"),
    };
    
    protected string securityToken = string.Empty;
    
    #region Implementation of IDisposable

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    #endregion
}