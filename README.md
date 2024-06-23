# MindWork AI Studio
MindWork AI Studio is a desktop application available for macOS, Windows, and Linux. It provides a unified chat interface for Large Language Models (LLMs). You bring your own API key for the respective LLM provider to use the models. The API keys are securely stored by the operating system.

**Key advantages:**
- **Free of charge**: The app is free to use, both for personal and commercial purposes.
- **Independence**: Users are not tied to any single provider. The initial version supports OpenAI models (like GPT-4o, GPT-4, GPT-4 Turbo, etc.). Future versions will support other providers such as Mistral or Google Gemini.
- **Unrestricted usage**: Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API.
- **Cost-effective**: You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit.
- **Privacy**: The data entered into the app is not used for training by the providers since we are using the provider's API.
- **Flexibility**: Choose the provider and model best suited for your current task.
- **No bloatware**: The app requires minimal storage for installation and operates with low memory usage. Additionally, it has a minimal impact on system resources, which is beneficial for battery life.

**Ready to get started 🤩?** [Download the appropriate setup for your operating system here](https://github.com/MindWorkAI/AI-Studio/releases/latest).

## Support the Project
Thank you for using MindWork AI Studio and considering supporting its development 😀. Your support helps keep the project alive and ensures continuous improvements and new features.
We offer various ways you can support the project:
- **Monthly Support**: Choose from different tiers that offer a range of benefits, including having your name or company logo featured in the app, access to exclusive content, and more.
- **One-Time Contributions**: Make a one-time donation and have your name or company logo included in the app as a gesture of our gratitude.

For companies, sponsoring MindWork AI Studio is not only a way to support innovation but also a valuable opportunity for public relations and marketing. Your company's name and logo will be featured prominently, showcasing your commitment to using cutting-edge AI tools and enhancing your reputation as an innovative enterprise.
 
To view all available tiers and their specific perks, please visit our [GitHub Sponsors page](https://github.com/MindWorkAI/AI-Studio/blob/main/Sponsors.md).
Your support, whether big or small, keeps the wheels turning and is deeply appreciated ❤️.

## Planned Features
Here's an exciting look at some of the features we're planning to add to MindWork AI Studio in future releases:
- **More providers**: We plan to add support for additional LLM providers, such as Mistral and Google Gemini, giving you more options to choose from.
- **Persistent chats**: Your chats will be stored locally, allowing you to continue conversations at any time without starting from scratch.
- **Local LLMs**: We aim to support local LLMs, enabling you to use options like LM Studio, `ollama`, or `llama.cpp` for a more private and self-contained experience.
- **System prompts**: Integration of a system prompt library will allow you to control the behavior of the LLM with predefined prompts, ensuring consistency and efficiency.
- **Text replacement for better privacy**: Define keywords that will be replaced in your chats before sending content to the provider, enhancing your privacy.
- **Advanced interactions**: We're full of ideas for advanced interactions tailored for specific use cases, whether in a business context or for writers and other professionals.

Stay tuned for more updates and enhancements to make MindWork AI Studio even more powerful and versatile 🤩.

## Building
You just want to use the app? Then simply [download the appropriate setup for your operating system](https://github.com/MindWorkAI/AI-Studio/releases/latest). This chapter is intended for developers who want to modify and customize the code.

In order to build MindWork AI Studio from source instead of using the pre-built binaries, follow these steps:
1. Install the .NET 8 SDK.
2. Install the Rust compiler.
3. Install NuShell. This shell works on all operating systems and is required because the build script is written in NuShell.
4. Clone the repository.
5. Open a terminal with NuShell.
6. Navigate to the `/app/MindWork AI Studio` directory within the repository.
7. To build the current version, run `nu build.nu publish`.
    - This will build the app for the current operating system, for both x64 (Intel, AMD) and ARM64 (e.g., Apple Silicon, Raspberry Pi).
    - The setup program will be located in `runtime/target/release/bundle` afterward.
8. To prepare a new release, run `nu build.nu prepare <ACTION>`, where `<ACTION>` is either `patch`, `minor`, or `major`.

## License
MindWork AI Studio is licensed under the `FSL-1.1-MIT` license (functional source license). Here’s a simple rundown of what that means for you:
- **Permitted Use**: Feel free to use, copy, modify, and share the software for your own projects, educational purposes, research, or even in professional services. The key is to use it in a way that doesn't compete with our offerings.
- **Competing Use**: Our only request is that you don't create commercial products or services that replace or compete with MindWork AI Studio or any of our other offerings.
- **No Warranties**: The software is provided "as is", without any promises from us about it working perfectly for your needs. While we strive to make it great, we can't guarantee it will be free of bugs or issues.
- **Future License**: Good news! The license for each release of MindWork AI Studio will automatically convert to an MIT license two years from its release date. This makes it even easier for you to use the software in the future.

For more details, refer to the [LICENSE](https://github.com/MindWorkAI/AI-Studio/blob/main/LICENSE.md) file. This license structure ensures you have plenty of freedom to use and enjoy the software while protecting our work.