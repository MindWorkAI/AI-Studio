namespace AIStudio.Tools;

/// <summary>
/// Pins the JSON shape of a type whose serialized form is hashed into stored data.
/// </summary>
/// <remarks>
/// A Roslyn analyzer only ever sees the current code, so it cannot notice that a property was added
/// yesterday. Declaring the expected shape here gives it something to compare against: rule MWAIS0011
/// derives a signature from the properties, their JSON names, their types, and their ignore conditions,
/// and fails the build when it no longer matches. The point is not the value itself but the moment it
/// forces — updating it is the step where somebody has to decide whether existing stored data may stop
/// being readable, and the changed value makes that decision visible in the diff.
/// Only types whose JSON is hashed directly carry this attribute. The artifact envelopes around them do
/// not, because the parts of them that reach a hash are named one by one in
/// <c>VisualBriefingPayloadHash</c>, where changing a type breaks the build on its own.
/// Attributes that affect reading rather than writing, such as <c>JsonRequired</c>, are not part of the
/// signature: they cannot change the bytes that were hashed.
/// </remarks>
/// <param name="signature">The expected shape signature, reported by MWAIS0011 whenever it changes.</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CanonicalJsonShapeAttribute(string signature) : Attribute
{
    /// <summary>
    /// Gets the expected shape signature.
    /// </summary>
    public string Signature { get; } = signature;
}