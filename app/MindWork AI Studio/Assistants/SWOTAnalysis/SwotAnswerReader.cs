namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Reads the answer of a model into a result.
/// </summary>
/// <typeparam name="TResult">The result.</typeparam>
/// <param name="answer">The answer of the model.</param>
/// <param name="result">The result, or an empty one when reading failed.</param>
/// <param name="issue">Why reading failed, written for the model; empty on success.</param>
/// <returns>Whether the answer could be read.</returns>
internal delegate bool SwotAnswerReader<TResult>(string answer, out TResult result, out string issue);