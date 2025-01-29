namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// The mapping between an AuthField and the field name in the authentication request.
/// </summary>
/// <param name="AuthField">The AuthField that is mapped to the field name.</param>
/// <param name="FieldName">The field name in the authentication request.</param>
public record AuthFieldMapping(AuthField AuthField, string FieldName);