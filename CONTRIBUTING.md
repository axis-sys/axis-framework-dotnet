# Contributing to the Axis Framework hub

Thanks for helping. This repository holds the reference application, the application guide and the documentation
site of the Axis Framework. The packages live in their own repositories: a bug or an idea for one package goes to
that package's repository (listed in the [README](README.md#packages)).

## Before you start

- **Bugs and ideas start as an issue.** Use the templates. For a new convention or a change to an existing one,
  describe the use case first; we agree on it in the issue before any code.
- Small fixes (a typo in the guide, a broken link, a missing test) can go straight to a pull request.

## Build and test

You need the .NET SDK pinned in `global.json` and Docker running. The Axis pre-releases restore from the
organization's GitHub Packages feed: set `AXIS_NUGET_USER` (a GitHub user) and `AXIS_NUGET_TOKEN` (a token with
`read:packages`) first.

```bash
dotnet build AxisFramework.slnx -c Release -warnaserror
dotnet test --solution AxisFramework.slnx -c Release
```

How to generate the documentation site is in [`AGENTS.md`](AGENTS.md#the-documentation-site).

## What a pull request needs

- Tests for the behavior it adds or changes, named `{WhatItDoes}When{Condition}Async`; E2E coverage for every new
  or changed controller.
- The guide pages and `agents/axis-framework-dotnet.md` updated when a convention or a file they link changes.
- A line in `CHANGELOG.md`.
- Commits in [Conventional Commits](https://www.conventionalcommits.org/) style.

## Branches

- Work happens on `develop`; `main` only receives what is released.
- A push to `main` publishes the documentation site to GitHub Pages.

## License

By contributing you agree that your contribution is licensed under the [Apache-2.0](LICENSE) license.
