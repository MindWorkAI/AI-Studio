using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Vision : ComponentBase
{
    private static readonly TextItem[] ITEMS_VISION =
    [
        new("Meet your needs", "Whatever your job or task is, MindWork AI Studio aims to meet your needs: whether you're a project manager, scientist, artist, author, software developer, or game developer."),
        new("Integrating your data", "You'll be able to integrate your data into AI Studio, like your PDF or Office files, or your Markdown notes."),
        new("Integration of enterprise data", "It will soon be possible to integrate data from the corporate network using a specified interface (External Retrieval Interface, ERI for short). This will likely require development work by the organization in question."),
        new("Useful assistants", "We'll develop more assistants for everyday tasks."),
        new("Writing mode", "We're integrating a writing mode to help you create extensive works, like comprehensive project proposals, tenders, or your next fantasy novel."),
        new("Specific requirements", "Want an assistant that suits your specific needs? We aim to offer a plugin architecture so organizations and enthusiasts can implement such ideas."),
        new("Voice control", "You'll interact with the AI systems using your voice. To achieve this, we want to integrate voice input (speech-to-text) and output (text-to-speech). However, later on, it should also have a natural conversation flow, i.e., seamless conversation."),
        new("Content creation", "There will be an interface for AI Studio to create content in other apps. You could, for example, create blog posts directly on the target platform or add entries to an internal knowledge management tool. This requires development work by the tool developers."),
        new("Email monitoring", "You can connect your email inboxes with AI Studio. The AI will read your emails and notify you of important events. You'll also be able to access knowledge from your emails in your chats."),
        new("Browser usage", "We're working on offering AI Studio features in your browser via a plugin, allowing, e.g., for spell-checking or text rewriting directly in the browser."),
    ];
}