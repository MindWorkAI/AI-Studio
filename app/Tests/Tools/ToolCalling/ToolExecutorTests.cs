using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what the tool executor hands to a tool and what it hands back to the loop.
/// </summary>
/// <remarks>
/// What a result demands of the chat has to reach the loop, which tightens the chat with it: a
/// result from a data source for self-hosted providers only that got lost on the way would let the
/// next message go to a cloud provider. A call which brought nothing in must demand nothing.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolExecutorTests : ToolRegistryTestBase
{
    [Test]
    public async Task WhatAResultDemandsReachesTheLoop()
    {
        var tool = new TestTool(Definition(), execute: _ => new ToolExecutionResult
        {
            TextContent = "A passage from the handbook.",
            RequiredProviderConfidence = ConfidenceLevel.HIGH,
            RequiredDataSecurity = DataSourceSecurity.SELF_HOSTED,
        });

        var (_, _, requiredProviderConfidence, requiredDataSecurity, _) = await this.Execute(tool, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(requiredDataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED));
            Assert.That(requiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));
        });
    }

    [Test]
    public async Task TheToolSeesTheChatOfTheCall()
    {
        ChatThread? seenThread = null;
        var tool = new TestTool(Definition(), execute: context =>
        {
            seenThread = context.ChatThread;
            return new ToolExecutionResult();
        });
        var thread = new ChatThread();

        await this.Execute(tool, thread);

        Assert.That(seenThread, Is.SameAs(thread), "Semantic Search searches the data sources picked for this very chat.");
    }

    [Test]
    public async Task ABlockedCallDemandsNothing()
    {
        var tool = new TestTool(Definition(), execute: _ => throw new ToolExecutionBlockedException("The data source is not available to this provider."));

        var (_, trace, _, requiredDataSecurity, _) = await this.Execute(tool, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(trace.Status, Is.EqualTo(ToolInvocationTraceStatus.BLOCKED));
            Assert.That(requiredDataSecurity, Is.EqualTo(DataSourceSecurity.NOT_SPECIFIED), "Nothing reached the model, so there is nothing the chat has to keep.");
        });
    }

    [Test]
    public async Task AFailedCallDemandsNothing()
    {
        var tool = new TestTool(Definition(), execute: _ => throw new InvalidOperationException("The index could not be read."));

        var (_, trace, _, requiredDataSecurity, _) = await this.Execute(tool, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(trace.Status, Is.EqualTo(ToolInvocationTraceStatus.ERROR));
            Assert.That(requiredDataSecurity, Is.EqualTo(DataSourceSecurity.NOT_SPECIFIED), "Nothing reached the model, so there is nothing the chat has to keep.");
        });
    }

    private Task<(string Content, ToolInvocationTrace Trace, ConfidenceLevel RequiredProviderConfidence, DataSourceSecurity RequiredDataSecurity, IReadOnlyList<AIStudio.Tools.Source> Sources)> Execute(TestTool tool, ChatThread thread)
    {
        var executor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance);
        return executor.ExecuteAsync("call-1", TOOL_ID, "{}", [(tool.GetDefinition(), tool)], new NoProvider(), thread, order: 1);
    }
}