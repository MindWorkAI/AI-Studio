using AIStudio.Components.Blocks;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Pages;

public partial class Home : ComponentBase
{
    private static readonly TextItem[] ITEMS =
    [
        new TextItem("Free of charge", "The app is free to use, both for personal and commercial purposes."),
        new TextItem("Independence", "Users are not tied to any single provider. The initial version supports OpenAI models (like GPT-4o, GPT-4, GPT-4 Turbo, etc.). Future versions will support other providers such as Mistral or Google Gemini."),
        new TextItem("Unrestricted usage", "Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API."),
        new TextItem("Cost-effective", "You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit."),
        new TextItem("Privacy", "The data entered into the app is not used for training by the providers since we are using the provider's API."),
        new TextItem("Flexibility", "Choose the provider and model best suited for your current task."),
    ];
}