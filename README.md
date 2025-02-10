# MindWork AI Studio
Are you new here? [Read here](#what-is-ai-studio) what AI Studio is.

## News
Things we are currently working on:

- Since November 2024: Work on RAG (integration of your data and files) has begun. We will support the integration of local and external data sources. We need to implement the following runtime (Rust) and app (.NET) steps:

  - [x] ~~Runtime: Restructuring the code into meaningful modules (PR [#192](https://github.com/MindWorkAI/AI-Studio/pull/192))~~
  - [x] ~~Define the [External Retrieval Interface (ERI)](https://github.com/MindWorkAI/ERI) as a contract for integrating arbitrary external data (PR [#1](https://github.com/MindWorkAI/ERI/pull/1))~~
  - [x] ~~App: Metadata for providers (which provider offers embeddings?) (PR [#205](https://github.com/MindWorkAI/AI-Studio/pull/205))~~
  - [x] ~~App: Add an option to show preview features (PR [#222](https://github.com/MindWorkAI/AI-Studio/pull/222))~~
  - [x] ~~App: Configure embedding providers (PR [#224](https://github.com/MindWorkAI/AI-Studio/pull/224))~~
  - [x] ~~App: Implement an [ERI](https://github.com/MindWorkAI/ERI) server coding assistant (PR [#231](https://github.com/MindWorkAI/AI-Studio/pull/231))~~
  - [x] ~~App: Management of data sources (local & external data via [ERI](https://github.com/MindWorkAI/ERI)) (PR [#259](https://github.com/MindWorkAI/AI-Studio/pull/259), [#273](https://github.com/MindWorkAI/AI-Studio/pull/273))~~
  - [ ] Runtime: Extract data from txt / md / pdf / docx / xlsx files
  - [ ] (*Optional*) Runtime: Implement internal embedding provider through [fastembed-rs](https://github.com/Anush008/fastembed-rs)
  - [ ] App: Implement external embedding providers
  - [ ] App: Implement the process to vectorize one local file using embeddings
  - [ ] Runtime: Integration of the vector database [LanceDB](https://github.com/lancedb/lancedb)
  - [ ] App: Implement the continuous process of vectorizing data
  - [x] ~~App: Define a common retrieval context interface for the integration of RAG processes in chats (PR [#281](https://github.com/MindWorkAI/AI-Studio/pull/281))~~
  - [ ] App: Define a common augmentation interface for the integration of RAG processes in chats
  - [ ] App: Integrate data sources in chats


- Since September 2024: Experiments have been started on how we can work on long texts with AI Studio. Let's say you want to write a fantasy novel or create a complex project proposal and use LLM for support. The initial experiments were promising, but not yet satisfactory. We are testing further approaches until a satisfactory solution is found. The current state of our experiment is available as an experimental preview feature through your app configuration. Related PR: ~~[#167](https://github.com/MindWorkAI/AI-Studio/pull/167), [#226](https://github.com/MindWorkAI/AI-Studio/pull/226)~~.


Other News:

- October 2024: We've found the first two financial supporters. Huge thanks to `richard-stanton` and `peerschuett` for backing the project. Thanks for having the courage to be the first to support us.

- October 2024: The [German Aerospace Center (DLR)](https://en.wikipedia.org/wiki/German_Aerospace_Center) ([Website](https://www.dlr.de/en)) will use AI Studio at least within the scope of one project and will also contribute to its further development. This is great news.


Features we have recently released:

- v0.9.25: Added [xAI](https://x.ai/) as a new provider. xAI provides their Grok models for generating content.
- v0.9.23: Added support for OpenAI `o` models (`o1`, `o1-mini`, `o3`, etc.); added also an [ERI](https://github.com/MindWorkAI/ERI) server coding assistant as a preview feature behind the RAG feature flag. Your own ERI server can be used to gain access to, e.g., your enterprise data from within AI Studio.
- v0.9.22: Added options for preview features; added embedding provider configuration for RAG (preview) and writer mode (experimental preview).
- v0.9.18: Added the new Anthropic Heiku model; added Groq and Google Gemini as provider options.
- v0.9.17: Added the new Anthropic model `claude-3-5-sonnet-20241022`.
- v0.9.16: Added workspace display options & improved the layout of the app window.
- v0.9.15: Added the bias-of-the-day assistant. Tells you about a cognitive bias every day.
- v0.9.13: You can use `ollama` providers secured with API keys.

## What is AI Studio?

![MindWork AI Studio - Home](documentation/AI%20Studio%20Home.png)
![MindWork AI Studio - Assistants](documentation/AI%20Studio%20Assistants.png)

MindWork AI Studio is a free desktop app for macOS, Windows, and Linux. It provides a unified user interface for interaction with Large Language Models (LLM). AI Studio also offers so-called assistants, where prompting is not necessary. You can think of AI Studio like an email program: you bring your own API key for the LLM of your choice and can then use these AI systems with AI Studio. Whether you want to use Google Gemini, OpenAI o1, or even your own local AI models.

**Ready to get started 🤩?** [Download the appropriate setup for your operating system here](documentation/Setup.md).

**Key advantages:**
- **Free of charge**: The app is free to use, both for personal and commercial purposes.
- **Independence**: You are not tied to any single provider. Instead, you can choose the provider that best suits their needs. Right now, we support OpenAI (GPT4o, o1, etc.), Mistral, Anthropic (Claude), Google Gemini, xAI (Grok), and self-hosted models using [llama.cpp](https://github.com/ggerganov/llama.cpp), [ollama](https://github.com/ollama/ollama), [LM Studio](https://lmstudio.ai/), [Groq](https://groq.com/), or [Fireworks](https://fireworks.ai/).
- **Assistants**: You just want to quickly translate a text? AI Studio has so-called assistants for such and other tasks. No prompting is necessary when working with these assistants.
- **Unrestricted usage**: Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API.
- **Cost-effective**: You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit.
- **Privacy**: You can control which providers receive your data using the provider confidence settings. For example, you can set different protection levels for writing emails compared to general chats, etc. Additionally, most providers guarantee that they won't use your data to train new AI systems.
- **Flexibility**: Choose the provider and model best suited for your current task.
- **No bloatware**: The app requires minimal storage for installation and operates with low memory usage. Additionally, it has a minimal impact on system resources, which is beneficial for battery life.

## **Ready to get started 🤩?** [Download the appropriate setup for your operating system here](documentation/Setup.md).

## Support the Project
Thank you for using MindWork AI Studio and considering supporting its development 😀. Your support helps keep the project alive and ensures continuous improvements and new features.

We offer various ways you can support the project:

- **Monthly Support**: By contributing a monthly amount, you can significantly help us maintain and develop the project. As a token of our appreciation, we will include your name or company logo in the app. While we cannot guarantee exclusive content at this time, we are working towards offering unique perks in the future.

- **One-Time Contributions**: Make a one-time donation and have your name or company logo included in the app as a gesture of our gratitude.

For companies, sponsoring MindWork AI Studio is not only a way to support innovation but also a valuable opportunity for public relations and marketing. Your company's name and logo will be featured prominently, showcasing your commitment to using cutting-edge AI tools and enhancing your reputation as an innovative enterprise.

To view all available tiers, please visit our [GitHub Sponsors page](https://github.com/sponsors/MindWorkAI).
Your support, whether big or small, keeps the wheels turning and is deeply appreciated ❤️.

## Planned Features
Here's an exciting look at some of the features we're planning to add to AI Studio in future releases:
- **Integrating your data**: You'll be able to integrate your data into AI Studio, like your PDF or Office files, or your Markdown notes.
- **Integration of enterprise data:** It will soon be possible to integrate data from the corporate network using a specified interface ([External Retrieval Interface](https://github.com/MindWorkAI/ERI), ERI for short). This will likely require development work by the organization in question.
- **Useful assistants:** We'll develop more assistants for everyday tasks.
- **Writing mode:** We're integrating a writing mode to help you create extensive works, like comprehensive project proposals, tenders, or your next fantasy novel.
- **Specific requirements:** Want an assistant that suits your specific needs? We aim to offer a plugin architecture so organizations and enthusiasts can implement such ideas.
- **Voice control:** You'll interact with the AI systems using your voice. To achieve this, we want to integrate voice input (speech-to-text) and output (text-to-speech). However, later on, it should also have a natural conversation flow, i.e., seamless conversation.
- **Content creation:** There will be an interface for AI Studio to create content in other apps. You could, for example, create blog posts directly on the target platform or add entries to an internal knowledge management tool. This requires development work by the tool developers.
- **Email monitoring:** You can connect your email inboxes with AI Studio. The AI will read your emails and notify you of important events. You'll also be able to access knowledge from your emails in your chats.
- **Browser usage:** We're working on offering AI Studio features in your browser via a plugin, allowing, e.g., for spell-checking or text rewriting directly in the browser.

Stay tuned for more updates and enhancements to make MindWork AI Studio even more powerful and versatile 🤩.

## Building
You want to know how to build MindWork AI Studio from source? [Check out the instructions here](documentation/Build.md).

## License
MindWork AI Studio is licensed under the `FSL-1.1-MIT` license (functional source license). Here’s a simple rundown of what that means for you:
- **Permitted Use**: Feel free to use, copy, modify, and share the software for your own projects, educational purposes, research, or even in professional services. The key is to use it in a way that doesn't compete with our offerings.
- **Competing Use**: Our only request is that you don't create commercial products or services that replace or compete with MindWork AI Studio or any of our other offerings.
- **No Warranties**: The software is provided "as is", without any promises from us about it working perfectly for your needs. While we strive to make it great, we can't guarantee it will be free of bugs or issues.
- **Future License**: Good news! The license for each release of MindWork AI Studio will automatically convert to an MIT license two years from its release date. This makes it even easier for you to use the software in the future.

For more details, refer to the [LICENSE](LICENSE.md) file. This license structure ensures you have plenty of freedom to use and enjoy the software while protecting our work.