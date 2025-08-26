using System.Numerics;

namespace AIStudio.Tools;

/// <summary>
/// Represents the result of an increment operation. It encapsulates whether the operation
/// was successful and the increased value.
/// </summary>
/// <typeparam name="TOut">The type of the incremented value, constrained to implement the IBinaryInteger interface.</typeparam>
public sealed record IncrementResult<TOut>(bool Success, TOut UpdatedValue) where TOut : IBinaryInteger<TOut>;