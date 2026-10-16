namespace AIStudio.Tools;

/// <summary>
/// What one receiver says to a question on the message bus, cf. MessageBus.SendMessageUseFirstResult.
/// </summary>
/// <remarks>
/// A bare result cannot tell an answer apart from "this is not my question". Most receivers are components
/// which never narrow down their events, so every question reaches them, and the default they return looks
/// like an answer: for a bool, it is false. Such a false used to drown out the chat, the only receiver which
/// knows whether it has unsaved changes, and the warning about them appeared only by chance. That is why a
/// receiver states that it answers, and every other one returns NONE.
/// </remarks>
/// <param name="IsAnswered">True, when the receiver handles the question.</param>
/// <param name="Value">The answer. It means nothing unless IsAnswered is true.</param>
/// <typeparam name="TResult">The type of the answer.</typeparam>
public readonly record struct MessageBusAnswer<TResult>(bool IsAnswered, TResult? Value)
{
    /// <summary>
    /// The receiver does not handle this question.
    /// </summary>
    public static readonly MessageBusAnswer<TResult> NONE = new(false, default);
}