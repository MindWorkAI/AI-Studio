using System.Net.Http.Headers;

namespace AIStudio.Tools;

public readonly record struct EnterpriseEnvironment(string ConfigurationServerUrl, Guid ConfigurationId, string Source, string SourceDetail, string Slot, EntityTagHeaderValue? ETag)
{
    public bool IsActive => !string.IsNullOrWhiteSpace(this.ConfigurationServerUrl) && this.ConfigurationId != Guid.Empty;
}