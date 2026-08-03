namespace AIStudio.Tools;

/// <summary>
/// Marks JSON serializer options whose exact byte output is hashed into stored data.
/// </summary>
/// <remarks>
/// Options carrying this attribute are frozen: changing how they serialize changes every hash ever
/// computed with them, which turns previously valid stored data into data that fails its integrity
/// check. Because that failure looks like corruption rather than like a code change, the rule
/// MWAIS0010 requires such options to be written out in full at their own declaration and to carry no
/// converters. Sharing a factory with non-hashed options is what allows a change meant for one of them
/// to reach the other unnoticed.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CanonicalJsonConfigurationAttribute : Attribute;