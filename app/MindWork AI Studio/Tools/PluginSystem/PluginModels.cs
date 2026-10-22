using AIStudio.Models.Plugins;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// A plugin which tells AI Studio about models it does not know, or knows wrongly.
/// </summary>
/// <remarks>
/// Organizations run models nobody outside them has ever heard of: their own fine-tunes, a model
/// behind an internal name, an engine an operator configured differently from the model card. Until
/// now the only way to tell AI Studio about those was the expert settings of each configured
/// provider, one person and one provider at a time.
///
/// A model plugin describes, and that is all it does. It names no endpoint, carries no key, runs no
/// code of its own and reaches nothing over the network, which is why it needs none of the checks an
/// assistant plugin goes through. Where it was deployed is what says how much it may claim, exactly
/// as for every other kind of plugin.
/// </remarks>
public sealed class PluginModels(bool isInternal, LuaState state, PluginType type) : PluginBase(isInternal, state, type), ILivePluginContentSource
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(PluginModels));

    private readonly List<ModelDeclaration> declarations = [];

    /// <summary>
    /// The models this plugin declares.
    /// </summary>
    public IReadOnlyList<ModelDeclaration> Declarations => this.declarations;

    /// <inheritdoc />
    public int Priority { get; } = ReadPriority(state);

    /// <summary>
    /// Reads the MODELS table of the plugin.
    /// </summary>
    /// <remarks>
    /// An entry which cannot be read is reported and skipped, and the rest of the table still
    /// counts. A single mistyped capability in the twentieth entry must not take the nineteen
    /// working ones with it -- the plugin would then be silently doing nothing at all.
    /// </remarks>
    public void TryLoad()
    {
        if (!this.State.Environment["MODELS"].TryRead<LuaTable>(out var modelsTable))
        {
            this.PluginIssues.Add(TB("The table MODELS does not exist or is using an invalid syntax."));
            return;
        }

        for (var i = 1; i <= modelsTable.ArrayLength; i++)
        {
            if (!modelsTable[i].TryRead<LuaTable>(out var modelTable))
            {
                LOG.LogWarning("The table 'MODELS' entry at index {Index} is not a valid table (model plugin id: {PluginId}).", i, this.Id);
                continue;
            }

            if (ModelDeclaration.TryParse(i, modelTable, this.Id, this.Name, LOG, out var declaration))
                this.declarations.Add(declaration);
            else
                LOG.LogWarning("The table 'MODELS' entry at index {Index} does not contain a valid model declaration and is ignored (model plugin id: {PluginId}).", i, this.Id);
        }

        if (this.declarations.Count is 0)
            LOG.LogWarning("The model plugin '{PluginId}' declares no model AI Studio could read. It has no effect.", this.Id);
    }

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginModels).Namespace, nameof(PluginModels));

    private static int ReadPriority(LuaState state) => state.Environment["PRIORITY"].TryRead<int>(out var priority) ? priority : 0;
}