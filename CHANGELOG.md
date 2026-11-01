# Changelog

All notable changes to this repository are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## 3.0.0 (unreleased)

- The repository restarts as the hub of the 3.0 framework; each package now lives in its own repository. The 2.x
  source stays at the `v2.2.0` tag, which the published 2.x packages point to.
- The ECommerce reference application, in `scaffolds/`, consumes every Axis package from NuGet; its four test
  projects are the hub's tests.
- The documentation site (DocFX, in `site/`) gathers the articles and the API reference of the packages listed in
  `site/packages.json` — today `AxisResult` — and the application guide, published to GitHub Pages.
- The application guide (`site/guide/`) and the usage agent `agents/axis-framework-dotnet.md`.
- `Axis.Conventions.Analyzers` (`AXIS0600`–`AXIS0608`) is released from the hub, starting at `3.0.0-beta.7`: the
  analyzers live in `src/Axis.Conventions.Analyzers` and their tests in `tests/Axis.Conventions.Analyzers.UnitTests`,
  now named `{WhatItDoes}When{Condition}Async`. Pre-releases go to GitHub Packages on a push to `develop`, releases to
  nuget.org on a push to `main`.
- Each conventions diagnostic links to its section of the new guide page `site/guide/analyzers.md`, which documents
  what each analyzer flags and skips, why, and the fix.
- The reference application runs the conventions analyzers from source, through a project reference, instead of the
  `3.0.0-beta.6` package.
- `AxisGlobalization` and `AxisValidator.Documents` join the package list and the documentation site, and the reference
  application moves to the `AxisPay` and `AxisValidator` versions whose document checks live in `AxisValidator.Documents`.
