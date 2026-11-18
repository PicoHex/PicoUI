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
#   R7  every PicoMarkdown/PicoMermaid dependency declared inside the packed
#       .nupkg files must equal VERSION -> else exit 1
#   R8  HEAD must BE the tip of origin/main, not merely an ancestor -> else exit 1
#       (F10b/F9-5: "behind main" and "not on main" are the same defect — the
#       payload is not what main says today, and nothing in the artifact says so.)
#
# R8 AND .github/workflows/release.yml:59-72 (one rule, two halves, no overlap)
#   workflow    owns the REMOTE half: it has a freshly fetched remote and compares
#               the tagged sha against origin/main. A tag build is a detached HEAD,
#               so R1 cannot even run there.
#   this script owns the LOCAL half: R1 (branch == main, no detached HEAD), R2
#               (clean tree) and R8 against the *local* refs/remotes/origin/main.
#               It never fetches and never pushes — it only reads what is already
#               there, which is why staging moved to the workflow.
#   R8 reproduces the workflow's comparison exactly:
#       [ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || exit 1
#   Equality covers "behind main" as well as "not on main": a commit that is not
#   on main cannot equal its tip, and an ancestor is not the tip either (the old
#   workflow check was `merge-base --is-ancestor`, which accepted every commit on
#   main's history — that is how a release shipped 69 commits behind main).
#
# REPRODUCE R8 WITHOUT PACKING ANYTHING
#   bash tools/release-build.sh --check-main-tip
#     exit 0  HEAD == refs/remotes/origin/main
#     exit 1  HEAD is not the tip (ahead/behind counts printed)
#     exit 2  refs/remotes/origin/main is unknown — fetch it; this script is offline
#   It runs only R8: no restore, build, test or pack, so it is safe in a dirty tree
#   and on any branch.
#
# ENVIRONMENT
#   OUT         output directory; must resolve outside the repository
#   VERSION     package version; defaults to 0.0.0-dev (a non-tag build)
#   SKIP_TESTS  set to 1 to skip the test gate (not recommended for a release)
#
# EXIT CODES
#   0  release build succeeded
#   1  HEAD is not the tip of origin/main (R8), or a packed package declares a
#      sibling dependency != VERSION (R7)
#   2  not on main — side branch, detached HEAD, not a git repository — or
#      origin/main is unknown, so R8 cannot be evaluated
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

MODE=release
case "${1:-}" in
    --check-main-tip) MODE=check-main-tip ;;
    "") ;;
    *) refuse 2 "unknown argument '$1' (only --check-main-tip is supported)" ;;
esac

# ---------------------------------------------------------------- locate repo
git rev-parse --show-toplevel >/dev/null 2>&1 \
    || refuse 2 "not a git repository — cannot prove this tree is 'main'."
cd "$(git rev-parse --show-toplevel)" || refuse 2 "cannot enter repository root."
REPO_ROOT=$(pwd -P)

if [ "$MODE" = "check-main-tip" ]; then
    say "PicoUI release guard — origin/main tip check only (no build, no pack, no push)"
else
    say "PicoUI release build"
fi
say "repository: $REPO_ROOT"

# ------------------------------------------------------------ R8: main-tip gate
# One implementation, two callers: the release path below, and
# `--check-main-tip` (which runs *only* this — no git state is changed, nothing is
# built, nothing is pushed, so it is safe on any branch and in a dirty tree).
#
# Rule (identical to .github/workflows/release.yml:59-72): the release commit must
# BE the tip of origin/main — not an ancestor of it. `ahead`/`behind` are printed so
# a refusal is diagnosable instead of just "not main".
origin_main_sha() { git rev-parse -q --verify refs/remotes/origin/main 2>/dev/null; }

check_main_tip() { # -> 0 equal | 1 not the tip | 2 origin/main unknown
    local head_sha main_sha ahead behind
    head_sha=$(git rev-parse HEAD 2>/dev/null) || return 2
    main_sha=$(origin_main_sha) || true
    if [ -z "$main_sha" ]; then
        printf 'REFUSED (exit 2): refs/remotes/origin/main is unknown here — this check cannot be evaluated. Fetch it first:\n    git fetch --no-tags origin +refs/heads/main:refs/remotes/origin/main\n' >&2
        return 2
    fi
    if [ "$head_sha" != "$main_sha" ]; then
        ahead=$(git rev-list --count "$main_sha..$head_sha" 2>/dev/null) || ahead='?'
        behind=$(git rev-list --count "$head_sha..$main_sha" 2>/dev/null) || behind='?'
        printf 'REFUSED (exit 1): HEAD %s is not the tip of origin/main (%s) — %s ahead, %s behind. Releases are only cut from the current tip of main.\n' \
            "$head_sha" "$main_sha" "$ahead" "$behind" >&2
        return 1
    fi
    return 0
}

if [ "$MODE" = "check-main-tip" ]; then
    say "PicoUI main-tip check (R8 of the release guard; mirrors .github/workflows/release.yml:59-72)"
    say "HEAD:        $(git rev-parse HEAD)"
    main_sha=$(origin_main_sha) || true
    say "origin/main: ${main_sha:-<unknown>}"
    check_main_tip
    code=$?
    if [ "$code" -eq 0 ]; then
        say "OK — HEAD is the tip of origin/main (exit 0)"
    else
        say "HEAD is NOT the tip of origin/main (exit $code)"
    fi
    exit "$code"
fi

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

# --------------------------------------------------------- R8: is this main's tip?
# The release invariants are checked before anything is built: a stale payload that
# packs fine is the failure mode this whole guard exists for.
say ""
say "== origin/main tip gate =="
check_main_tip
main_tip_code=$?
if [ "$main_tip_code" -ne 0 ]; then
    refuse "$main_tip_code" "this tree is not the tip of origin/main — pack nothing, release nothing."
fi
note "HEAD $(git rev-parse --short HEAD) is the tip of origin/main (ok)"

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

# --------------------------------------------- the local feed must exist first
# NuGet.config declares its `local` source as a RELATIVE path (`artifacts/nupkg`,
# resolved against the config file, i.e. $REPO_ROOT/artifacts/nupkg) and that
# directory is not in git. NuGet does not ignore a missing local source -- it is
# a hard NU1301 on every restore:
#     error NU1301: The local source '<repo>/artifacts/nupkg' doesn't exist.
# So a fresh clone could not restore at all, and this script failed at its very
# first restore (the test gate below) with exit 4 before it ever packed anything.
#
# The two workflows create that directory themselves (ci.yml:72, release.yml:87);
# this is the same directory for the LOCAL half -- the path those workflows never
# run. `artifacts/` is gitignored (.gitignore:65), so creating it leaves the
# working tree clean and R2 above still passes on the next run.
#
# This is NOT $PKGS and does not replace it: --artifacts-path moves bin/obj and
# the packed output outside the tree, but which sources a restore consults is
# fixed by nuget.config, so the in-repo directory has to exist even though this
# script writes nothing into it.
mkdir -p "$REPO_ROOT/artifacts/nupkg" \
    || refuse 4 "cannot create the local NuGet source $REPO_ROOT/artifacts/nupkg -- nuget.config declares it as a relative source and NuGet fails with NU1301 when it is absent."

# ------------------------------------------------------------------ build
# Pack per project (not via the solution) for the reason recorded in ci.yml: it
# keeps the restore and the Release configuration in one explicit place.
#
# Cross-repo dependencies (PicoNode.Http / PicoNode.Web / PicoWeb) are always
# packages, so nothing here can build or write into the PicoNode repository;
# that is the dependency graph release.yml ships.
#
# UseProjectReferences=false is the second half of that: the sibling PicoUI
# modules (PicoMarkdown/PicoMermaid) are consumed as PACKAGES, so their version
# in the nuspec comes from $(PicoMarkdownVersion)/$(PicoMermaidVersion) instead
# of from a project reference. That is the same path release.yml packs, which is
# the point: what this script verifies is what the pipeline ships. The versions
# are injected at VERSION, and the core packages are packed FIRST so phase 2 can
# restore them from $PKGS (RestoreAdditionalProjectSources).
pack_args=(-c Release -o "$PKGS" -p:Version="$VERSION"
           -p:UseProjectReferences=false
           -p:PicoMarkdownVersion="$VERSION"
           -p:PicoMermaidVersion="$VERSION"
           -p:RestoreAdditionalProjectSources="$PKGS"
           --artifacts-path "$WORK")

shopt -s nullglob
projects=(src/*/*.csproj)
[ ${#projects[@]} -gt 0 ] || refuse 4 "no packages to build: src/*/*.csproj matched nothing."

# Phase 1 of two. Keep in step with .github/workflows/release.yml.
sibling_cores=(PicoMarkdown PicoMermaid)

if [ "${SKIP_TESTS:-0}" != "1" ]; then
    say ""
    say "== test gate =="

    # Tests that locate the repository by walking up from AppContext.BaseDirectory
    # cannot do it here: --artifacts-path moves bin/obj — and with them the test
    # assembly — into $WORK, which is outside the tree, so the upward search finds
    # no src/. That made one source-scan test fail on every TFM and left this gate
    # permanently red: the only local way to reach the pack step was SKIP_TESTS=1.
    # The repository root is already known here ($REPO_ROOT), so hand it over
    # instead of asking the tests to rediscover it.
    export PICOUI_REPO_ROOT="$REPO_ROOT"

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

# Phase 1: the sibling cores, which nothing in this repository depends on.
for core in "${sibling_cores[@]}"; do
    proj="src/$core/$core.csproj"
    [ -f "$proj" ] \
        || refuse 4 "expected the sibling core project $proj — cannot build a consistent payload."
    say "-- pack $proj (phase 1)"
    if ! dotnet pack "$proj" "${pack_args[@]}"; then
        refuse 4 "pack failed for $proj — see the output above. Nothing was released."
    fi
done

# Phase 2: everything else. PicoTui consumes phase-1 packages at $VERSION.
for proj in "${projects[@]}"; do
    case "$(basename "$proj" .csproj)" in
        PicoMarkdown | PicoMermaid) continue ;;
    esac
    say "-- pack $proj (phase 2)"
    if ! dotnet pack "$proj" "${pack_args[@]}"; then
        refuse 4 "pack failed for $proj — see the output above. Nothing was released."
    fi
done

# ------------------------------ R7: sibling dependency versions == VERSION
# A package whose nuspec pins its sibling at another version still restores
# "successfully" — onto an older DLL — and no build, test or publish step
# notices. PicoTui 2026.9.0 shipped exactly that: -p:UseProjectReferences=false
# took the dependency version from a constant in Directory.Packages.props, not
# from the tag, so the next tag would have depended on 2026.9.0 forever.
# The pack above injects -p:PicoMarkdownVersion/-p:PicoMermaidVersion; this gate
# reads the finished packages back and refuses a payload that disagrees.
say ""
say "== sibling dependency gate =="

command -v unzip >/dev/null 2>&1 \
    || refuse 4 "unzip is required to read the nuspec out of each .nupkg — install it and re-run."

nupkgs=("$PKGS"/*.nupkg)
if [ ${#nupkgs[@]} -eq 0 ]; then
    say "no .nupkg was produced — listing the output directory instead:"
    ls -l "$OUT" 2>/dev/null || true
    refuse 4 "build reported success but produced no package."
fi

sibling_deps_of() { # stdin: one nuspec -> "<id> <version>" per sibling dependency
    grep -E '<dependency[^>]*id="Pico(Markdown|Mermaid)"' | while IFS= read -r line; do
        id=$(printf '%s' "$line" | sed -E 's/^.*id="([^"]*)".*$/\1/')
        ver=$(printf '%s' "$line" | sed -E 's/^.*version="([^"]*)".*$/\1/')
        case "$ver" in
            *'<'*) ver='(no version attribute)' ;;
        esac
        printf '%s %s\n' "$id" "$ver"
    done
}

dependency_mismatch=0
for pkg in "${nupkgs[@]}"; do
    name=$(basename "$pkg")
    nuspec=$(unzip -p "$pkg" '*.nuspec' 2>/dev/null) \
        || refuse 4 "cannot read the nuspec inside $name — refusing a payload whose dependency versions are unverified."
    deps=$(printf '%s\n' "$nuspec" | sibling_deps_of)
    declared=$(printf '%s\n' "$deps" | awk 'NF {print $1}')
    while read -r id ver; do
        [ -n "$id" ] || continue
        # tolerate the '[x.y.z]' exact-range form before comparing
        bare=${ver#\[}
        bare=${bare%\]}
        if [ "$bare" != "$VERSION" ]; then
            printf '    %s: %s = %s (expected %s)\n' "$name" "$id" "$ver" "$VERSION" >&2
            dependency_mismatch=1
        else
            note "$name: $id = $ver (ok)"
        fi
    done <<< "$deps"
    # The consuming package must actually declare both siblings, otherwise a pack
    # that silently dropped them would pass this gate vacuously.
    case "$name" in
        PicoTui.*)
            for expected in "${sibling_cores[@]}"; do
                if ! printf '%s\n' "$declared" | grep -qx "$expected"; then
                    printf '    %s: no %s dependency\n' "$name" "$expected" >&2
                    dependency_mismatch=1
                fi
            done
            ;;
    esac
done

if [ "$dependency_mismatch" -ne 0 ]; then
    refuse 1 "the payload's sibling dependencies do not equal VERSION=$VERSION — see above. Nothing was released."
fi
note "every sibling dependency is $VERSION (ok)"

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
