using System.Text.Json;

namespace AIStudio.Tools.ToolCallingSystem;

public interface IToolImplementation
{
    public string ImplementationKey { get; }

    public IReadOnlySet<string> SensitiveTraceArgumentNames { get; }

    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default);

    public string FormatTraceResult(string rawResult) => rawResult;
}
