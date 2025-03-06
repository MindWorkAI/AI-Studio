using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ERIClient;

public abstract class ERIClientBase(string baseAddress) : IDisposable
{
    protected static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
        }
    };
    
    protected readonly HttpClient httpClient = new()
    {
        BaseAddress = new Uri(baseAddress),
    };
    
    protected string securityToken = string.Empty;
    
    #region Implementation of IDisposable

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    #endregion
}