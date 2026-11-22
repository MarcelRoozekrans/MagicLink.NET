#!/usr/bin/env bash
# Looks up the last released version of each package, the version package validation compares the
# public API against (ADR-252 of the product line), and writes it as an MSBuild file.
#
# Usage: api-baselines.sh <packing-version> <output-props> < package-ids
#
# The baseline of a package is the highest stable version on nuget.org whose major.minor.patch is
# below the packing version's. CI packs a pull request at the next patch, so the baseline is the
# last release; the release pull request packs the version it releases, which is not on nuget.org
# yet, so its baseline is the last release too.
#
# A package with no stable version on nuget.org is new and has nothing to compare against: it is
# skipped with a message. Any other failure stops the script, because treating it as "no baseline"
# would switch the check off without a trace. NUGET_FLAT_CONTAINER overrides the service root.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <packing-version> <output-props> < package-ids" >&2
  exit 2
fi

packing_version=$1
output=$2
flat_container=${NUGET_FLAT_CONTAINER:-https://api.nuget.org/v3-flatcontainer}

# major.minor.patch as one sortable number; a prerelease or build part is ignored.
sort_key() {
  local major minor patch
  IFS=. read -r major minor patch <<< "${1%%[-+]*}"
  echo $(( 10#$major * 1000000000000 + 10#$minor * 1000000 + 10#$patch ))
}

if ! [[ $packing_version =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$ ]]; then
  echo "::error::The packing version $packing_version is not a semantic version." >&2
  exit 1
fi
packing_key=$(sort_key "$packing_version")

body=$(mktemp)
trap 'rm -f "$body"' EXIT
groups=""
read_any=false

while IFS= read -r id || [ -n "$id" ]; do
  id=${id%$'\r'}
  [ -n "$id" ] || continue
  read_any=true
  url="$flat_container/$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')/index.json"

  if ! code=$(curl -sS --connect-timeout 15 --max-time 60 --retry 3 --retry-delay 2 -o "$body" -w '%{http_code}' "$url"); then
    echo "::error::Could not reach $url to look up the API baseline of $id." >&2
    exit 1
  fi
  case "$code" in
    200) ;;
    404)
      echo "$id has no release on nuget.org yet, so its API is not compared with one."
      continue
      ;;
    *)
      echo "::error::nuget.org answered HTTP $code for $url, so the API baseline of $id is unknown." >&2
      exit 1
      ;;
  esac

  # An answer that is not a list of version strings says nothing about what was released.
  if ! versions=$(jq -r '.versions | if type == "array" and all(.[]; type == "string") then .[] else error("not a version list") end' "$body" 2> /dev/null); then
    echo "::error::nuget.org answered $url with something other than a version list, so the API baseline of $id is unknown." >&2
    exit 1
  fi

  baseline=""
  baseline_key=-1
  has_stable=false
  while IFS= read -r version; do
    # Stable versions only: a prerelease is not a release the API has to stay compatible with.
    [[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || continue
    has_stable=true
    key=$(sort_key "$version")
    if [ "$key" -lt "$packing_key" ] && [ "$key" -gt "$baseline_key" ]; then
      baseline=$version
      baseline_key=$key
    fi
  done <<< "$versions"

  if [ "$has_stable" = false ]; then
    echo "$id has no stable release on nuget.org yet, so its API is not compared with one."
    continue
  fi
  if [ -z "$baseline" ]; then
    echo "::error::$id has stable releases on nuget.org, but none below $packing_version, so there is no baseline to compare its API with." >&2
    exit 1
  fi

  echo "$id: API baseline $baseline"
  groups+="  <PropertyGroup Condition=\"'\$(PackageId)' == '$id'\">"$'\n'
  groups+="    <PackageValidationBaselineVersion>$baseline</PackageValidationBaselineVersion>"$'\n'
  groups+="  </PropertyGroup>"$'\n'
done

if [ "$read_any" = false ]; then
  echo "::error::No package ids were read, so no API baseline was looked up." >&2
  exit 1
fi

mkdir -p "$(dirname "$output")"
{
  echo '<?xml version="1.0" encoding="utf-8"?>'
  echo "<!-- Written by .github/scripts/api-baselines.sh for packing version $packing_version. -->"
  echo '<Project>'
  printf '%s' "$groups"
  echo '</Project>'
} > "$output"
