# AGENTS.md

Working notes for anyone who develops **in** this repository, person or coding agent. To learn how to **build an
application** the Axis way, read the [application guide](site/guide/index.md) or hand
[`agents/axis-framework-dotnet.md`](agents/axis-framework-dotnet.md) to your assistant. A change to a package itself
belongs in that package's repository (listed in the [README](README.md#packages)).

## Layout

| Path | What lives there |
|---|---|
| `AxisFramework.slnx` | The solution: the conventions analyzers, their tests and every project of the reference application. |
| `src/Axis.Conventions.Analyzers/` | The `Axis.Conventions.Analyzers` package (`AXIS0600`–`AXIS0608`), the only package released from the hub. |
| `src/Directory.Build.props` | Package metadata and version (`3.0.0-beta.7`) of what `src/` releases; everything else stays non-packable. |
| `tests/Axis.Conventions.Analyzers.UnitTests/` | The analyzers' unit tests: each case compiles a snippet and counts the diagnostics. |
| `tests/Directory.Build.props` | Marks the analyzer tests (`AxisTestProject=true`) before the root props are read. |
| `scaffolds/` | The reference application (ECommerce): `Core/`, `Contracts/`, `Adapters/`, `Host/`, `Tests/`. See [`scaffolds/README.md`](scaffolds/README.md). |
| `scaffolds/Tests/` | The four test projects — architecture, unit, integration, E2E. They are the hub's tests. |
| `Directory.Build.props` | Compilation settings for every project and the wiring of the test projects (`AxisTestProject`). |
| `scaffolds/Directory.Build.props` | Runs the conventions analyzers of `src/` on every project of the reference application (a project reference with `OutputItemType="Analyzer"`). |
| `scaffolds/Tests/Directory.Build.props` | Marks the test projects (`AxisTestProject=true`) before the root props are read. |
| `Directory.Packages.props` | The version of every Axis package the application consumes, one line per package, with transitive pinning, and of the analyzers' build dependencies. |
| `site/` | The DocFX site: `index.md`, `toc.yml`, `docfx.json`, `packages.json`, `collect-docs.mjs`, `ApiReference/`. |
| `site/guide/` | The application guide, one page per concern. |
| `agents/` | `axis-framework-dotnet.md`, the usage agent applications copy into their own assistant. |

## Commands

Run from the repository root, with the SDK pinned in `global.json`, Docker running, and `AXIS_NUGET_USER` /
`AXIS_NUGET_TOKEN` set (a GitHub user and a token with `read:packages`) so the Axis packages restore from GitHub
Packages:

```bash
dotnet restore AxisFramework.slnx
dotnet build AxisFramework.slnx -c Release --no-restore -warnaserror
dotnet test --solution AxisFramework.slnx -c Release --no-build
```

The build must end with zero warnings. The E2E suite runs against the provider pinned in
`ECommerceProviderPin.DefaultProvider`; the other provider's collection is reported as skipped. Only `src/` is
packable; after the build, pack the package from its project (packing the solution warns on the web host):

```bash
dotnet pack src/Axis.Conventions.Analyzers/Axis.Conventions.Analyzers.csproj -c Release --no-build -o artifacts
```

### The documentation site

With the package repositories listed in `site/packages.json` cloned side by side under one folder:

```bash
node site/collect-docs.mjs <folder>
dotnet build site/ApiReference/ApiReference.csproj -c Release
dotnet tool restore
dotnet docfx site/docfx.json
```

`collect-docs.mjs` copies each repository's `docs/` into `site/articles/<repository>/` and writes
`site/articles/toc.yml`, the package indexes and `site/ApiReference/Packages.g.props`; the `ApiReference` build lays
out the packages' assemblies and XML docs in `site/ApiReference/out/`; DocFX writes the API reference to `site/api/`
and the site to `site/_site/` (add `--serve` to browse it). Everything they write is git-ignored. `ApiReference` is
not part of `AxisFramework.slnx`, because it needs the props file `collect-docs.mjs` writes.

## The conventions analyzers

`src/Axis.Conventions.Analyzers` is a `netstandard2.0` Roslyn component; like every analyzer project, it needs no XML
docs. Its ids live in `AXIS0600`–`AXIS0799`. A new or changed analyzer comes with:

1. its descriptor in `ConventionsDiagnostics.cs`, with `helpLinkUri: HelpBase + "axisNNNN"`, and a line in
   `AnalyzerReleases.Unshipped.md`;
2. its section `## AXISNNNN` in [`site/guide/analyzers.md`](site/guide/analyzers.md) — the anchor the help link
   opens — and a row in that page's table and in the package [README](src/Axis.Conventions.Analyzers/README.md);
3. tests in `tests/Axis.Conventions.Analyzers.UnitTests` through `AnalyzerHarness.RunAsync<TAnalyzer>`, with cases
   that fire and cases that do not;
4. the reference application still building with `-warnaserror`: it runs the analyzers from source, so a new
   diagnostic it raises is fixed in the same change (or the convention is wrong);
5. the guide page of the convention and `agents/axis-framework-dotnet.md` updated when the convention changes.

The package version is in `src/Directory.Build.props`. A push to `develop` publishes a version with a pre-release
suffix to GitHub Packages ([`prerelease.yml`](.github/workflows/prerelease.yml)); a push to `main` publishes a version
without one to nuget.org and tags the release ([`release.yml`](.github/workflows/release.yml)). Never reuse a
version.

## When a package moves to its own repository

Each Axis package is released from its own repository. When one publishes its first pre-release from there:

1. Raise its version in `Directory.Packages.props` (every id the repository ships).
2. Add the repository and its package ids to `site/packages.json`, so its articles and API reference join the site.
3. Build with `-warnaserror` and run the tests of the reference application; then generate the site.
4. Update the package table of the `README.md` if the repository's description changed.

The reference application's tests are the hub's tests: they prove the package compiles and fits with the others.
A package's own scenarios belong to its unit tests, in its repository.

## Conventions

- **The reference application follows the guide.** A change to it keeps every convention of `site/guide/`; a change
  to a convention updates the guide page, `agents/axis-framework-dotnet.md` and the code it links in the same
  change. The guide links files by path: renaming or moving a linked file updates its links.
- **Consume packages, never source.** Every Axis dependency is a `PackageReference` with its version in
  `Directory.Packages.props`; no project here references another repository's source. The one project reference
  into `src/` is the conventions analyzers, which are released from this repository.
- **No comments that restate the code**, no `#region`, and `///` docs only where they feed the OpenAPI document.
- **Tests** are named `{WhatItDoes}When{Condition}Async`, follow Arrange-Act-Assert and use xUnit v3,
  `Axis.Testing.XUnit`, Moq and Testcontainers. A test never tags itself with a rule; its name says what it proves.
- **A new or changed controller** needs E2E coverage: a happy path and a 401, plus a 403 when it is
  permission-gated.

## Pull requests

A pull request contains:

- a green `dotnet build AxisFramework.slnx -c Release -warnaserror` and `dotnet test --solution AxisFramework.slnx -c Release`;
- for a new or changed analyzer, its tests and its section in `site/guide/analyzers.md`;
- the guide pages and the usage agent updated when a convention or a linked file changed;
- a `CHANGELOG.md` line;
- commits in [Conventional Commits](https://www.conventionalcommits.org/) style.

Branching and the rest of the process are in [`CONTRIBUTING.md`](CONTRIBUTING.md).
