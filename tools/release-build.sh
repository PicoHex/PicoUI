#!/usr/bin/env bash
# =============================================================================
# PicoUI — release build guard
#
# RULE: `main` is the only release branch. This script refuses to produce a
#       release artifact from any other branch, from a detached HEAD, or from a
#       dirty working tree. See docs/releasing.md.
#
# WHAT THIS REPOSITORY RELEASES
#   PicoUI is a .NET library repository, not an application: there is no
#   executable to ship. The release product is the four NuGet packages (plus
#   their .snupkg symbol packages) packed from src/ — exactly the projects
#   carrying <IsPackable>true</IsPackable>:
#
#       src/PicoMarkdown  -> PicoMarkdown.<version>.nupkg + .snupkg
#       src/PicoMermaid   -> PicoMermaid.<version>.nupkg  + .snupkg
#       src/PicoTui       -> PicoTui.<version>.nupkg      + .snupkg
#       src/PicoHtmx      -> PicoHtmx.<version>.nupkg     + .snupkg
#
#   The tag CI publishes to NuGet is packed by exactly this set (see
#   .github/workflows/release.yml), so a local run reproduces the release
#   payload rather than a look-alike.
#
# WHY THE GUARD EXISTS
#   A release was once cut from a side branch: the packages were 69 commits
#   behind main while their file timestamps and version string looked brand
#   new. Nothing in the artifact said "stale" — only a branch check does.
#   This script is that check, made mechanical.
#
# WHAT IT ENFORCES
#   R1  branch must be `main`       -> else exit 2 (prints the current branch)
#   R2  working tree must be clean  -> else exit 3 (prints up to 3 dirty paths)
#   R3  prints HEAD sha, branch, UTC time
#   R4  every artifact lands OUTSIDE the repo, in
#         OUT=${OUT:-$TMPDIR/rel-$(git rev-parse --short HEAD)}
#       bin/obj are redirected there as well (--artifacts-path), so the working
#       tree stays byte-for-byte untouched: zero in-repo residue.
#   R5  prints sha256 of every .nupkg (or lists the output dir + sizes)
#   R6  any restore/build/test/pack failure -> non-zero exit (4)
#
# ENVIRONMENT
#   OUT         output directory; must resolve outside the repository
#   VERSION     package version; defaults to 0.0.0-dev (a non-tag build)
#   SKIP_TESTS  set to 1 to skip the test gate (not recommended for a release)
#
# EXIT CODES
#   0  release build succeeded
#   2  not on main — side branch, detached HEAD, or not a git repository
#   3  working tree is dirty
#   4  build/test/pack failed
#   5  OUT resolves inside the repository
# =============================================================================

set -uo pipefail

say()  { printf '%s\n' "$*"; }
note() { printf '  %s\n' "$*"; }

refuse() { # refuse <exit-code> <reason>
    local code="$1"; shift
    printf '\nREFUSED (exit %s): %s\n' "$code" "$*" >&2
    exit "$code"
}

# ---------------------------------------------------------------- locate repo
git rev-parse --show-toplevel >/dev/null 2>&1 \
    || refuse 2 "not a git repository — cannot prove this tree is 'main'."
cd "$(git rev-parse --show-toplevel)" || refuse 2 "cannot enter repository root."
REPO_ROOT=$(pwd -P)

say "PicoUI release build"
say "repository: $REPO_ROOT"

# ------------------------------------------------------- R1: main is the only
# `main` is the only release branch. Detached HEAD is refused too: it carries no
# branch name, so it can never be shown to be main.
branch=$(git branch --show-current)
if [ -z "$branch" ]; then
    refuse 2 "detached HEAD — releases may only be cut from 'main'."
fi
if [ "$branch" != "main" ]; then
    refuse 2 "on branch '$branch' — releases may only be cut from 'main'."
fi
say "branch:     $branch (ok)"

# ------------------------------------------------------------- R2: clean tree
# A release must be reproducible from a commit. Uncommitted edits are not in the
# object database, so whatever they change would ship unversioned.
dirty=$(git status --porcelain)
if [ -n "$dirty" ]; then
    printf '\nREFUSED (exit 3): working tree is dirty — commit or stash first.\n' >&2
    printf '%s\n' "$dirty" | head -n 3 | sed 's/^/    /' >&2
    total=$(printf '%s\n' "$dirty" | wc -l | tr -d ' ')
    if [ "$total" -gt 3 ]; then
        printf '    ... and %s more path(s)\n' "$((total - 3))" >&2
    fi
    exit 3
fi
say "tree:       clean (ok)"

# ------------------------------------------- R3: build provenance, printed
HEAD_SHA=$(git rev-parse HEAD)
HEAD_SHORT=$(git rev-parse --short HEAD)
UTC_NOW=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
VERSION=${VERSION:-0.0.0-dev}

say "HEAD:       $HEAD_SHA"
say "branch:     $branch"
say "UTC time:   $UTC_NOW"
say "version:    $VERSION"

# ------------------------------------- R4: artifacts live outside the repo
: "${TMPDIR:=${TEMP:-/tmp}}"
OUT=${OUT:-$TMPDIR/rel-$HEAD_SHORT}

# Canonicalise OUT *without* touching the filesystem: the containment check must
# run before anything is created, otherwise an OUT inside the repository would
# already have left a directory behind by the time it is refused.
absolute_path() {
    if command -v realpath >/dev/null 2>&1 && realpath -m . >/dev/null 2>&1; then
        realpath -m "$1"
    else
        # Fallback: resolve through the nearest existing ancestor, creating nothing.
        local p=$1 suffix=''
        while [ ! -d "$p" ] && [ "$p" != "/" ]; do
            suffix="/$(basename "$p")$suffix"
            p=$(dirname "$p")
        done
        printf '%s\n' "$(cd "$p" && pwd -P)$suffix"
    fi
}

OUT=$(absolute_path "$OUT")

# Writing the release payload into the repository is how a "release" becomes a
# working-tree change nobody owns. Refuse it loudly, before creating anything.
case "$OUT" in
    "$REPO_ROOT" | "$REPO_ROOT"/*)
        refuse 5 "OUT ($OUT) is inside the repository — artifacts must land outside it."
        ;;
esac

mkdir -p "$OUT" || refuse 4 "cannot create output directory: $OUT"

WORK="$OUT/.work"       # bin/obj live here -> never in the repo's bin/obj
PKGS="$OUT/packages"

# A release build must not reuse stale outputs from an earlier run at the same
# commit. Start from an empty payload.
rm -rf "$WORK" "$PKGS"
mkdir -p "$PKGS"

say "output:     $OUT"

# ------------------------------------------------------------------ build
# Pack per project (not via the solution) for the reason recorded in ci.yml: a
# solution build dispatches the out-of-solution sibling projects with their own
# configuration, so PicoUI Release outputs would link Debug siblings.
#
# UseSiblingProjectReferences=false keeps PicoHtmx on its pinned PicoNode
# packages instead of the sibling checkout — the dependency graph release.yml
# ships, and the only one that cannot write object files into the *other*
# repository.
pack_args=(-c Release -o "$PKGS" -p:Version="$VERSION" -p:UseSiblingProjectReferences=false --artifacts-path "$WORK")

shopt -s nullglob
projects=(src/*/*.csproj)
[ ${#projects[@]} -gt 0 ] || refuse 4 "no packages to build: src/*/*.csproj matched nothing."

if [ "${SKIP_TESTS:-0}" != "1" ]; then
    say ""
    say "== test gate =="
    tests=(tests/*/*.Tests.csproj)
    [ ${#tests[@]} -gt 0 ] || refuse 4 "no test projects found: tests/*/*.Tests.csproj matched nothing."
    for proj in "${tests[@]}"; do
        say "-- test $proj"
        if ! dotnet test --project "$proj" -c Release --artifacts-path "$WORK"; then
            refuse 4 "test gate failed — refusing to produce a release artifact."
        fi
    done
else
    say ""
    say "!! SKIP_TESTS=1 — test gate skipped; this is not a releasable payload. !!"
fi

say ""
say "== pack =="
for proj in "${projects[@]}"; do
    say "-- pack $proj"
    if ! dotnet pack "$proj" "${pack_args[@]}"; then
        refuse 4 "pack failed for $proj — see the output above. Nothing was released."
    fi
done

# ------------------------------------------------------- R5: fingerprints
say ""
say "== release artifacts =="

sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | awk '{print $1}'
    else
        return 1
    fi
}

nupkgs=("$PKGS"/*.nupkg)
if [ ${#nupkgs[@]} -eq 0 ]; then
    say "no .nupkg was produced — listing the output directory instead:"
    ls -l "$OUT" 2>/dev/null || true
    refuse 4 "build reported success but produced no package."
fi

for pkg in "${nupkgs[@]}"; do
    note "$(basename "$pkg")"
    if digest=$(sha256_of "$pkg"); then
        note "  sha256  $digest"
    else
        note "  sha256  (no sha256sum/shasum on this host)"
    fi
    note "  size    $(wc -c < "$pkg" | tr -d ' ') bytes"
done

snupkgs=("$PKGS"/*.snupkg)
say ""
say "packages:   ${#nupkgs[@]} nupkg + ${#snupkgs[@]} snupkg"
say "output:     $PKGS"
say "work dir:   $WORK"
say ""
say "release build OK — HEAD $HEAD_SHA on $branch, $UTC_NOW"
say "record the sha256 values above in the release notes before tagging."
exit 0
