namespace AIStudio.Tools;

/// <summary>
/// Separates malformed JSON from a response which parsed but violated its CLR contract.
/// </summary>
internal enum StructuredResponseFailureKind
{
    JSON_INVALID,
    CONTRACT_INVALID,
}