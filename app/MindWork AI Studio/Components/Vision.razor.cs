using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Vision : ComponentBase
{
    private static readonly TextItem[] ITEMS_VISION =
    [
        new("Meet your needs", "Whatever your job or task is, MindWork AI Studio aims to meet your needs: whether you're a project manager, scientist, artist, author, software developer, or game developer."),
        new("One stop shop", "The app will strive to fulfill all your AI needs: text-generation AI (LLM), image-generation AI, audio-generation AI (text-to-speech, potentially even text-to-music), and audio input (transcription, dictation). When there's a provider and an API available, we'll try to integrate it."),
        new("Local file system", "When you wish, we aim to integrate your local system. Local documents could be incorporated using Retrieval-Augmented Generation (RAG), and we could directly save AI-generated content to your file system."),
        new("Local AI systems", "Want to use AI systems locally and offline? We aim to make that possible too."),
        new("Your AI systems", "Prefer to run your AI systems with providers like replicate.com? We plan to support that!"),
        new("Assistants", "We aim to integrate specialized user interfaces as assistants. For example, a UI specifically for writing emails, or one designed for translating and correcting text, and more."),
    ];
}