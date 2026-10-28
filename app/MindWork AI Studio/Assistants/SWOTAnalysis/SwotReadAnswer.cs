namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// What reading an answer of a model brought, after a repair round if one was needed.
/// </summary>
/// <typeparam name="TResult">The result.</typeparam>
/// <param name="Result">The result, or null when no answer could be read.</param>
/// <param name="Issue">Why the last answer could not be read; empty when one could.</param>
internal sealed record SwotReadAnswer<TResult>(TResult? Result, string Issue) where TResult : class;