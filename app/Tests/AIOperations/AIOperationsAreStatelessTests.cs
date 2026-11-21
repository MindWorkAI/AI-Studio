using System.Reflection;
using System.Runtime.CompilerServices;

using AIStudio.AIOperations;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks that every AI operation can serve several callers at once.
/// </summary>
/// <remarks>
/// The operations are singletons, so two chats with RAG run the same instance at the same time. Anything an
/// operation stored between calls, such as its provider or its answers, would leak from one call into the
/// other. Only the parameters of a primary constructor may be kept, which hold the services it depends on.
/// </remarks>
[TestFixture]
public sealed class AIOperationsAreStatelessTests
{
    [Test]
    public void TheOperationsAreFound()
    {
        Assert.That(FindOperationTypes().Select(type => type.Name), Is.SupersetOf(new[] { "TextContentCleaningOperation", "DataSourceChoiceOperation", "RetrievalContextValidationOperation", "PluginAuditOperation" }), "Without the operations, every other test here would pass while checking nothing.");
    }

    [Test]
    public void NoOperationHasWritableState()
    {
        foreach (var operationType in FindOperationTypes())
            Assert.That(FindWritableState(operationType), Is.Empty, $"The AI operation '{operationType.Name}' keeps state between calls; keep everything of a call in local variables.");
    }

    [Test]
    public void EveryOperationIsASingleton()
    {
        var programSource = ReadProgramSource();
        foreach (var operationType in FindOperationTypes())
            Assert.That(programSource, Does.Contain($"builder.Services.AddSingleton<{operationType.Name}>();"), $"The AI operation '{operationType.Name}' has to be registered as a singleton in Program.cs.");
    }

    [Test]
    public void EveryOperationReadsItsProviderSettings()
    {
        foreach (var operationType in FindOperationTypes())
        {
            var method = operationType.GetMethod("GetProviderSettings", BindingFlags.Public | BindingFlags.Static, [typeof(AIStudio.Settings.SettingsManager)]);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(AIOperationProviderSettings)), $"The AI operation '{operationType.Name}' needs a public static GetProviderSettings(SettingsManager), see IAIOperation.");
        }
    }

    /// <summary>
    /// Checks the check itself, since the operations alone would let it pass while it finds nothing.
    /// </summary>
    [Test]
    public void TheCheckFindsWritableState()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FindWritableState(typeof(StatefulExample)), Is.EquivalentTo(new[] { "StatefulExample.counter", "StatefulExample.Instances", "StatefulExample.Answer", "StatefulExample.Model" }));
            Assert.That(FindWritableState(typeof(StatelessExample)), Is.Empty, "Constants, read-only fields, getters, and the parameters of a primary constructor are no state.");
        });
    }

    private static IEnumerable<Type> FindOperationTypes() => typeof(IAIOperation<,>).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IAIOperation<,>)));

    /// <summary>
    /// Lists the fields and properties of a type and its base types which could keep state between calls.
    /// </summary>
    private static List<string> FindWritableState(Type type)
    {
        const BindingFlags ALL_DECLARED = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var writable = new List<string>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(ALL_DECLARED))
            {
                if (field.IsLiteral || field.IsInitOnly)
                    continue;

                // The backing field of a property, which the properties below cover:
                if (field.IsDefined(typeof(CompilerGeneratedAttribute)) && field.Name.EndsWith("k__BackingField", StringComparison.Ordinal))
                    continue;

                // A parameter of a primary constructor, which the compiler keeps as a field named <name>P:
                if (field.Name.StartsWith('<') && field.Name.EndsWith(">P", StringComparison.Ordinal))
                    continue;

                writable.Add($"{current.Name}.{field.Name}");
            }

            foreach (var property in current.GetProperties(ALL_DECLARED))
                if (property.SetMethod is not null)
                    writable.Add($"{current.Name}.{property.Name}");
        }

        return writable;
    }

    private static string ReadProgramSource([CallerFilePath] string sourceFilePath = "") => File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "MindWork AI Studio", "Program.cs"));

    // ReSharper disable UnusedMember.Local
    private sealed class StatefulExample
    {
        private int counter;

        public StatefulExample() => Instances++;

        private static int Instances { get; set; }

        public string Answer { get; set; } = string.Empty;

        public string Model { get; init; } = string.Empty;

        public int Count() => ++this.counter + Instances;
    }

    private sealed class StatelessExample(string name)
    {
        private const int LIMIT = 3;
        private static readonly string PREFIX = "prefix";
        private readonly int limit = LIMIT;

        public string Name => $"{PREFIX}{name}{this.limit}";

        public int Limit { get; } = LIMIT;
    }
    // ReSharper restore UnusedMember.Local
}
