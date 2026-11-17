namespace AIStudio.Tools.Mathematics;

/// <summary>
/// A computation which the Mathematics tools refused or could not finish.
/// </summary>
/// <remarks>
/// The message goes to the model as the result of the tool, in English, like the other messages
/// about the arguments of a tool. It states what happened and, where the input was the cause, how
/// AngouriMath reads the input and how it is written instead.
/// </remarks>
/// <param name="message">What happened.</param>
public sealed class MathComputationException(string message) : Exception(message);