using AIStudio.Chat;
using AIStudio.Tools.Imports;

namespace AIStudio.Tools.Media;

/// <summary>Pending media results waiting for one concrete UI target.</summary>
public sealed record MediaImportDelivery
{
    public required ImportTarget Target { get; init; }

    public IReadOnlyList<FileAttachment> Attachments { get; init; } = [];

    public string? Text { get; init; }

    public bool IsEmpty => this.Attachments.Count is 0 && this.Text is null;
}