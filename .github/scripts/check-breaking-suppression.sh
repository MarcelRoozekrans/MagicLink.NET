#!/usr/bin/env bash
# Fails a pull request that adds or extends an API compatibility suppression without marking the
# break (product-line ADR-252). A suppression accepts a break of the public API, and release-please
# takes the version from the commits, so the break needs a commit marked as breaking: "!" before
# the colon, or a "BREAKING CHANGE:" footer. A squash merge turns the pull request title into the
# commit on main, so the title needs the "!" as well.
#
# Usage: check-breaking-suppression.sh <base-sha> <head-sha> <pull-request-title>
# Run it inside the repository, with the history of both commits fetched.
#
# Removing entries or a whole file, which is due once the release with the break is the baseline,
# needs no marker.
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: $0 <base-sha> <head-sha> <pull-request-title>" >&2
  exit 2
fi

base=$1
head=$2
title=$3
suppression_files=':(glob)**/CompatibilitySuppressions*.xml'
breaking_subject='^[a-z]+(\([^)]*\))?!: '

# Lines the pull request adds to a suppression file, compared with where it branched off.
added=$(git diff --no-ext-diff --unified=0 "$base...$head" -- "$suppression_files" \
  | grep -E '^\+' | grep -vE '^\+\+\+ ' | grep -E '[^[:space:]+]' || true)

if [ -z "$added" ]; then
  echo "No API compatibility suppression was added or extended."
  exit 0
fi

echo "This pull request adds or extends an API compatibility suppression:"
git diff --no-ext-diff --stat "$base...$head" -- "$suppression_files"

breaking_commit=""
while IFS= read -r sha; do
  [ -n "$sha" ] || continue
  subject=$(git log -1 --format=%s "$sha")
  body=$(git log -1 --format=%b "$sha")
  if [[ $subject =~ $breaking_subject ]] || printf '%s\n' "$body" | grep -qE '^BREAKING[ -]CHANGE: '; then
    breaking_commit="$sha $subject"
    break
  fi
done < <(git rev-list "$base..$head")

failed=false
if [ -z "$breaking_commit" ]; then
  echo "::error::No commit in this pull request is marked as breaking. A suppression accepts a break of the public API, so mark a commit with ! before the colon or a BREAKING CHANGE: footer."
  failed=true
else
  echo "Breaking commit: $breaking_commit"
fi
if ! [[ $title =~ $breaking_subject ]]; then
  echo "::error::The pull request title is not marked as breaking. A squash merge makes the title the commit on main, so it needs the ! as well, for example feat!: ..."
  failed=true
fi

if [ "$failed" = true ]; then
  exit 1
fi
echo "The break is marked in a commit and in the title."
