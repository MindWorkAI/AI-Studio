namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mathematics;

/// <summary>
/// What compute_symbolically does with an expression.
/// </summary>
public enum SymbolicOperation
{
    SIMPLIFY = 0,
    EXPAND,
    FACTOR,
    DIFFERENTIATE,
    INTEGRATE,
    LIMIT,
    SOLVE,
}