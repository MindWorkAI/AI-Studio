using System.Text.Json;

using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Assistants.ModelComparison;
using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Settings.DataModel.PreviousModels;
using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.DataBackup;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// The types which AI Studio reads or writes as JSON on their own, for the tests which compare the contracts of
/// System.Text.Json.
/// </summary>
/// <remarks>
/// Only the outermost types belong here: whatever they hold gets checked along with them. Add a type whenever
/// AI Studio starts to store a new kind of file or to exchange a new kind of message, with the options it uses
/// for them. What AI Studio sends is listed apart from what it receives: only what it reads has to keep the
/// defaults of a new object.
/// </remarks>
public static partial class JsonTestRoots
{
    /// <summary>
    /// The data AI Studio keeps on the disk of the user.
    /// </summary>
    public static IEnumerable<TestCaseData> Stored()
    {
        TestHost.PrepareStaticState();

        // Chats, in the workspaces, among the temporary chats, and in chat archives:
        yield return Case(new(typeof(ChatThread), WorkspaceBehaviour.JSON_OPTIONS));

        // The settings, and their older versions, which the migrations still read:
        yield return Case(new(typeof(Data), SettingsManager.JSON_OPTIONS));
        yield return Case(new(typeof(DataV1V3), SettingsManager.JSON_OPTIONS));
        yield return Case(new(typeof(DataV4), SettingsManager.JSON_OPTIONS));
        yield return Case(new(typeof(DataV5), SettingsManager.JSON_OPTIONS));

        //
        // The values of managed settings, which get written and read on their own, as the type of their
        // setting. That type is often an interface, such as ISet<string>, which the settings themselves
        // never use:
        //
        foreach (var valueType in ManagedSettingValueTypes())
            yield return Case(new(valueType, SettingsManager.JSON_OPTIONS));

        // The SWOT analysis, which a chat stores as the presentation of a message:
        yield return Case(new(typeof(SwotAnalysisResult), SwotAnalysisContentPresentation.JSON_OPTIONS), "read");
        yield return Case(new(typeof(SwotAnalysisResult), SwotAnalysisContentPresentation.WRITE_OPTIONS), "written");

        // Backups and their restore:
        yield return Case(new(typeof(DataBackupManifest), DataBackupFormat.JSON_OPTIONS));
        yield return Case(new(typeof(DataRestorePlan), DataBackupFormat.JSON_OPTIONS));
        yield return Case(new(typeof(DataRestoreReport), DataBackupFormat.JSON_OPTIONS));

        // The text around a passage which the provenance store keeps:
        yield return Case(new(typeof(IReadOnlyList<string>), SqliteProvenanceStoreClientImplementation.JSON_OPTIONS));
        yield return Case(new(typeof(List<string>), SqliteProvenanceStoreClientImplementation.JSON_OPTIONS));

        // The preparations of the BeyondFacts Lab, their forms, materials, work states, results, and research, and the preparation selected last:
        yield return Case(new(typeof(BflProject), BeyondFactsLabJson.PERSISTENCE));
        yield return Case(new(typeof(BflStepFormData), BeyondFactsLabJson.PERSISTENCE));
        yield return Case(new(typeof(BflMaterial), BeyondFactsLabJson.PERSISTENCE));
        yield return Case(new(typeof(BflWorkState), BeyondFactsLabJson.PERSISTENCE));
        yield return Case(new(typeof(BflResult), BeyondFactsLabJson.PERSISTENCE));
        yield return Case(new(typeof(BflResearch), BeyondFactsLabJson.PERSISTENCE));
        yield return Case(new(typeof(Guid?), BeyondFactsLabJson.PERSISTENCE), "selected preparation");

        // The presets and the stored results of the Model Comparison assistant:
        yield return Case(new(typeof(ModelComparisonPreset), ModelComparisonJson.PERSISTENCE));
        yield return Case(new(typeof(ModelComparisonRecord), ModelComparisonJson.PERSISTENCE));
        yield return Case(new(typeof(ModelComparisonSeries), ModelComparisonJson.PERSISTENCE));
    }

    private static TestCaseData Case(JsonTestRoot root, string? purpose = null)
    {
        //
        // The same System.Text.Json does with the first call of Serialize or Deserialize. Only then, the
        // options hand out contracts:
        //
        root.Options.MakeReadOnly(populateMissingResolver: true);
        return new TestCaseData(root).SetArgDisplayNames(purpose is null ? root.ToString() : $"{root}, {purpose}");
    }

    /// <summary>
    /// Names the case after its type together with the last part of the namespace, which tells apart types of the
    /// same name, e.g., the lists of models of two providers.
    /// </summary>
    private static TestCaseData CaseWithNamespace(Type type, JsonSerializerOptions options)
    {
        var typeNamespace = type.Namespace!;
        return Case(new(type, options), typeNamespace[(typeNamespace.LastIndexOf('.') + 1)..]);
    }

    /// <summary>
    /// Returns the classes and structs of AI Studio which are the given type.
    /// </summary>
    /// <remarks>
    /// A converter which writes a value as the type it is of meets these types without any property naming them.
    /// Listing them all here keeps a new one from going unchecked.
    /// </remarks>
    private static IEnumerable<Type> ImplementationsOf(Type type) => typeof(Program).Assembly.GetTypes()
        .Where(candidate => candidate is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } && type.IsAssignableFrom(candidate))
        .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal);

    private static IEnumerable<Type> ManagedSettingValueTypes()
    {
        //
        // Creating the settings registers every managed setting, just like the start of the app does:
        //
        _ = new Data();
        return ManagedConfiguration.RegisteredSettings
            .Select(setting => setting.GetType().GetGenericArguments()[1])
            .Distinct()
            .OrderBy(JsonContractDescription.TypeName, StringComparer.Ordinal);
    }
}