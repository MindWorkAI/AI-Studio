using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Settings;

namespace AIStudio.Tools.ERIClient;

public abstract class ERIClientBase(IERIDataSource dataSource) : IDisposable
{
    protected readonly IERIDataSource DataSource = dataSource;

    /// <summary>
    /// How AI Studio writes its requests to an ERI server and reads the answers.
    /// </summary>
    /// <remarks>
    /// An answer which names a field twice is refused, also when the two names differ only in case:
    /// AI Studio would follow one of the values, such as the provider types a data source allows, while
    /// the server may have meant the other.
    /// </remarks>
    internal static readonly JsonSerializerOptions JSON_OPTIONS = new JsonSerializerOptions
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
        },
        TypeInfoResolver = ERIClientJsonContext.Default,
    }.AsReadOnly();
    
    protected readonly HttpClient HttpClient = ExternalHttpClientTimeout.CreateHttpClient(new Uri($"{dataSource.Hostname}:{dataSource.Port}"), ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
    
    protected string SecurityToken = string.Empty;
    
    #region Implementation of IDisposable

    public void Dispose()
    {
        this.HttpClient.Dispose();
    }

    #endregion
}