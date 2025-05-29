using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class Vision : MSGComponentBase
{
    private TextItem[] itemsVision = [];

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.InitializeVisionItems();
    }

    private void InitializeVisionItems()
    {
        this.itemsVision =
        [
            new(T("Meet your needs"), T("Whatever your job or task is, MindWork AI Studio aims to meet your needs: whether you're a project manager, scientist, artist, author, software developer, or game developer.")),
            new(T("Integrating your data"), T("You'll be able to integrate your data into AI Studio, like your PDF or Office files, or your Markdown notes.")),
            new(T("Integration of enterprise data"), T("It will soon be possible to integrate data from the corporate network using a specified interface (External Retrieval Interface, ERI for short). This will likely require development work by the organization in question.")),
            new(T("Useful assistants"), T("We'll develop more assistants for everyday tasks.")),
            new(T("Writing mode"), T("We're integrating a writing mode to help you create extensive works, like comprehensive project proposals, tenders, or your next fantasy novel.")),
            new(T("Specific requirements"), T("Want an assistant that suits your specific needs? We aim to offer a plugin architecture so organizations and enthusiasts can implement such ideas.")),
            new(T("Voice control"), T("You'll interact with the AI systems using your voice. To achieve this, we want to integrate voice input (speech-to-text) and output (text-to-speech). However, later on, it should also have a natural conversation flow, i.e., seamless conversation.")),
            new(T("Content creation"), T("There will be an interface for AI Studio to create content in other apps. You could, for example, create blog posts directly on the target platform or add entries to an internal knowledge management tool. This requires development work by the tool developers.")),
            new(T("Email monitoring"), T("You can connect your email inboxes with AI Studio. The AI will read your emails and notify you of important events. You'll also be able to access knowledge from your emails in your chats.")),
            new(T("Browser usage"), T("We're working on offering AI Studio features in your browser via a plugin, allowing, e.g., for spell-checking or text rewriting directly in the browser.")),
        ];
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                this.InitializeVisionItems();
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }

    #endregion
}