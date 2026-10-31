# AGENTS.md

These instructions cover preparing a release and crediting contributors. They add to the `AGENTS.md` in
the repository root, which applies here as well; its "Changelogs" section explains how to write the
changelog entries.

## Release Process

1. Create changelog file: `app/MindWork AI Studio/wwwroot/changelog/vX.Y.Z.md`
2. Check that every contribution in the release is credited, see "Crediting contributors" below. The
   authors of all pull requests merged since the last release are listed by
   `gh pr list -R MindWorkAI/AI-Studio --state merged --limit 1000 --search "merged:>=<YYYY-MM-DD>" --json author --jq '[.[].author.login] | unique | .[]'`,
   with the date of the last release tag from `git log -1 --format=%cs <last release tag>`; skip bots
3. Commit changelog
4. Run from `app/Build`: `dotnet run release --action <build|month|year>`
5. Create PR with version bump and changes
6. After PR merge, maintainer creates git tag: `vX.Y.Z`
7. GitHub Actions builds release binaries for all platforms
8. Binaries uploaded to GitHub Releases
9. Update the wiki (`MindWorkAI/AI-Studio.wiki`): raise the version in `_Footer.md` and `de/_Footer.md`, and
   check that the user guide and the Enterprise IT pages describe the release; see "Wiki" in the root
   `AGENTS.md`

## Crediting contributors

When a pull request is merged, or a release is prepared, check both places where we thank contributors.
This holds for everybody except the maintainer, members of the core team included, not only for external
contributors:

- **The changelog entry of the change.** Thank the contributor at the end of the entry, in the form
  ``<first name> <last name> (`<GitHub username>`)``, and call a first contribution out as such.
- **The "Code Contributions" list on the supporters page** in `app/MindWork AI Studio/Pages/Supporters.razor`.
  Add contributors who are not listed yet, one
  `<Supporter Name="<GitHub username>" Type="SupporterType.INDIVIDUAL" URL="https://github.com/<GitHub username>" Acknowledgment="@T("…")"/>`
  each, at the end of the list. The list is sorted by the first merged contribution of each person; someone
  whom the changelog thanks for work inside another person's pull request counts from that pull request on.

The acknowledgment calls the person by their first name, unless the credit choice below rules out their
name, and says what they built. It never compares contributors or counts their contributions, and the
order of the list is chronological only. Bug reports
alone and commissioned work outside this repository get no entry on the supporters page; the changelog may
still thank them.

The credit choice in the pull request template is binding:

- **"Credit me with my GitHub username only":** use the GitHub username alone. No real name, neither in
  the changelog nor in the acknowledgment text.
- **"Do not credit me":** neither a changelog mention nor an entry on the supporters page.
- **No choice ticked, or a pull request from before the template:** the GitHub username plus the name, when
  the contributor shows it publicly on their GitHub profile (`gh api users/<GitHub username> --jq .name`)
  or an earlier changelog already thanked them by that name.

Acknowledgments on the supporters page are `T()` texts, so the two steps of "Localization" in the root
`AGENTS.md` apply: remind the developer to run the localization, then review the German value.
