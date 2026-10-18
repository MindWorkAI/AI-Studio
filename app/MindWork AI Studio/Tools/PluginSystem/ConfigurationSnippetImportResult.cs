namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// A configuration object read from a pasted configuration snippet, ready to become one of the user's own.
/// </summary>
/// <param name="ConfigurationObject">The configuration object, with an ID of its own and belonging to no configuration plugin.</param>
/// <param name="Secret">The decrypted API key, token, or password which the snippet brought along, or an empty string.</param>
/// <param name="Hints">What the user should know before adding the object, each as a sentence of its own.</param>
/// <param name="AttachmentsToRelink">The file attachments of a chat template which are not on this computer, as the snippet names them.</param>
public sealed record ConfigurationSnippetImportResult(IConfigurationObject ConfigurationObject, string Secret, IReadOnlyList<string> Hints, IReadOnlyList<string> AttachmentsToRelink);