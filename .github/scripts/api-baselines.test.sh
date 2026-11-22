#!/usr/bin/env bash
# Tests for api-baselines.sh against a loopback server that serves made-up nuget.org version lists.
set -euo pipefail

script="$(cd "$(dirname "$0")" && pwd)/api-baselines.sh"
failures=0
work=$(mktemp -d)
root="$work/feed"

# index <package id> <json body>: the version list nuget.org would serve for the package.
index() {
  mkdir -p "$root/$1"
  printf '%s' "$2" > "$root/$1/index.json"
}

index released '{"versions":["0.1.0","0.1.1","0.2.0-beta.1","0.1.2"]}'
index ten '{"versions":["0.9.0","1.0.0","1.2.0","1.10.0","2.0.0"]}'
index prerelease-only '{"versions":["0.1.0-alpha.1"]}'
index empty '{"versions":[]}'
index broken '<html>Service unavailable</html>'
index not-a-list '{"versions":"0.1.0"}'

port=$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1])')
python3 -m http.server "$port" --bind 127.0.0.1 --directory "$root" > /dev/null 2>&1 &
server=$!
trap 'kill "$server" 2> /dev/null || true; rm -rf "$work"' EXIT
for _ in $(seq 1 50); do
  curl -s -o /dev/null "http://127.0.0.1:$port/" && break
  sleep 0.1
done
export NUGET_FLAT_CONTAINER="http://127.0.0.1:$port"

# expect <name> <expected exit code> <packing version> <ids, one per line> [expected baseline lines...]
expect() {
  local name=$1 want=$2 version=$3 ids=$4 got=0 line
  shift 4
  rm -f "$work/out.props"
  printf '%s\n' "$ids" | bash "$script" "$version" "$work/out.props" > "$work/out.txt" 2>&1 || got=$?
  local ok=true
  [ "$got" = "$want" ] || ok=false
  for line in "$@"; do
    grep -qF "$line" "$work/out.props" 2> /dev/null || ok=false
  done
  if [ "$ok" = true ]; then
    echo "ok   $name"
  else
    echo "FAIL $name: exit $got, expected $want"
    sed 's/^/     /' "$work/out.txt"
    [ -f "$work/out.props" ] && sed 's/^/     /' "$work/out.props"
    failures=$((failures + 1))
  fi
}

baseline() {
  printf "<PropertyGroup Condition=\"'\$(PackageId)' == '%s'\">" "$1"
}

expect "next patch compares with the last release" 0 0.1.3-ci released \
  "$(baseline released)" "<PackageValidationBaselineVersion>0.1.2</PackageValidationBaselineVersion>"
expect "a release compares with the one before it" 0 0.1.2 released \
  "<PackageValidationBaselineVersion>0.1.1</PackageValidationBaselineVersion>"
expect "versions compare as numbers, prereleases are skipped" 0 1.10.1-ci ten \
  "<PackageValidationBaselineVersion>1.10.0</PackageValidationBaselineVersion>"
expect "a major compares with the last release of the major before" 0 2.0.0 ten \
  "<PackageValidationBaselineVersion>1.10.0</PackageValidationBaselineVersion>"
expect "package ids are looked up in lower case" 0 0.1.3 Released \
  "$(baseline Released)" "<PackageValidationBaselineVersion>0.1.2</PackageValidationBaselineVersion>"
expect "an unpublished package is skipped" 0 0.1.3 "$(printf 'released\nnever-published\nprerelease-only\nempty')" \
  "$(baseline released)"
if grep -qE "never-published|prerelease-only|'empty'" "$work/out.props"; then
  echo "FAIL an unpublished package got a baseline"; failures=$((failures + 1))
fi
expect "no release below the packing version fails" 1 0.1.0 released
expect "a packing version that is not semver fails" 1 0.1 released
expect "an answer that is not JSON fails" 1 0.1.3 broken
expect "a version list of the wrong shape fails" 1 0.1.3 not-a-list
expect "no package ids fails" 1 0.1.3 ""
kill "$server"; wait "$server" 2> /dev/null || true
expect "an unreachable feed fails" 1 0.1.3 released

if [ "$failures" -gt 0 ]; then
  echo "$failures case(s) failed." >&2
  exit 1
fi
echo "All cases passed."
