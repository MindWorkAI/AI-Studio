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
  - [x] ~~Runtime: Extract data from txt / md / pdf / docx / xlsx files (PR [#374](https://github.com/MindWorkAI/AI-Studio/pull/374))~~
  - [ ] (*Optional*) Runtime: Implement internal embedding provider through [fastembed-rs](https://github.com/Anush008/fastembed-rs)
  - [x] ~~App: Implement dialog for checking & handling [pandoc](https://pandoc.org/) installation ([PR #393](https://github.com/MindWorkAI/AI-Studio/pull/393), [PR #487](https://github.com/MindWorkAI/AI-Studio/pull/487))~~
  - [ ] App: Implement external embedding providers
  - [ ] App: Implement the process to vectorize one local file using embeddings
  - [ ] Runtime: Integration of the vector database [LanceDB](https://github.com/lancedb/lancedb)
  - [ ] App: Implement the continuous process of vectorizing data
  - [x] ~~App: Define a common retrieval context interface for the integration of RAG processes in chats (PR [#281](https://github.com/MindWorkAI/AI-Studio/pull/281), [#284](https://github.com/MindWorkAI/AI-Studio/pull/284), [#286](https://github.com/MindWorkAI/AI-Studio/pull/286), [#287](https://github.com/MindWorkAI/AI-Studio/pull/287))~~
  - [x] ~~App: Define a common augmentation interface for the integration of RAG processes in chats (PR [#288](https://github.com/MindWorkAI/AI-Studio/pull/288), [#289](https://github.com/MindWorkAI/AI-Studio/pull/289))~~
  - [x] ~~App: Integrate data sources in chats (PR [#282](https://github.com/MindWorkAI/AI-Studio/pull/282))~~


- Since September 2024: Experiments have been started on how we can work on long texts with AI Studio. Let's say you want to write a fantasy novel or create a complex project proposal and use LLM for support. The initial experiments were promising, but not yet satisfactory. We are testing further approaches until a satisfactory solution is found. The current state of our experiment is available as an experimental preview feature through your app configuration. Related PR: ~~[PR #167](https://github.com/MindWorkAI/AI-Studio/pull/167), [PR #226](https://github.com/MindWorkAI/AI-Studio/pull/226)~~, [PR #376](https://github.com/MindWorkAI/AI-Studio/pull/376).

- Since March 2025: We have started developing the plugin system. There will be language plugins to offer AI Studio in other languages, configuration plugins to centrally manage certain providers and rules within an organization, and assistant plugins that allow anyone to develop their own assistants. We are using Lua as the plugin language:
  - [x] ~~Plan & implement the base plugin system ([PR #322](https://github.com/MindWorkAI/AI-Studio/pull/322))~~
  - [x] ~~Start the plugin system ([PR #372](https://github.com/MindWorkAI/AI-Studio/pull/372))~~
  - [x] ~~Added hot-reload support for plugins ([PR #377](https://github.com/MindWorkAI/AI-Studio/pull/377), [PR #391](https://github.com/MindWorkAI/AI-Studio/pull/391))~~
  - [x] ~~Add support for other languages (I18N) to AI Studio ([PR #381](https://github.com/MindWorkAI/AI-Studio/pull/381), [PR #400](https://github.com/MindWorkAI/AI-Studio/pull/400), [PR #404](https://github.com/MindWorkAI/AI-Studio/pull/404), [PR #429](https://github.com/MindWorkAI/AI-Studio/pull/429), [PR #446](https://github.com/MindWorkAI/AI-Studio/pull/446), [PR #451](https://github.com/MindWorkAI/AI-Studio/pull/451), [PR #455](https://github.com/MindWorkAI/AI-Studio/pull/455), [PR #458](https://github.com/MindWorkAI/AI-Studio/pull/458), [PR #462](https://github.com/MindWorkAI/AI-Studio/pull/462), [PR #469](https://github.com/MindWorkAI/AI-Studio/pull/469), [PR #486](https://github.com/MindWorkAI/AI-Studio/pull/486))~~
  - [x] ~~Add an I18N assistant to translate all AI Studio texts to a certain language & culture ([PR #422](https://github.com/MindWorkAI/AI-Studio/pull/422))~~
  - [x] ~~Provide MindWork AI Studio in German ([PR #430](https://github.com/MindWorkAI/AI-Studio/pull/430), [PR #446](https://github.com/MindWorkAI/AI-Studio/pull/446), [PR #451](https://github.com/MindWorkAI/AI-Studio/pull/451), [PR #455](https://github.com/MindWorkAI/AI-Studio/pull/455), [PR #458](https://github.com/MindWorkAI/AI-Studio/pull/458), [PR #462](https://github.com/MindWorkAI/AI-Studio/pull/462), [PR #469](https://github.com/MindWorkAI/AI-Studio/pull/469), [PR #486](https://github.com/MindWorkAI/AI-Studio/pull/486))~~
  - [x] ~~Add configuration plugins, which allow pre-defining some LLM providers in organizations ([PR #491](https://github.com/MindWorkAI/AI-Studio/pull/491), [PR #493](https://github.com/MindWorkAI/AI-Studio/pull/493), [PR #494](https://github.com/MindWorkAI/AI-Studio/pull/494), [PR #497](https://github.com/MindWorkAI/AI-Studio/pull/497))~~
  - [ ] Add an app store for plugins, showcasing community-contributed plugins from public GitHub and GitLab repositories. This will enable AI Studio users to discover, install, and update plugins directly within the platform.
  - [ ] Add assistant plugins

Other News:

- April 2025: We have two active financial supporters: Peer `peerschuett` and Dominic `donework`. Thank you very much for your support. MindWork AI reinvests these donations by passing them on to our AI Studio dependencies ([see here](https://github.com/orgs/MindWorkAI/sponsoring)). In the event that we receive large donations, we will first sign the app ([#56](https://github.com/MindWorkAI/Planning/issues/56)). In case we receive more donations, we will look for and pay staff to develop features for AI Studio.

- April 2025: The [German Aerospace Center (DLR)](https://en.wikipedia.org/wiki/German_Aerospace_Center) ([Website](https://www.dlr.de/en)) will use AI Studio at least within the scope of three projects and will also contribute to its further development. This is great news.


Features we have recently released:

- v0.9.50: Added support for self-hosted LLMs using [vLLM](https://blog.vllm.ai/2023/06/20/vllm.html).
- v0.9.46: Released our plugin system, a German language plugin, early support for enterprise environments, and configuration plugins. Additionally, we added the Pandoc integration for future data processing and file generation.
- v0.9.45: Added chat templates to AI Studio, allowing you to create and use a library of system prompts for your chats.
- v0.9.44: Added PDF import to the text summarizer, translation, and legal check assistants, allowing you to import PDF files and use them as input for the assistants.
- v0.9.40: Added support for the `o4` models from OpenAI. Also, we added Alibaba Cloud & Hugging Face as LLM providers.
- v0.9.39: Added the plugin system as a preview feature.
- v0.9.31: Added Helmholtz & GWDG as LLM providers. This is a huge improvement for many researchers out there who can use these providers for free. We added DeepSeek as a provider as well.
- v0.9.29: Added agents to support the RAG process (selecting the best data sources & validating retrieved data as part of the augmentation process)
- v0.9.26+: Added RAG for external data sources using our [ERI interface](https://mindworkai.org/#eri---external-retrieval-interface) as a preview feature.
- v0.9.25: Added [xAI](https://x.ai/) as a new provider. xAI provides their Grok models for generating content.
- v0.9.23: Added support for OpenAI `o` models (`o1`, `o1-mini`, `o3`, etc.); added also an [ERI](https://github.com/MindWorkAI/ERI) server coding assistant as a preview feature behind the RAG feature flag. Your own ERI server can be used to gain access to, e.g., your enterprise data from within AI Studio.

## What is AI Studio?

![MindWork AI Studio - Home](documentation/AI%20Studio%20Home.png)
![MindWork AI Studio - Assistants](documentation/AI%20Studio%20Assistants.png)

MindWork AI Studio is a free desktop app for macOS, Windows, and Linux. It provides a unified user interface for interaction with Large Language Models (LLM). AI Studio also offers so-called assistants, where prompting is not necessary. You can think of AI Studio like an email program: you bring your own API key for the LLM of your choice and can then use these AI systems with AI Studio. Whether you want to use Google Gemini, OpenAI o1, or even your own local AI models.

**Ready to get started 🤩?** [Download the appropriate setup for your operating system here](documentation/Setup.md).

**Key advantages:**
- **Free of charge**: The app is free to use, both for personal and commercial purposes.
- **Independence**: You are not tied to any single provider. Instead, you can choose the providers that best suit your needs. Right now, we support:
  - [OpenAI](https://openai.com/) (GPT5, GPT4.1, o1, o3, o4, etc.)
  - [Mistral](https://mistral.ai/)
  - [Anthropic](https://www.anthropic.com/) (Claude)
  - [Google Gemini](https://gemini.google.com)
  - [xAI](https://x.ai/) (Grok)
  - [DeepSeek](https://www.deepseek.com/en)
  - [Alibaba Cloud](https://www.alibabacloud.com) (Qwen)
  - [Hugging Face](https://huggingface.co/) using their [inference providers](https://huggingface.co/docs/inference-providers/index) such as Cerebras, Nebius, Sambanova, Novita, Hyperbolic, Together AI, Fireworks, Hugging Face
  - Self-hosted models using [llama.cpp](https://github.com/ggerganov/llama.cpp), [ollama](https://github.com/ollama/ollama), [LM Studio](https://lmstudio.ai/), and [vLLM](https://github.com/vllm-project/vllm)
  - [Groq](https://groq.com/)
  - [Fireworks](https://fireworks.ai/)
  - For scientists and employees of research institutions, we also support [Helmholtz](https://helmholtz.cloud/services/?serviceID=d7d5c597-a2f6-4bd1-b71e-4d6499d98570) and [GWDG](https://gwdg.de/services/application-services/ai-services/) AI services. These are available through federated logins like eduGAIN to all 18 Helmholtz Centers, the Max Planck Society, most German, and many international universities.
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

## Enterprise IT
Do you want to manage AI Studio centrally from your IT department? Yes, that’s possible. [Here’s how it works.](documentation/Enterprise%20IT.md)

## License
MindWork AI Studio is licensed under the `FSL-1.1-MIT` license (functional source license). Here’s a simple rundown of what that means for you:
- **Permitted Use**: Feel free to use, copy, modify, and share the software for your own projects, educational purposes, research, or even in professional services. The key is to use it in a way that doesn't compete with our offerings.
- **Competing Use**: Our only request is that you don't create commercial products or services that replace or compete with MindWork AI Studio or any of our other offerings.
- **No Warranties**: The software is provided "as is", without any promises from us about it working perfectly for your needs. While we strive to make it great, we can't guarantee it will be free of bugs or issues.
- **Future License**: Good news! The license for each release of MindWork AI Studio will automatically convert to an MIT license two years from its release date. This makes it even easier for you to use the software in the future.

For more details, refer to the [LICENSE](LICENSE.md) file. This license structure ensures you have plenty of freedom to use and enjoy the software while protecting our work.