namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// The response to an authentication request.
/// </summary>
/// <param name="Success">True, when the authentication was successful.</param>
/// <param name="Token">The token to use for further requests.</param>
/// <param name="Message">When the authentication was not successful, this contains the reason.</param>
public readonly record struct AuthResponse(bool Success, string? Token, string? Message);