# PicoHtmx Repo Relocation Design (PicoNode → PicoAgent)

> **Date:** 2026-08-17
> **Status:** Implemented (Phase 0 landed in 9bf69f6 "refactor(htmx): import PicoHtmx module from the PicoNode repo")
> **Note:** Phase 0 (relocation) is DONE and committed. Remaining work is the htmx-pattern-extraction phases (see plan).
> **Scope:** Move the PicoHtmx module (source + tests) from the PicoNode repo into the PicoAgent repo, next to the other UI toolkits (PicoMarkdown / PicoMermaid / PicoTui). Prerequisite (Phase 0) for the htmx-pattern-extraction work.

## Problem

PicoHtmx is a **UI/HTML toolkit** (AOT-safe HTML builder + htmx response helpers + server-rendered components), but it lives in the **PicoNode repo**, which is the **network stack** (TcpNode / HTTP / PicoWeb / PicoNode.Web). It is unrelated to that stack:

- Namespace is already standalone: `PicoHtmx` (not under `PicoNode.*`).
- Its only dependency is `PicoWeb` (the web-app layer); no coupling to the TCP/HTTP internals.

The repo grouping is inconsistent: PicoMarkdown, PicoMermaid and PicoTui (all UI/renderer toolkits) live in the **PicoAgent repo** under `src/`, while PicoHtmx sits in PicoNode. If PicoHtmx later becomes an independent repository, it should cluster with those UI toolkits — so it should live beside them now.

## Current State (verified)

| Item | Location |
|---|---|
| Source (7 files) | `PicoNode/src/Web/PicoHtmx/` — `Components.cs`, `GlobalUsings.cs`, `HtmlBuilder.cs`, `HtmxResponse.cs`, `HtmxResult.cs`, `Layout.cs`, `PicoHtmx.csproj` |
| Tests (7 files) | `PicoNode/tests/PicoHtmx.Tests/` — `ComponentsTests.cs`, `GlobalUsings.cs`, `HtmlBuilderTests.cs`, `HtmxResponseTests.cs`, `HtmxResultTests.cs`, `LayoutTests.cs`, `PicoHtmx.Tests.csproj` |
| Dependency | `PicoHtmx.csproj` → `ProjectReference ..\PicoWeb\PicoWeb.csproj` (PicoNode repo) |
| Consumers | PicoNode: `PicoWeb.Samples` (`HtmxController.cs`, csproj:16), `PicoHtmx.Tests`; PicoAgent: `PicoAgent.Htmx` (csproj:26 sibling path) |
| Solutions | `PicoNode.slnx:26,39` (src + tests) |
| CI | PicoNode `ci.yml:99-103` "AOT publish (PicoHtmx web sample)" runs `PicoWeb.Samples` |
| Package | PicoHtmx package version pinned in `PicoAgent/Directory.Packages.props:23` (`2026.1.4`); published from the PicoNode repo release pipeline |

## Decisions

1. **Move source + tests into PicoAgent repo** verbatim: `src/PicoHtmx/` and `tests/PicoHtmx.Tests/`, alongside PicoMarkdown / PicoMermaid / PicoTui.
2. **Namespace stays `PicoHtmx`** — zero namespace churn; only `using PicoNode.Web;` remains (PicoWeb stays in PicoNode).
3. **PicoWeb dependency becomes a cross-repo sibling reference**, reusing PicoAgent's existing `UseSiblingProjectReferences` machinery (project ref when `../PicoNode` is checked out; `PackageReference PicoWeb` otherwise). Same conditional pattern `PicoAgent.Htmx.csproj:24-30` already uses.
4. **`PicoWeb.Samples` (PicoNode) drops `HtmxController` + the PicoHtmx project reference.** It is PicoNode's only reverse consumer; keeping it would force a PicoNode→PicoAgent sibling reference (inverted direction) plus a PicoNode CI checkout of PicoAgent. Not worth it — PicoNode's sample is about the network stack.
5. **PicoHtmx AOT validation transfers to `PicoAgent.Htmx`** — it is already an AOT-published sample and is the heaviest PicoHtmx consumer; coverage improves.
6. **PicoNode CI removes the PicoHtmx AOT step; PicoAgent CI adds `PicoHtmx.Tests`** to its test matrix (it already checks out PicoNode as a sibling for `PicoWeb`/`PicoHtmx`).
7. **NuGet publishing source moves to the PicoAgent repo** release pipeline (PicoHtmx already has its version entry in PicoAgent's `Directory.Packages.props`).
8. **Ordering:** this relocation is **Phase 0** — it must land before the htmx-pattern-extraction spec (`2026-08-17-htmx-pattern-extraction-design.md`), whose Phase-1 paths then point at `PicoAgent/src/PicoHtmx/`.

## Migration Steps

### Step 1 — Move source

```
PicoNode/src/Web/PicoHtmx/  →  PicoAgent/src/PicoHtmx/   (7 files, verbatim)
```

`PicoHtmx.csproj` changes:
- Replace `<ProjectReference Include="..\PicoWeb\PicoWeb.csproj" />` with the conditional pair:
  - `ProjectReference ..\..\..\PicoNode\src\Web\PicoWeb\PicoWeb.csproj` (sibling mode, `Condition="'$(UseSiblingProjectReferences)' == 'true'"`)
  - `PackageReference Include="PicoWeb"` (package mode, already pinned `2026.1.5` in PicoAgent's props)

### Step 2 — Move tests

```
PicoNode/tests/PicoHtmx.Tests/  →  PicoAgent/tests/PicoHtmx.Tests/   (7 files, verbatim)
```

`PicoHtmx.Tests.csproj`: `ProjectReference ..\..\src\Web\PicoHtmx\PicoHtmx.csproj` → `..\..\src\PicoHtmx\PicoHtmx.csproj`.

### Step 3 — PicoAgent solution + consumer

- `PicoAgent.slnx`: add `src/PicoHtmx/PicoHtmx.csproj` and `tests/PicoHtmx.Tests/PicoHtmx.Tests.csproj` (beside the other `src/` modules at lines 19-21, 34-36).
- `PicoAgent.Htmx.csproj:26`: sibling PicoHtmx path `..\..\..\PicoNode\src\Web\PicoHtmx\PicoHtmx.csproj` → local `..\..\src\PicoHtmx\PicoHtmx.csproj`. PicoWeb sibling path (line 25) stays unchanged.

### Step 4 — PicoNode cleanup

- Delete `PicoNode/samples/PicoWeb.Samples/Controllers/HtmxController.cs`; remove the PicoHtmx `ProjectReference` (csproj:16).
- Remove `src/Web/PicoHtmx` and `tests/PicoHtmx.Tests` entries from `PicoNode.slnx` (lines 26, 39).
- Remove the "AOT publish (PicoHtmx web sample)" CI step (`ci.yml:99-103`).
- Remove PicoHtmx from the PicoNode release/publish pipeline (if separate from the build matrix).

### Step 5 — PicoAgent CI + release

- Add `tests/PicoHtmx.Tests/PicoHtmx.Tests.csproj` to the PicoAgent CI test step (sibling PicoNode checkout already present).
- Move PicoHtmx NuGet publishing to the PicoAgent release pipeline.
- Verify the AOT publish of `PicoAgent.Htmx` now also validates PicoHtmx (it already does implicitly — confirm no PicoHtmx trim warnings are newly masked).

## Verification

- `PicoNode`: `dotnet build PicoNode.slnx` green without PicoHtmx; `PicoWeb.Samples` AOT publish green without HtmxController.
- `PicoAgent`: `dotnet build PicoAgent.slnx` green; `PicoHtmx.Tests` all pass (moved verbatim — must be byte-identical behavior); `PicoAgent.Htmx.Tests` (317) green; `PicoAgent.Htmx` AOT publish green.
- No `namespace PicoHtmx` references break: `grep -rn "PicoHtmx"` across both repos shows only the expected new paths.

## Impact

| Repo | Change |
|---|---|
| PicoNode | remove `src/Web/PicoHtmx`, `tests/PicoHtmx.Tests`, `PicoWeb.Samples` HtmxController + ref, slnx entries, CI AOT step |
| PicoAgent | add `src/PicoHtmx` (7 files, csproj re-pointed), `tests/PicoHtmx.Tests` (7 files), slnx entries, `PicoAgent.Htmx` ref re-pointed, CI test entry, release pipeline entry |

## Non-Goals

- No API changes to PicoHtmx (the extraction spec handles those, later).
- No namespace rename (`PicoHtmx` stays).
- No change to PicoWeb's repo location (it stays in PicoNode — it is the web layer).
