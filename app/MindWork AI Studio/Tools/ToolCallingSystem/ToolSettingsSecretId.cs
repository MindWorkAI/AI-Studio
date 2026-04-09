using AIStudio.Tools;

namespace AIStudio.Tools.ToolCallingSystem;

internal sealed record ToolSettingsSecretId(string ToolId, string FieldName) : ISecretId
{
    public string SecretId => $"tool::{this.ToolId}";

    public string SecretName => this.FieldName;
}
