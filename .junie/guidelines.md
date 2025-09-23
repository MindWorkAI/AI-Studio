# Project Guidelines

## Repository Structure
- The repository and the app consist of a Rust project in the `runtime` folder and a .NET solution in the `app` folder.
- The .NET solution then contains 4 .NET projects:
  - `Build Script` is not required for running the app; instead, it contains the build script for creating new releases, for example.
  - `MindWork AI Studio` contains the actual app code.
  - `SharedTools` contains types that are needed in the build script and in the app, for example.
  - `SourceCodeRules` is a Roslyn analyzer project. It contains analyzers and code fixes that we use to enforce code style rules within the team.

## Changelogs
- There is a changelog in Markdown format for each version.
- All changelogs are located in the folder `app/MindWork AI Studio/wwwroot/changelog`.
- These changelogs are intended for end users, not for developers.
- Therefore, we don't mention all changes in the changelog: changes that end users wouldn't understand remain unmentioned. For complex refactorings, for example, we mention a generic point that the code quality has been improved to enhance future maintenance.
- The changelog is always written in US English.
- The changelog doesn't mention bug fixes if the bug was never shipped and users don't know about it.