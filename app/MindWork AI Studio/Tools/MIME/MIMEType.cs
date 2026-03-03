namespace AIStudio.Tools.MIME;

public record MIMEType
{
    public required ISubtype Type { get; init; }
    
    public required string TextRepresentation { get; init; }

    #region Overrides of Object

    public override string ToString() => this.TextRepresentation;

    #endregion
    
    public static implicit operator string(MIMEType mimeType) => mimeType.TextRepresentation;
}