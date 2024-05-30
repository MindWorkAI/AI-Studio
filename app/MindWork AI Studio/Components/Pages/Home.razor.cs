using AIStudio.Components.Blocks;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Pages;

public partial class Home : ComponentBase
{
    private static readonly TextItem[] ITEMS_ADVANTAGES =
    [
        new TextItem("Free of charge", "The app is free to use, both for personal and commercial purposes."),
        new TextItem("Independence", "Users are not tied to any single provider. The initial version supports OpenAI models (like GPT-4o, GPT-4, GPT-4 Turbo, etc.). Future versions will support other providers such as Mistral or Google Gemini."),
        new TextItem("Unrestricted usage", "Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API."),
        new TextItem("Cost-effective", "You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit."),
        new TextItem("Privacy", "The data entered into the app is not used for training by the providers since we are using the provider's API."),
        new TextItem("Flexibility", "Choose the provider and model best suited for your current task."),
        new TextItem("No bloatware", "The app requires minimal storage for installation and operates with low memory usage. Additionally, it has a minimal impact on system resources, which is beneficial for battery life."),
    ];

    private static readonly TextItem[] ITEMS_VISION =
    [
        new TextItem("Meet your needs", "Whatever your job or task is, MindWork AI Studio aims to meet your needs: whether you're a project manager, scientist, artist, author, software developer, or game developer."),
        new TextItem("One stop shop", "The app will strive to fulfill all your AI needs: text-generation AI (LLM), image-generation AI, audio-generation AI (text-to-speech, potentially even text-to-music), and audio input (transcription, dictation). When there's a provider and an API available, we'll try to integrate it."),
        new TextItem("Local file system", "When you wish, we aim to integrate your local system. Local documents could be incorporated using Retrieval-Augmented Generation (RAG), and we could directly save AI-generated content to your file system."),
        new TextItem("Local AI systems", "Want to use AI systems locally and offline? We aim to make that possible too."),
        new TextItem("Your AI systems", "Prefer to run your AI systems with providers like replicate.com? We plan to support that!"),
        new TextItem("Assistants", "We aim to integrate specialized user interfaces as assistants. For example, a UI specifically for writing emails, or one designed for translating and correcting text, and more."),
    ];
}