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
