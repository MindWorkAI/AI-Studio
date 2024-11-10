# MindWork AI Studio

## News
Things we are currently working on:

- Since November 2024: Work on RAG (integration of your data and files) has begun. We will support the integration of local and external data sources. We need to implement the following runtime (Rust) and app (.NET) steps:

  - [x] ~~Runtime: Restructuring the code into meaningful modules (PR [#192](https://github.com/MindWorkAI/AI-Studio/pull/192))~~
  - [x] ~~Define the [External Data API (EDI)](https://github.com/MindWorkAI/EDI) as a contract for integrating arbitrary external data (PR [#1](https://github.com/MindWorkAI/EDI/pull/1))~~
  - [ ] App: Metadata for providers (which provider offers embeddings?)
  - [ ] App: Management of data sources (local data)
  - [ ] Runtime: Extract data from txt / md / pdf / docx / xlsx files
  - [ ] App: Implement embedding providers
  - [ ] App: Implement the process to vectorize local data using embeddings
  - [ ] Runtime: Integration of the vector database [LanceDB](https://github.com/lancedb/lancedb)
  - [ ] App: Define an interface for the integration of RAG processes in chats
  - [ ] App: Integrate data sources in chats
  - [ ] App: Management of data sources (external data via [EDI](https://github.com/MindWorkAI/EDI))


- Since September 2024: Experiments have been started on how we can work on long texts with AI Studio. Let's say you want to write a fantasy novel or create a complex project proposal and use LLM for support. The initial experiments were promising, but not yet satisfactory. We are testing further approaches until a satisfactory solution is found. Related PR: [#167](https://github.com/MindWorkAI/AI-Studio/pull/167).


Other News:

- October 2024: We've found the first two financial supporters. Huge thanks to `richard-stanton` and `peerschuett` for backing the project. Thanks for having the courage to be the first to support us.

- October 2024: The [German Aerospace Center (DLR)](https://en.wikipedia.org/wiki/German_Aerospace_Center) ([Website](https://www.dlr.de/en)) will use AI Studio at least within the scope of one project and will also contribute to its further development. This is great news.


Features we have recently released:

- v0.9.17: Added the new Anthropic model `claude-3-5-sonnet-20241022`.
- v0.9.16: Added workspace display options & improved the layout of the app window.
- v0.9.15: Added the bias-of-the-day assistant. Tells you about a cognitive bias every day.
- v0.9.13: You can use `ollama` providers secured with API keys.
- v0.9.12: Added a job posting assistant to the business category and improved grammar & spelling check and rewrite assistants.
- v0.9.11: Added enforcement of minimal confidence levels & dark mode.
- v0.9.10: Added support for the OpenAI `o1` model.

## What is AI Studio?

![MindWork AI Studio - Home](documentation/AI%20Studio%20Home.png)
![MindWork AI Studio - Assistants](documentation/AI%20Studio%20Assistants.png)

MindWork AI Studio is a desktop application available for macOS, Windows, and Linux. It provides a unified chat interface for Large Language Models (LLMs). You bring your own API key for the respective LLM provider to use the models. The API keys are securely stored by the operating system.

**Key advantages:**
- **Free of charge**: The app is free to use, both for personal and commercial purposes.
- **Independence**: You are not tied to any single provider. Instead, you can choose the provider that best suits their needs. Right now, we support OpenAI (GPT4o etc.), Mistral, Anthropic (Claude), Google Gemini, and self-hosted models using [llama.cpp](https://github.com/ggerganov/llama.cpp), [ollama](https://github.com/ollama/ollama), [LM Studio](https://lmstudio.ai/), [Groq](https://groq.com/), or [Fireworks](https://fireworks.ai/).
- **Unrestricted usage**: Unlike services like ChatGPT, which impose limits after intensive use, MindWork AI Studio offers unlimited usage through the providers API.
- **Cost-effective**: You only pay for what you use, which can be cheaper than monthly subscription services like ChatGPT Plus, especially if used infrequently. But beware, here be dragons: For extremely intensive usage, the API costs can be significantly higher. Unfortunately, providers currently do not offer a way to display current costs in the app. Therefore, check your account with the respective provider to see how your costs are developing. When available, use prepaid and set a cost limit.
- **Privacy**: The data entered into the app is not used for training by the providers since we are using the provider's API.
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
Here's an exciting look at some of the features we're planning to add to MindWork AI Studio in future releases:
- **More providers**: We plan to add support for additional LLM providers, such as Google Gemini, giving you more options to choose from.
- **System prompts**: Integration of a system prompt library will allow you to control the behavior of the LLM with predefined prompts, ensuring consistency and efficiency.
- **Text replacement for better privacy**: Define keywords that will be replaced in your chats before sending content to the provider, enhancing your privacy.
- **Advanced interactions**: We're full of ideas for advanced interactions tailored for specific use cases, whether in a business context or for writers and other professionals.

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