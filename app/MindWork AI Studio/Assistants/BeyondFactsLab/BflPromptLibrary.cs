using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Reads the prompts and the knowledge base of the BeyondFacts Lab, which come with AI Studio.
/// </summary>
/// <remarks>
/// The files are German Markdown below Assistants/BeyondFactsLab, embedded into the app. A development build reads
/// them from the disk, so that a changed prompt shows in the next request without building again; see AppResources.
/// </remarks>
public static class BflPromptLibrary
{
    /// <summary>
    /// The folder of the lab, relative to the project folder.
    /// </summary>
    private const string FOLDER = "Assistants/BeyondFactsLab";

    /// <summary>
    /// The prompt every chat of the lab starts with: role, principles, and the places of the other parts.
    /// </summary>
    public const string CORE_PATH = FOLDER + "/Prompts/core.md";

    /// <summary>
    /// The guide by Lara Wüster (TUM), which every chat of the lab keeps in its context.
    /// </summary>
    public const string KNOWLEDGE_PATH = FOLDER + "/Knowledge/beyondfacts-paper.md";

    /// <summary>
    /// The instruction to address the person formally.
    /// </summary>
    private const string ADDRESS_FORMAL_PATH = FOLDER + "/Prompts/snippets/address-formal.md";

    /// <summary>
    /// The instruction to address the person informally.
    /// </summary>
    private const string ADDRESS_INFORMAL_PATH = FOLDER + "/Prompts/snippets/address-informal.md";

    /// <summary>
    /// Gets the prompt of a step, named like the folder of the step.
    /// </summary>
    /// <param name="step">The step.</param>
    public static string StepPath(BflStep step) => $"{FOLDER}/Prompts/steps/{step.FolderName}.md";

    /// <summary>
    /// Gets the instruction for a form of address.
    /// </summary>
    /// <param name="addressForm">The form of address.</param>
    public static string AddressPath(AddressForm addressForm) => addressForm is AddressForm.INFORMAL ? ADDRESS_INFORMAL_PATH : ADDRESS_FORMAL_PATH;

    /// <summary>
    /// Gets every file of the lab's prompts, e.g., to check that the app comes with all of them.
    /// </summary>
    public static IReadOnlyList<string> AllPaths =>
    [
        CORE_PATH,
        KNOWLEDGE_PATH,
        ..Enum.GetValues<AddressForm>().Select(AddressPath).Distinct(),
        ..BflStep.All.Select(StepPath)
    ];

    /// <summary>
    /// Reads a file of the lab's prompts.
    /// </summary>
    /// <param name="path">The file, one of the paths of this class.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text of the file.</returns>
    /// <exception cref="FileNotFoundException">The app comes without the file, or the file is empty.</exception>
    public static async Task<string> ReadAsync(string path, CancellationToken token = default)
    {
        var text = await AppResources.ReadTextAsync(path, token);
        if (string.IsNullOrWhiteSpace(text))
            throw new FileNotFoundException("The BeyondFacts Lab misses one of its prompts.", path);

        return text;
    }
}