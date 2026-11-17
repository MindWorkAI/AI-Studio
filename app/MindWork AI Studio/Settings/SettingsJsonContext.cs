using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Settings.DataModel.PreviousModels;

using EMailWritingStyles = AIStudio.Assistants.EMail.WritingStyles;
using ERIContentType = AIStudio.Tools.ERIClient.DataModel.ContentType;
using ProviderV4 = AIStudio.Settings.DataModel.PreviousModels.Provider;
using RewriteImproveWritingStyles = AIStudio.Assistants.RewriteImprove.WritingStyles;

namespace AIStudio.Settings;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the settings, built at compile time.
/// </summary>
/// <remarks>
/// Besides the settings and their older versions, which the migrations still read, the values of managed
/// settings get written and read on their own, as the type of their setting. That type is often an interface,
/// such as ISet&lt;string&gt;, which the settings themselves never use. A test fails for a managed setting
/// whose type is missing here.<br/><br/>
///
/// Two types with the same name need a name of their own for their contract, else the generator builds
/// only the first of them and warns with SYSLIB1031.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Data))]
[JsonSerializable(typeof(DataV1V3))]
[JsonSerializable(typeof(DataV4))]
[JsonSerializable(typeof(DataV5))]
[JsonSerializable(typeof(ISet<string>))]
[JsonSerializable(typeof(ISet<PreviewFeatures>))]
[JsonSerializable(typeof(ISet<LLMProviders>))]
[JsonSerializable(typeof(ISet<ConfigurableAssistant>))]
[JsonSerializable(typeof(IList<string>))]
[JsonSerializable(typeof(IList<DataAssistantPluginEnterpriseApproval>))]
[JsonSerializable(typeof(IDictionary<string, string>))]
[JsonSerializable(typeof(ProviderV4), TypeInfoPropertyName = "ProviderV4")]
[JsonSerializable(typeof(List<ProviderV4>), TypeInfoPropertyName = "ListProviderV4")]
[JsonSerializable(typeof(EMailWritingStyles), TypeInfoPropertyName = "EMailWritingStyles")]
[JsonSerializable(typeof(RewriteImproveWritingStyles), TypeInfoPropertyName = "RewriteImproveWritingStyles")]
[JsonSerializable(typeof(ERIContentType), TypeInfoPropertyName = "ERIContentType")]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;