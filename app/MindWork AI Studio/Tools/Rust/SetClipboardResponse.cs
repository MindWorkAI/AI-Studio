namespace AIStudio.Tools.Rust;

/// <summary>
/// The response from the set clipboard operation.
/// </summary>
/// <param name="Success">True, when the operation was successful.</param>
/// <param name="Issue">The issues, which occurred during the operation, empty when successful.</param>
public readonly record struct SetClipboardResponse(bool Success, string Issue);