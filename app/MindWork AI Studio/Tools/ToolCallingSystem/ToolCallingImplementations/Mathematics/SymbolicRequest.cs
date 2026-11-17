using AngouriMath.Core;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

/// <summary>
/// The arguments of a call of compute_symbolically, checked against the operation, but not yet read as mathematics.
/// </summary>
/// <param name="Operation">What to do.</param>
/// <param name="Expression">The expression, as the model wrote it.</param>
/// <param name="Variable">The variable to differentiate, integrate, approach, or solve for, or null to take the only one.</param>
/// <param name="Order">How often to differentiate.</param>
/// <param name="LowerBound">The lower bound of a definite integral, or null for an indefinite one.</param>
/// <param name="UpperBound">The upper bound of a definite integral, or null for an indefinite one.</param>
/// <param name="Approach">The point a limit approaches, or null for any other operation.</param>
/// <param name="Direction">From where the limit approaches.</param>
public sealed record SymbolicRequest(SymbolicOperation Operation, string Expression, string? Variable, int Order, string? LowerBound, string? UpperBound, string? Approach, ApproachFrom Direction);