# Releasing PicoUI

**`main` is the only release branch. `tools/release-build.sh` is the only
supported way to produce a release artifact.**

## The rule

- Releases are cut from `main` — never from a side branch, never from a
  detached HEAD.
- Run `tools/release-build.sh`; do not `dotnet pack` by hand to make something
  you intend to publish.
- The script refuses (exit `2`) unless the branch is `main` and (exit `3`)
  unless the working tree is clean, so "just this once, from my branch" is not
  available.

## What a PicoUI release actually is

This repository ships **libraries**, not an application — there is no
executable. The release product is the four NuGet packages (plus their `.snupkg`
symbol packages) packed from the projects that carry
`<IsPackable>true</IsPackable>`:

| Project | Package |
|---|---|
| `src/PicoMarkdown` | `PicoMarkdown.<version>.nupkg` |
| `src/PicoMermaid` | `PicoMermaid.<version>.nupkg` |
| `src/PicoTui` | `PicoTui.<version>.nupkg` |
| `src/PicoHtmx` | `PicoHtmx.<version>.nupkg` |

One tag releases all of them. `.github/workflows/release.yml` runs the tests,
packs the same four projects at the tag version, publishes to NuGet and creates
the GitHub release. `tools/release-build.sh` reproduces that payload locally, so
what you check before tagging is what the pipeline ships.

The script writes everything outside the repository, to
`${OUT:-$TMPDIR/rel-<short-sha>}` (`bin/` and `obj/` included), and prints the
sha256 of every `.nupkg`. Copy those digests into the release notes — they are
the only way to tell two builds of the same version apart.

## Before you tag — three self-checks

1. **You are on the commit you think you are.** `git rev-parse --abbrev-ref HEAD`
   prints `main`, and `git log --oneline origin/main..HEAD` prints nothing
   (nothing unpushed, nothing local-only).
2. **The tree is clean and tests pass.**
   `git status --porcelain` is empty, and
   `bash tools/release-build.sh` exits `0` — the script runs all four test
   projects as a gate before it packs anything.
3. **CI is green on that exact commit.** `gh run list --branch main --limit 1`
   shows a successful run for the sha the script printed. A local green build
   does not cover the five-RID AOT matrix.

Only then create the annotated tag (`git tag -a v<year>.<x>.<y>`) and push it.

## A local AOT publish rewrites the lock graph — do not commit it

`packages.lock.json` is a reviewed artifact: it is what makes the y-floating
versions in `Directory.Packages.props` reproducible, and
`.github/workflows/deps-refresh.yml` — not a build and not a developer — is the
only thing allowed to move it.

**A plain `dotnet publish` with a RID breaks that rule silently.** `-r <rid>`
together with `-p:PublishAot=true` reshapes the restore graph (it pulls in
`Microsoft.DotNet.ILCompiler` and the RID-specific runtime packs), and the
implicit restore writes that reshaped graph straight back into the committed
lock file. One lock file can only describe the RID-less default build, so the
rewrite is wrong for every other platform — and because it arrives as a normal
` M` line after a successful publish, it looks like ordinary build output.
This is not hypothetical: an unflagged AOT publish rewrites the lock of the
sample it publishes plus the lock of the library that sample references (two per
sample), so publishing all four samples on a clean checkout rewrites **8 of the
12** committed locks into a RID + ILCompiler shape — byte-for-byte identical to
the `b4d6423` regression that had to be reverted. CI then fails `NU1004`
(`the project's runtime identifiers have changed`) on four of its five RIDs.

Keep the RID graph out of the repository by publishing with a redirected lock
path:

```bash
dotnet publish samples/PicoHtmx.Sample -c Release -r win-x64 -p:PublishAot=true \
  -p:NuGetLockFilePath=obj/publish-graph.lock.json
```

`obj/` is git-ignored, so the reshaped graph lands beside the other build output
and the committed locks stay exactly as reviewed. Replace the sample and the RID
to publish something else (the four samples are `PicoMarkdown.Sample`,
`PicoMermaid.Sample`, `PicoTui.Sample`, `PicoHtmx.Sample`).

`-p:RestorePackagesWithLockFile=false` is **not** a substitute, however obvious
it looks: NuGet refuses it with `NU1005` (`RestorePackagesWithLockFile property
is set to false but a packages lock file exists`) for as long as the lock file
is there. Redirecting the path is the supported way.

If you already published without the flag, every rewritten lock goes back to the
committed graph in one step:

```bash
git checkout -- '*/packages.lock.json'
git status --short      # must not list a packages.lock.json
```

Then re-check that the reviewed graph is still the one that restores cleanly:

```bash
dotnet restore PicoUI.slnx -p:RestoreLockedMode=true
```

CI enforces this from both ends: every AOT publish step carries the redirected
lock path, and two *Lock graph guard* steps fail the job if any build, test or
publish step rewrote a lock — a regression here must not be able to arrive
inside a green run.

## Why this rule exists

Someone once cut a release from a side branch. The packages built, the version
string was correct, and the file timestamps were brand new — but the branch was
**69 commits behind `main`**. The artifact was months of work stale and nothing
about it said so: no build log, no version number, no timestamp revealed it. It
was found later, after consumers had taken the packages.

Ninety-nine honest builds make the hundredth shortcut look safe. The branch
check is the only signal that is impossible to fake by accident, so it is
mechanical and non-negotiable.

## What the script enforces

| Check | Behaviour |
|---|---|
| **R1** branch is `main` | side branch or detached HEAD → prints the branch → **exit 2** |
| **R2** clean tree | prints the first 3 dirty paths → **exit 3** |
| **R3** provenance | prints full HEAD sha, branch name, UTC time |
| **R4** artifacts outside the repo | `${OUT:-$TMPDIR/rel-<short-sha>}`; `bin/obj` redirected there too; `OUT` inside the repo → **exit 5** |
| **R5** fingerprints | sha256 + size of every `.nupkg` (or lists the output dir) |
| **R6** build failure | any restore/test/pack failure → **exit 4** |

Exit codes: `0` success · `2` not on `main` · `3` dirty tree · `4` build failed
· `5` `OUT` inside the repository.

## If it refuses

- **exit 2** — you are not on `main`. Merge to `main` first; do not rename a
  branch to get past the check.
- **exit 3** — commit or stash. An uncommitted artifact cannot be reproduced
  from a commit.
- **exit 4** — the build or the test gate failed; nothing was released.
- **exit 5** — point `OUT` somewhere outside the repository (the default
  already does).

Override `VERSION` for the tag you are about to create; it defaults to
`0.0.0-dev` for a non-tag build. `SKIP_TESTS=1` exists for local iteration only
— a payload built with it is not releasable.
