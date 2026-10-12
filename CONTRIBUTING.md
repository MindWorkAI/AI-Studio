# Contributing to MindWork AI Studio

Thank you for considering a contribution to AI Studio. This document explains how we work, what we expect from a pull request, and what you agree to when you submit one. Please read it before you start, especially before larger work.

## In short

- We build AI Studio with AI coding agents, and we recommend that you do the same. You remain responsible for every line you submit.
- Before you build a new feature or change something fundamental, ask in a short proposal whether it fits our plans. Small fixes need no proposal.
- Fill in the pull request template and tick the required boxes yourself.
- Allow edits by maintainers. Pull requests without this option are closed.
- We treat pull requests as proposals: we will change yours to fit the product, and we may close it.
- You license your contribution under the MIT License. We ship it as part of AI Studio under the project license or under the MIT License.
- We thank contributors in the changelog and on the supporters page in the app, unless you tell us otherwise.
- Malicious contributions, attempts to manipulate the project, and spam lead to a permanent block.

## How we build AI Studio

AI Studio has grown into a large codebase: a Rust runtime, a .NET Blazor app, a Lua plugin system, retrieval, tool calling, enterprise configuration, and more. Changes to a codebase of this size and complexity are no longer practical to get right without AI assistance. That is why we build AI Studio with AI coding agents, and why we recommend that you do too.

What we mean is agentic software development, not vibe coding. The agent does the work; you direct it, read what it produced, test it, and have it fix what is wrong. A pull request which nobody has read before it was submitted is not a contribution.

As of October 2026, we recommend one of these setups, or a newer model of the same class:

- Claude Code with Claude Opus 5.5 at reasoning effort xhigh or higher
- OpenAI Codex with Sol 6 at reasoning effort xhigh or higher

The repository contains an [AGENTS.md](AGENTS.md), which your agent reads on its own. It describes the architecture, our conventions, and how to build and test AI Studio, and it is binding for every agent working on the code.

Before you open a pull request, run [the quality gate](documentation/Build.md#the-quality-gate) with `dotnet run verify`, then [run the app locally](documentation/Build.md#run-the-app-locally-with-all-your-changes) and try out what you changed.

Contributions written without AI assistance are welcome as well, as long as they meet the same standard.

## Before you start

### Changes that need a proposal first

It would be a shame if you put many hours into a pull request we cannot accept, because it does not fit where AI Studio is heading or because it collides with work already underway that you cannot see from the outside. A short proposal prevents that. Please [open a proposal](https://github.com/MindWorkAI/AI-Studio/discussions/new?category=proposals) in our discussions before you start if your change:

- adds a new feature or a new assistant,
- adds a new dependency, such as a NuGet package or a Rust crate,
- adds a new LLM or embedding provider,
- changes how data or settings are stored,
- changes the interfaces of the Lua plugins, the configuration plugins, or the enterprise configuration,
- changes the architecture or the concept of the user interface.

A proposal is not a design document. Describe the problem from the point of view of the people using AI Studio, and your intended solution in a few sentences. We answer with one of three replies:

- **Go:** it fits, start working.
- **Go, but …:** it fits, with a note on the direction we need.
- **Not planned:** it does not fit our plans right now.

The detailed review happens later, on the pull request.

### Issues labeled "help wanted"

[Our roadmap](https://github.com/orgs/MindWorkAI/projects/2/views/3) contains large features which need preparatory work in the codebase first, and from the outside you cannot tell which ones are ready. Issues we consider ready for contributors from outside the core team carry the label `help wanted`, both [in this repository](https://github.com/MindWorkAI/AI-Studio/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22help%20wanted%22) and [in our planning repository](https://github.com/MindWorkAI/Planning/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22help%20wanted%22). They need no proposal. Leave a short comment on the issue that you are working on it so that nobody does the same work twice. For anything else on our roadmap, please open a proposal first.

### Everything else

Bug fixes, small improvements, and corrections to the documentation need no proposal. Just open a pull request.

## Pull requests

### Requirements

- **Fill in the pull request template.** Its required checkboxes are statements you make personally, so tick them yourself.
- **Allow edits by maintainers.** We revise pull requests with our own agents and push the changes directly to your branch, so this option is mandatory. GitHub offers it only for forks owned by a personal account. If your fork belongs to an organization, please open your pull request from a personal fork instead.
- An automated check verifies both points. When something is missing, it leaves a comment explaining what to fix. Pull requests which still fail the check after seven days are closed.
- Keep each pull request to one topic.
- GitHub limits you to three open pull requests at a time; drafts do not count. When you have more in the pipeline, wait until one of them is merged or closed.

### What happens after you submit

We understand pull requests as proposals. We review each one against our product vision and our codebase, and we usually change it before merging: sometimes a little, sometimes a lot. We do this with our own agents, directly on your branch. When we merge, all commits are squashed into one, and you remain credited as its author or co-author in the git history.

Please keep in mind:

- There is no obligation to merge a pull request, and there is no fixed time frame for a review.
- We may close a pull request without a detailed explanation, for example when reviewing it would cost more than it contributes.
- Contributions are voluntary and unpaid.

## Using AI responsibly

Use AI as much as you like. What matters is that a person stands behind the result.

- **Understand what you submit.** You must be able to explain every change when we ask. Whether you write your answers yourself or with the help of AI, for example to translate them into English, is up to you.
- **You are responsible, no matter who clicks the button.** It makes no difference to us whether you or your agent opens a pull request or posts a comment. What counts is that you have checked the content and stand behind it.
- **Check what your tools found.** AI tools are good at finding bugs and vulnerabilities, and we welcome such findings. Before you report one, make sure it is real: reproduce it, or at least confirm in the code that it behaves the way the finding claims. Findings nobody has checked will be closed.

You do not need to tell us which tools or models you used.

## Issues, ideas, and questions

- **You found a bug:** open an issue [in this repository](https://github.com/MindWorkAI/AI-Studio/issues/new/choose).
- **You have an idea for a feature but do not plan to build it:** open an issue in [our planning repository](https://github.com/MindWorkAI/Planning/issues).
- **You want to build something that needs a proposal:** start a discussion in the [Proposals](https://github.com/MindWorkAI/AI-Studio/discussions/categories/proposals) category.
- **You have a question:** ask it in the [Q&A](https://github.com/MindWorkAI/AI-Studio/discussions/categories/q-a) category.
- **You found a security vulnerability:** never report it publicly. Follow our [security policy](SECURITY.md).

## Licensing of your contribution

AI Studio is distributed under the [Functional Source License, Version 1.1, MIT Future License](LICENSE.md) (FSL-1.1-MIT). With each pull request you submit, you agree to the following for that pull request, including all commits you add to it later (your "contribution"):

1. **You license your contribution under the [MIT License](https://opensource.org/license/mit).** This allows us to distribute it as part of AI Studio under the Functional Source License, and to change the license of AI Studio to the MIT License at any time.
2. **You have the right to do so.** The contribution is your own work, or you have permission to submit it. If you created it as part of your job or for a client, your employer or client agrees to the contribution.
3. **Your contribution does not infringe the rights of others**, as far as you know, and contains no code under a license incompatible with this arrangement. This includes the output of AI tools: you have made sure that it does not reproduce third-party code under incompatible terms.
4. **You provide your contribution "as is"**, without any warranty, and you are not expected to support it.

You confirm this by ticking the corresponding boxes in the pull request template. Our automated check records your confirmation in a comment on the pull request.

## Credits and privacy

We like to thank the people who contribute. Depending on the size of a contribution, we mention you in the changelog of the release and on the supporters page inside the app, with your GitHub username and, if you show it publicly on your GitHub profile, your name.

You decide how we credit you: tick "Credit me with my GitHub username only" or "Do not credit me" in the pull request template. You can change your choice later by contacting us; we correct it from the next release on. Released changelogs are part of past versions of the app and stay as they are.

Git keeps a permanent record. The name and email address of your commits become part of the public history of the repository, which cannot be changed afterward. If you do not want your private email address there, [use the noreply address GitHub provides](https://docs.github.com/en/account-and-profile/setting-up-and-managing-your-personal-account-on-github/managing-email-preferences/setting-your-commit-email-address). We use this information to credit you and to keep track of where the code in AI Studio comes from.

## Unacceptable contributions

Any of the following gets the contributions closed and the account permanently blocked and reported to GitHub:

- **Malicious code:** backdoors, malware, deliberately introduced vulnerabilities, or anything else meant to harm the people using AI Studio or the project. This includes attempts to compromise our supply chain, such as dependencies, build scripts, workflows, or the release process.
- **Hidden or obfuscated content:** invisible or misleading Unicode characters, encoded payloads, or changes which do something other than what they claim to do.
- **Manipulation of AI systems:** we review and revise contributions with AI agents. Text aimed at those agents, such as instructions hidden in code, comments, documentation, test data, commit messages, or pull request descriptions, counts as a malicious contribution, no matter how harmless it looks.
- **Manipulation of the project or its people:** fake accounts, fake or coordinated reviews and votes, or pressure and social engineering to gain access or to rush changes through.
- **Flooding the project** with issues, pull requests, discussions, or comments, no matter whether by hand or automated.

Where such conduct may constitute a criminal offense, we reserve the right to report it to the law enforcement authorities.

## Further notes

- Everyone taking part in the spaces of AI Studio is expected to follow our [Code of Conduct](CODE_OF_CONDUCT.md).
- Contributing does not grant you any right to use the name, the logos, or other marks of MindWork AI Studio.
- We may update this document. The version in effect when you open a pull request applies to that pull request.

Thank you for helping to make AI Studio better.
