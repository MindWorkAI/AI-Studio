using AIStudio.Components;

using Microsoft.AspNetCore.Components;

using Changelog = AIStudio.Components.Changelog;

namespace AIStudio.Pages;

public partial class Home : MSGComponentBase
{
    [Inject]
    private HttpClient HttpClient { get; init; } = null!;
    
    private string LastChangeContent { get; set; } = string.Empty;
    
    private TextItem[] itemsAdvantages = [];
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.InitializeAdvantagesItems();
        
        // Read the last change content asynchronously
        // without blocking the UI thread:
        _ = this.ReadLastChangeAsync();
    }

    private void InitializeAdvantagesItems()
    {
        this.itemsAdvantages = [
            new(this.T("Free of charge"), this.T("The app is free to use, both for personal and commercial purposes.")),
            new(this.T("Independence"), this.T("You are not tied to any single provider. Instead, you might choose the provider that best suits your needs. Right now, we support OpenAI (GPT4o, o1, etc.), Mistral, Anthropic (Claude), Google Gemini, xAI (Grok), DeepSeek, Alibaba Cloud (Qwen), Hugging Face, and self-hosted models using vLLM, llama.cpp, ollama, LM Studio, Groq, or Fireworks. For scientists and employees of research institutions, we also support Helmholtz and GWDG AI services. These are available through federated logins like eduGAIN to all 18 Helmholtz Centers, the Max Planck Society, most German, and many international universities.")), 
            new(this.T("Assistants"), this.T("You just want to quickly translate a text? AI Studio has so-called assistants for such and other tasks. No prompting is necessary when working with these assistants.")),
            new(this.T("Unrestricted usage"), this.T("Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API.")),
            new(this.T("Cost-effective"), this.T("You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit.")),
            new(this.T("Privacy"), this.T("You can control which providers receive your data using the provider confidence settings. For example, you can set different protection levels for writing emails compared to general chats, etc. Additionally, most providers guarantee that they won't use your data to train new AI systems.")),
            new(this.T("Flexibility"), this.T("Choose the provider and model best suited for your current task.")),
            new(this.T("No bloatware"), this.T("The app requires minimal storage for installation and operates with low memory usage. Additionally, it has a minimal impact on system resources, which is beneficial for battery life.")),
        ];
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                this.InitializeAdvantagesItems();
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }

    #endregion

    private async Task ReadLastChangeAsync()
    {
        var latest = Changelog.LOGS.MaxBy(n => n.Build);
        using var response = await this.HttpClient.GetAsync($"changelog/{latest.Filename}");
        this.LastChangeContent = await response.Content.ReadAsStringAsync();
        
        await this.InvokeAsync(this.StateHasChanged);
    }
    
    private const string QUICK_START_GUIDE =
        """
        Ready to dive in and get started with MindWork AI Studio? This quick start guide will help you set up everything you need to start using the app.
        
        ## Step 1: Create an Account with OpenAI
        1. Go to [OpenAI's platform](https://platform.openai.com/).
        2. Click on "Sign up" and follow the instructions provided.
        
        ## Step 2: Obtain an API Key
        1. After creating your OpenAI account, ensure you have a project named "default".
        2. If you want, you can create a new project by clicking "Create project".
        3. Navigate to a project of your choice, then click on "API keys" in the left-hand navigation menu.
        4. You might need to validate your phone number. Click "Start verification" and follow the instructions.
        5. Once verified, click "Create new secret key" to generate a new API key for the selected project.
        6. Name your key something descriptive like "AI Studio Laptop."
        7. Copy the displayed secret key. Remember, you will not be able to see this key again.
        8. Store it in a password manager like [KeePassXC](https://keepassxc.org).
        9. **Important:** Treat your API key like a secret password. Anyone with access to your key can use OpenAI's systems **at your expense**. **Do not share it with anyone!**
        
        ## Step 3: Add OpenAI as a Provider in MindWork AI Studio
        1. Go to the settings section in MindWork AI Studio.
        2. Click "Add Provider" and select OpenAI as the provider.
        3. Paste your API key into the corresponding field.
        
        ## Step 4: Load OpenAI Models
        1. Ensure you have an internet connection and your API key is valid.
        2. Click "Reload" to retrieve a list of available OpenAI models.
        3. Select "gpt-4o" to use the latest model.
        4. Provide a name for this combination of provider, API key, and model. This is called the "instance name". For example, you can name it based on the usage context, such as "Personal OpenAI" or "Work OpenAI".
        
        ## Step 5: Save the Provider
        1. Click "Add" to save the provider.
        2. Depending on your operating system and settings, you may need to enter your login password. This is required to store and access your API key securely.
        
        ## Step 6: Add Funds to Your OpenAI Account
        To utilize OpenAI's services, you need to add funds to your account. It's best to use the prepaid method to avoid high bills and set your budget. Navigate to the "Billing" section. Here, you can see your current balance. Click on "Add to credit balance" to deposit an amount. A balance of $10 is sufficient to start.
        
        ## Step 7: Start Chatting with the AI
        1. Switch to the chat section in MindWork AI Studio.
        2. Select your provider from the dropdown menu at the top.
        3. Enter your question or message in the input field below to start your first chat with the AI.
        
        ## Additional Resources
        There are also video tutorials on how to obtain your OpenAI API key. One example is this video by Anders Jensen: [https://www.youtube.com/watch?v=OB99E7Y1cMA](https://www.youtube.com/watch?v=OB99E7Y1cMA).
        
        That's it! You're ready to explore and create with MindWork AI Studio. Enjoy your journey!                               
        """;
}