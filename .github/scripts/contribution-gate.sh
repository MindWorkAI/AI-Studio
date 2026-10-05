#!/usr/bin/env bash
#
# Checks the pull requests of external contributors against the requirements in CONTRIBUTING.md:
# "Allow edits by maintainers" must be enabled, and the required statements of the pull request
# template must be ticked, word by word. The result is reported as the commit status
# "contribution-gate", together with one comment per pull request: a list of what to fix, or a
# receipt of the confirmed statements. Pull requests which still fail after GRACE_DAYS are closed.
#
# Usage:
#   contribution-gate.sh <pull request number>   checks one pull request
#   contribution-gate.sh --all                   checks every open pull request
#
# Environment:
#   GH_TOKEN      token for the GitHub API
#   REPOSITORY    owner/name of the repository, e.g. MindWorkAI/AI-Studio
#   DRY_RUN       "true" reports every change instead of making it
#   EVENT_ACTION  action of the pull request event which started the check, if any
#
# The script runs on Linux in our workflow and on macOS for local tests, so it sticks to bash 3.2
# and portable tools. The description of a pull request is untrusted input: it is only ever handled
# as data, never evaluated.

set -euo pipefail

: "${REPOSITORY:?REPOSITORY must be set, e.g. MindWorkAI/AI-Studio}"
DRY_RUN="${DRY_RUN:-false}"
EVENT_ACTION="${EVENT_ACTION:-}"

GRACE_DAYS=7
LABEL="needs-contributor-action"
STATUS_CONTEXT="contribution-gate"
MARKER="<!-- contribution-gate -->"
BOT_LOGIN="github-actions[bot]"
CONTRIBUTING_URL="https://github.com/$REPOSITORY/blob/main/CONTRIBUTING.md"
TEMPLATE_URL="https://github.com/$REPOSITORY/blob/main/.github/pull_request_template.md"

# These must match the required statements in .github/pull_request_template.md word by word.
REQUIRED_STATEMENTS=(
  "**Review:** I have reviewed every change in this pull request and can explain it when asked."
  "**License:** I license my contribution in this pull request, including all commits I add to it later, under the MIT License, and I agree that it is distributed as part of MindWork AI Studio under the project license or under the MIT License."
  "**Right to contribute:** The contribution is my own work, or I have permission to submit it, including the consent of my employer or client where needed. To the best of my knowledge, it does not infringe the rights of others, and AI-generated parts do not reproduce third-party code under incompatible terms."
  "**Changes by maintainers:** I understand that the maintainers will change this pull request to fit the product, and that they may close it."
)

log() {
  printf '%s\n' "$*" >&2
}

# Runs a GitHub API call which changes something; during a dry run, it only reports the call.
api_write() {
  if [ "$DRY_RUN" = "true" ]; then
    log "[dry run] gh api $*"
    return 0
  fi

  gh api "$@" > /dev/null
}

set_status() {
  local sha="$1" state="$2" description="$3"
  api_write -X POST "repos/$REPOSITORY/statuses/$sha" \
    -f state="$state" -f context="$STATUS_CONTEXT" -f description="$description"
}

# Writes the comment of the gate: creates it, or updates the existing one when its text changed.
write_comment() {
  local number="$1" comment_id="$2" old_body="$3" new_body="$4"
  if [ -z "$comment_id" ]; then
    api_write -X POST "repos/$REPOSITORY/issues/$number/comments" -f body="$new_body"
  elif [ "$old_body" != "$new_body" ]; then
    api_write -X PATCH "repos/$REPOSITORY/issues/comments/$comment_id" -f body="$new_body"
  fi
}

check_all() {
  local numbers number failed=0
  numbers=$(gh api --paginate "repos/$REPOSITORY/pulls?state=open&per_page=100" --jq '.[].number')
  for number in $numbers; do

    # Each pull request runs in its own process, so that set -e stays in effect for it and an
    # error in one check does not stop the others.
    if ! bash "$0" "$number"; then
      log "::warning::The contribution check of pull request #$number failed."
      failed=1
    fi
  done

  return "$failed"
}

check_one() {
  local number="$1"
  local pr
  pr=$(gh api "repos/$REPOSITORY/pulls/$number")

  local state draft created_at author association head_sha head_repo owner_type can_modify body
  state=$(jq -r '.state' <<< "$pr")
  draft=$(jq -r '.draft' <<< "$pr")
  created_at=$(jq -r '.created_at' <<< "$pr")
  author=$(jq -r '.user.login' <<< "$pr")
  association=$(jq -r '.author_association' <<< "$pr")
  head_sha=$(jq -r '.head.sha' <<< "$pr")
  head_repo=$(jq -r '.head.repo.full_name // ""' <<< "$pr")
  owner_type=$(jq -r '.head.repo.owner.type // ""' <<< "$pr")
  can_modify=$(jq -r '.maintainer_can_modify' <<< "$pr")
  body=$(jq -r '.body // ""' <<< "$pr")

  if [ "$state" != "open" ]; then
    log "#$number: skipped, the pull request is $state."
    return 0
  fi

  # The core team is exempt. The permission is asked for in addition to the association, because
  # the association of members with a private membership may read CONTRIBUTOR for our token.
  local permission
  permission=$(gh api "repos/$REPOSITORY/collaborators/$author/permission" --jq '.permission' 2>/dev/null || echo "unknown")
  if [ "$head_repo" = "$REPOSITORY" ] \
    || [ "$permission" = "admin" ] || [ "$permission" = "write" ] \
    || [ "$association" = "OWNER" ] || [ "$association" = "MEMBER" ] || [ "$association" = "COLLABORATOR" ]; then
    log "#$number: exempt, @$author belongs to the core team."
    set_status "$head_sha" success "Not required for the core team."
    return 0
  fi

  # Pull requests opened before the gate arrived on the default branch are exempt: they were
  # written without the template. The oldest commit of this script there marks that moment; on
  # a branch which does not have it yet, nobody is exempt.
  local introduced_at
  introduced_at=$(gh api --paginate "repos/$REPOSITORY/commits?path=.github/scripts/contribution-gate.sh&per_page=100" \
    --jq '.[].commit.committer.date' | tail -n 1)
  if [ -n "$introduced_at" ] && [[ "$created_at" < "$introduced_at" ]]; then
    log "#$number: exempt, opened before the contribution check was introduced on $introduced_at."
    set_status "$head_sha" success "Opened before the contribution check was introduced."
    return 0
  fi

  if [ "$draft" = "true" ]; then
    log "#$number: waiting, the pull request is a draft."
    set_status "$head_sha" pending "Checked once the pull request is ready for review."
    return 0
  fi

  # Collect what the contributor has to fix, as Markdown list items.
  local problems="" problem_count=0
  if [ -z "$head_repo" ]; then
    problems+=$'- The fork of this pull request no longer exists. Please open a new pull request from a personal fork.\n'
    problem_count=$((problem_count + 1))
  elif [ "$owner_type" = "Organization" ]; then
    problems+=$'- Your fork belongs to an organization, and GitHub offers **Allow edits by maintainers** only for forks owned by a personal account. Please open this pull request again from a personal fork.\n'
    problem_count=$((problem_count + 1))
  elif [ "$can_modify" != "true" ]; then
    problems+=$'- Enable **Allow edits by maintainers** in the sidebar of this pull request. We revise pull requests with our own agents and push the changes directly to your branch.\n'
    problem_count=$((problem_count + 1))
  fi

  # Ticked task list items of the description, with their whitespace normalized.
  local ticked statement label missing="" missing_count=0
  ticked=$(printf '%s\n' "$body" | tr -d '\r' \
    | sed -nE 's/^[[:space:]]*[-*+][[:space:]]+\[[xX]\][[:space:]]+//p' \
    | sed -E 's/[[:space:]]+/ /g; s/ $//')

  for statement in "${REQUIRED_STATEMENTS[@]}"; do
    if ! grep -Fxq -- "$statement" <<< "$ticked"; then
      label=$(sed -E 's/^\*\*([^*]+):\*\*.*$/\1/' <<< "$statement")
      missing+="${missing:+, }**$label**"
      missing_count=$((missing_count + 1))
    fi
  done

  if [ "$missing_count" -eq 1 ]; then
    problems+="- Tick the required statement $missing in the description of this pull request. If it is missing, copy it unchanged from [the pull request template]($TEMPLATE_URL): this check compares its wording word by word."$'\n'
    problem_count=$((problem_count + 1))
  elif [ "$missing_count" -gt 1 ]; then
    problems+="- Tick the required statements $missing in the description of this pull request. If they are missing, copy them unchanged from [the pull request template]($TEMPLATE_URL): this check compares their wording word by word."$'\n'
    problem_count=$((problem_count + 1))
  fi

  # The existing comment of the gate. Only comments of our bot count, so that nobody can plant
  # a comment carrying the marker.
  local comment_id comment_body=""
  comment_id=$(gh api --paginate "repos/$REPOSITORY/issues/$number/comments" \
    --jq ".[] | select(.user.login == \"$BOT_LOGIN\" and (.body | startswith(\"$MARKER\"))) | .id" | head -n 1)
  if [ -n "$comment_id" ]; then
    comment_body=$(gh api "repos/$REPOSITORY/issues/comments/$comment_id" --jq '.body')
  fi

  local failing_since
  failing_since=$(sed -nE 's/^<!-- failing-since: ([0-9TZ:-]+) -->$/\1/p' <<< "$comment_body" | head -n 1)

  # A pull request which a maintainer reopened gets a new grace period.
  if [ "$EVENT_ACTION" = "reopened" ]; then
    failing_since=""
  fi

  if [ "$problem_count" -eq 0 ]; then
    log "#$number: passed."

    # A receipt, once written, stays as it is: it records when the statements were confirmed.
    if [ -z "$comment_id" ] || [ -n "$failing_since" ]; then
      local receipt
      receipt="$MARKER
**Contribution check passed**

On $(date -u +%Y-%m-%d) at $(date -u +%H:%M) UTC, @$author confirmed the following statements in the description of this pull request, whose head was commit ${head_sha:0:7} at that time:
"
      for statement in "${REQUIRED_STATEMENTS[@]}"; do
        receipt+="
- $statement"
      done
      receipt+="

**Allow edits by maintainers** is enabled. Thank you! See [CONTRIBUTING.md]($CONTRIBUTING_URL) for what happens next."
      write_comment "$number" "$comment_id" "$comment_body" "$receipt"
    fi

    api_write -X DELETE "repos/$REPOSITORY/issues/$number/labels/$LABEL" 2>/dev/null || true
    set_status "$head_sha" success "All contribution requirements are met."
    return 0
  fi

  local now_iso now_epoch since_epoch deadline_epoch deadline_date
  now_iso=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  now_epoch=$(date -u +%s)
  failing_since="${failing_since:-$now_iso}"
  since_epoch=$(jq -rn --arg t "$failing_since" '$t | fromdateiso8601')
  deadline_epoch=$((since_epoch + GRACE_DAYS * 86400))
  deadline_date=$(jq -rn --argjson t "$deadline_epoch" '$t | todate | .[0:10]')

  if [ "$now_epoch" -ge "$deadline_epoch" ]; then
    log "#$number: closed, still failing after $GRACE_DAYS days."
    api_write -X POST "repos/$REPOSITORY/issues/$number/comments" \
      -f body="This pull request is closed because it still did not meet the contribution requirements $GRACE_DAYS days after we first pointed them out. You are welcome to open a new pull request once you can meet them; see [CONTRIBUTING.md]($CONTRIBUTING_URL)."
    api_write -X PATCH "repos/$REPOSITORY/pulls/$number" -f state=closed
    set_status "$head_sha" failure "Closed: the contribution requirements were not met in time."
    return 0
  fi

  log "#$number: action needed ($problem_count problem(s)), deadline $deadline_date."
  local notice
  notice="$MARKER
<!-- failing-since: $failing_since -->
**Contribution check: action needed**

Thank you for your pull request! Before we can review it, please take care of the following:

$problems
This check runs again whenever you edit or update this pull request, and once a day. If the requirements are still not met by $deadline_date, this pull request will be closed. See [CONTRIBUTING.md]($CONTRIBUTING_URL) for details."
  write_comment "$number" "$comment_id" "$comment_body" "$notice"

  api_write -X POST "repos/$REPOSITORY/issues/$number/labels" -f "labels[]=$LABEL" || true
  set_status "$head_sha" failure "Action needed, see the comment of the contribution check."
}

if [ "${1:-}" = "--all" ]; then
  check_all
elif [[ "${1:-}" =~ ^[0-9]+$ ]]; then
  check_one "$1"
else
  log "Usage: $0 <pull request number> | --all"
  exit 2
fi
