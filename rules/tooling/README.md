# rules/tooling

Zero-dependency Node scripts (ESM, `.mjs`) that keep `rules/` honest. Require Node 18+. Run from anywhere.

| Script | What it does | Exit |
|---|---|---|
| `node tooling/lint-rules.mjs` | Validate every `rules/*.yaml` against [`_schema.md`](../_schema.md): required fields, enums, `id == file name`, uniqueness, folder ↔ tier/concern agreement, and that every `reference`/`examples` path exists. | 1 on error |
| `node tooling/gen-index.mjs` | Regenerate [`../README.md`](../README.md) from frontmatter (by family · by concern · backlog). The index is generated — never hand-edit it. | 0 |
| `node tooling/check-freshness.mjs` | Report rules whose `ledger.reviewed_against` is behind the framework's current `git HEAD` — the review backlog. | 0 |
| `node tooling/check-enforcement.mjs` | Verify the rule ↔ analyzer link (ADR-0004): every `derives_to.hooks` `AXISNNNN` has a Roslyn descriptor under `src/**/*.Analyzers`, and every descriptor is referenced by exactly one rule. Reports coverage of framework `must` rules with no analyzer yet. | 1 on drift |
| `node tooling/sync-scaffolds.mjs` | Inject real, compiled `#region scaffold:<id>` snippets from the `src/scaffolds/` sample solution into the matching `<!-- scaffold:<id> -->` block in `docs/en-us/**`, so doc code is never hand-typed. `--check` reports drift without writing. | 1 on drift/missing |

`yaml-lite.mjs` is a strict reader for the constrained YAML subset in `_schema.md`; it throws on anything
it does not recognize (so a malformed rule fails loudly). It is not a general YAML parser.

Typical loop: edit rules → `lint-rules` → `gen-index` → commit. CI runs `lint-rules` as a gate.
