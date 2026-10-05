# Security Policy

## Supported versions

We fix security issues in the latest release of AI Studio. Please update to it and check whether the problem still exists before you report it.

## Reporting a vulnerability

Please never report a security vulnerability in a public issue, discussion, or pull request. Report it privately through GitHub instead: [report a vulnerability](https://github.com/MindWorkAI/AI-Studio/security/advisories/new). Only the maintainers can see your report.

A good report tells us:

- which version and operating system you used,
- what an attacker can achieve, and under which conditions,
- how to reproduce the problem, ideally with a proof of concept,
- whether you know of the vulnerability being exploited.

## Findings of AI tools

AI tools are good at finding vulnerabilities, and we welcome such findings. Before you report one, make sure it is real: reproduce it, or at least confirm in the code that it behaves the way the finding claims. Reports nobody has checked will be closed.

## Scope

This policy covers AI Studio itself: the app, its runtime, and the plugins it ships with. Out of scope are:

- vulnerabilities in the services of AI providers, or in self-hosted servers and models,
- the behavior of language models themselves, such as a model following instructions injected into a document, unless AI Studio bypasses or breaks one of its own safeguards,
- configurations which an organization rolls out to its own installations.

## What happens next

We answer as soon as we can, on a best-effort basis; there is no fixed response time. We keep you informed while we work on a fix, and we publish a security advisory once the fix is released. If you like, we credit you in the advisory and in the changelog.

We do not offer a bug bounty.
