#!/usr/bin/env bash
# Tests for check-breaking-suppression.sh: each case builds a small repository with a base commit and
# a pull request's commits, runs the check and compares its exit code with the expected one.
set -euo pipefail

script="$(cd "$(dirname "$0")" && pwd)/check-breaking-suppression.sh"
failures=0
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

entry() {
  printf '  <Suppression>\n    <DiagnosticId>CP0002</DiagnosticId>\n    <Target>%s</Target>\n  </Suppression>\n' "$1"
}

suppressions() {
  printf '<?xml version="1.0" encoding="utf-8"?>\n<Suppressions>\n'
  for target in "$@"; do entry "$target"; done
  printf '</Suppressions>\n'
}

# new_repo: a repository whose base commit has src/Pkg/Code.cs, and optionally a suppression file.
new_repo() {
  local dir="$work/$1"; shift
  git init -q "$dir"
  git -C "$dir" config user.email test@example.com
  git -C "$dir" config user.name test
  mkdir -p "$dir/src/Pkg"
  echo 'class C {}' > "$dir/src/Pkg/Code.cs"
  if [ "$#" -gt 0 ]; then suppressions "$@" > "$dir/src/Pkg/CompatibilitySuppressions.xml"; fi
  git -C "$dir" add -A
  git -C "$dir" commit -q -m "chore: base"
  echo "$dir"
}

commit() {
  local dir=$1; shift
  git -C "$dir" add -A
  git -C "$dir" commit -q --allow-empty "$@"
}

# expect <name> <expected exit code> <repo> <base> <title>
expect() {
  local name=$1 want=$2 dir=$3 base=$4 title=$5 got=0
  (cd "$dir" && bash "$script" "$base" HEAD "$title") > "$work/out.txt" 2>&1 || got=$?
  if [ "$got" = "$want" ]; then
    echo "ok   $name"
  else
    echo "FAIL $name: exit $got, expected $want"
    sed 's/^/     /' "$work/out.txt"
    failures=$((failures + 1))
  fi
}

# 1. No suppression file touched: passes whatever the commits say.
d=$(new_repo plain); b=$(git -C "$d" rev-parse HEAD)
echo 'class D {}' > "$d/src/Pkg/Code.cs"; commit "$d" -m "feat: add D"
expect "no suppression change" 0 "$d" "$b" "feat: add D"

# 2. A new suppression file with no breaking commit fails.
d=$(new_repo added); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "feat: remove Gone"
expect "added file, no breaking commit" 1 "$d" "$b" "feat: remove Gone"

# 3. The same with a ! commit and a ! title passes.
d=$(new_repo added-bang); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "feat!: remove Gone"
expect "added file, ! commit and title" 0 "$d" "$b" "feat!: remove Gone"

# 4. A BREAKING CHANGE: footer counts as the breaking commit; the title still needs the !.
d=$(new_repo added-footer); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"
commit "$d" -m "feat(core): remove Gone" -m "BREAKING CHANGE: Gone is removed."
expect "added file, footer and ! title" 0 "$d" "$b" "feat(core)!: remove Gone"

# 5. A breaking commit but a title without the marker fails: a squash merge would lose the marker.
expect "breaking commit, plain title" 1 "$d" "$b" "feat(core): remove Gone"

# 6. A breaking title alone, with no breaking commit, fails.
d=$(new_repo title-only); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "feat: remove Gone"
expect "! title, no breaking commit" 1 "$d" "$b" "feat!: remove Gone"

# 7. Extending an existing file needs the marker too.
d=$(new_repo extended "M:C.Old"); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Old" "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "fix: remove Gone"
expect "extended file, no breaking commit" 1 "$d" "$b" "fix: remove Gone"

# 8. Removing entries, or the whole file, once the break is the baseline needs no marker.
d=$(new_repo shrunk "M:C.Old" "M:C.Older"); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Old" > "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "chore: drop a suppression"
expect "entries removed" 0 "$d" "$b" "chore: drop a suppression"
d=$(new_repo deleted "M:C.Old"); b=$(git -C "$d" rev-parse HEAD)
rm "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "chore: drop the suppressions"
expect "file deleted" 0 "$d" "$b" "chore: drop the suppressions"

# 9. "BREAKING CHANGE" only in the subject is not a footer, and "!" after the colon is not a marker.
d=$(new_repo fake-markers); b=$(git -C "$d" rev-parse HEAD)
suppressions "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"
commit "$d" -m "feat: BREAKING CHANGE: remove Gone!"
expect "markers in the wrong place" 1 "$d" "$b" "feat: remove Gone!"

# 10. The base may be behind main's later commits: only the pull request's own commits count.
d=$(new_repo merge-base); b=$(git -C "$d" rev-parse HEAD)
git -C "$d" checkout -q -b pr
suppressions "M:C.Gone" > "$d/src/Pkg/CompatibilitySuppressions.xml"; commit "$d" -m "feat: remove Gone"
git -C "$d" checkout -q -
commit "$d" -m "feat!: an unrelated break on main"
nb=$(git -C "$d" rev-parse HEAD); git -C "$d" checkout -q pr
expect "breaking commit only on the base branch" 1 "$d" "$nb" "feat!: remove Gone"

if [ "$failures" -gt 0 ]; then
  echo "$failures case(s) failed." >&2
  exit 1
fi
echo "All cases passed."
