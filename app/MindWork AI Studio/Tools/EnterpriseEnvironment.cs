namespace AIStudio.Tools;

public readonly record struct EnterpriseEnvironment(string ConfigurationServerUrl, Guid ConfigurationId)
{
    public bool IsActive => !string.IsNullOrEmpty(this.ConfigurationServerUrl) && this.ConfigurationId != Guid.Empty;
}