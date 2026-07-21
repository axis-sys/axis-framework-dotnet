# Axis Documentation Guide

This document is the **single source of truth** for how every Axis package is documented. It distills the proven pattern established by **AxisResult** (`docs/en-us/0-Foundations/AxisResult`) so that every other package reaches the same quality, structure, navigation and bilingual coverage.

> **Golden reference:** when in doubt about a format, open the equivalent page under `docs/en-us/0-Foundations/AxisResult/` (or `docs/pt-br/0-Foundations/AxisResult/`). AxisResult is the living template — match it.

---

## Core principles

1. **Mirror AxisResult exactly.** Same folder layout, same file-naming, same page anatomy, same tone. A reader who knows one package's docs already knows them all.
2. **English first, then a faithful translation.** Write `docs/en-us/` completely, then translate 1:1 into `docs/pt-br/`. Translate prose and code comments; never translate identifiers, type names, method names or API symbols.
3. **The README is a map, not a wall of text.** It lets a reader grasp the package in ~5 minutes and jump straight to the detail they need.
4. **Code first, theory second.** Every concept opens with a runnable `csharp` example, then explains why.
5. **Pure Markdown, no site generator.** Rendered natively on GitHub. Navigation is manual via relative links.
6. **Examples must match the real API.** Read the package's `.cs` source; signatures, type names and members in examples must be real.

---

## Folder structure (per package)

Every package lives under its family's numbered top-level folder (`0-Foundations`, `1-Observability`,
`2-ApplicationFlow`, `3-Infra`, `4-Edge`), mirrored one-to-one under `docs/en-us/` and `docs/pt-br/`.
There is **no separate package-root README** outside `docs/` — the README is just another file
alongside its siblings, flat inside the package's language folder:

```
docs/
├── en-us/
│   └── <N-Family>/
│       └── <Package>/
│           ├── README.md              # English map — the landing page
│           ├── getting-started.md
│           ├── why-<package-kebab>.md
│           ├── <feature>.md           # one page per feature / concept / operator
│           └── api-reference.md
└── pt-br/
    └── <N-Family>/
        └── <Package>/
            ├── README.md              # Portuguese map (mirror of the English README)
            ├── getting-started.md
            ├── <feature>.md
            └── api-reference.md
```

The **framework-level master index** is the only README outside this layout: the English map lives at
the repo root (`README.md`) and the Portuguese map at `docs/pt-br/README.md`.

**File-naming rule (important):** AxisResult keeps **identical English filenames in both `en-us/` and `pt-br/`** (e.g. `getting-started.md`, `why-axisresult.md`, `map.md` exist verbatim in both folders). **Do the same** — keep the same kebab-case English filenames in `pt-br/`. Only the **content** is translated, never the filenames. This keeps cross-links trivially parallel — every page and its translation sit at the identical relative position in their respective language tree, so a link only ever needs to walk *within* the current language tree (never through a `docs/en-us/` or `docs/pt-br/` segment mid-path).

---

## Cross-language and back links (copy verbatim, adjust names)

- **Root English README** (framework master index, repo root), just under the H1:
  `> 🌐 [Português (documentação navegável)](docs/pt-br/README.md)`
- **Portuguese README** (`docs/pt-br/README.md`), just under the H1:
  `> 🌐 [English (README principal)](../../README.md)`
- **Every English page** (sibling of its package's `README.md`) ends with:
  `↩ [Back to <Package> docs](README.md)`
- **Every Portuguese page** ends with:
  `↩ [Voltar à documentação do <Package>](README.md)`

**Cross-package links** (e.g. `AxisLogger` linking to `AxisTelemetry`) stay inside the current language
tree and walk relative to the family folders — from `docs/en-us/1-Observability/AxisLogger/page.md` to
`docs/en-us/1-Observability/AxisTelemetry/README.md` is `../AxisTelemetry/README.md`; to a package in a
different family, e.g. from `2-ApplicationFlow/AxisMediator/` to `1-Observability/AxisLogger/`, is
`../../1-Observability/AxisLogger/README.md`. Never route a cross-package link through `docs/en-us/` or
`docs/pt-br/` — the language boundary is already fixed at the root of the current tree, and a page must
never link across languages except via the two chrome links above.

---

## Document archetypes

Pick the archetypes that fit the package. A small package may have only `README + getting-started + why + api-reference + 1–2 feature pages`. A large one adds a philosophy page, several feature pages, and pattern pages.

### A. README — the map

Anatomy (in order):

1. `# <Package> — Documentation` (H1)
2. `> 🌐 [Português (documentação navegável)](docs/pt-br/README.md)`
3. **Bold tagline** — one sentence stating what the package is and its headline traits.
4. A **"hero" `csharp` block** — the single most representative real-world usage.
5. One line telling the reader to use the page as a map.
6. `---`
7. `## The trunk (read first)` — a ~5-minute tour with `###` subsections covering the mental model, the key types, how to create/use the primary objects, and **Installation** (`dotnet add package <Package>`). End key subsections with an arrow link to the detail page: `→ **[Title](page.md)**`.
8. `---`
9. `## The map (jump to what you need)` — a 3-column table:

   | Group | You want to… | Detail |
   |---|---|---|
   | **Feature · `Api`** | natural-language use case | [page.md](page.md) |

   Mark the single most central feature with ` ⭐`.
10. Three shortcut lines: `**Start here:** …` · `**Fundamentals:** …` · `**Reference & extras:** …` (each a `·`-separated list of links).
11. `---`
12. `## Design principles` — a numbered list (≈5) of the principles the package embodies, each `**bolded lead.** explanation`.
13. `---`
14. `## License` → `Apache 2.0`

### B. getting-started.md

`# Getting started · installation and usage` → blockquote one-liner → `## Installation` (`dotnet add package …` + a line on size/dependencies) → `## Creating …` / `## Using …` / `## Inspecting …` (the minimal lifecycle, each a `csharp` block) → a `## Chaining …`/integration example with a `**Why it pays off:**` line → `## See also` → `↩ Back`.

### C. why-<package>.md — comparison

`# Why <Package>? · comparison` → blockquote → one `## vs. <Alternative>` section per competitor (honest, specific, no hand-waving) → a `## The comparison` **feature matrix table** (features as rows, libraries as columns, `**Yes**`/`No`/`Partial`, the Axis column always first and bolded) → `## See also` → `↩ Back`.

### D. Philosophy / concept page (with ASCII diagram)

For the package's central mental model (mirror `railway-oriented-programming.md`). `# <Concept> · the why` → blockquote → `## The problem` (a deliberately ugly "before" `csharp` block + a punchy critique in **bold** counts like "**40 lines. 5 catch blocks.**") → the "after" with the package → `## What is <Concept>?` with an **ASCII diagram** → short history/credits → `## See also` → `↩ Back`.

### E. Fundamentals / concept page

For a core type or rule (mirror `errors-and-types.md`). `# <Topic> · \`Type\`` → blockquote → opening `csharp` → `## <The N categories>` or `## <Key rules>` (often a table) → a `## Why …?` rationale section → optional `**Why it pays off:**` → `## See also` → `↩ Back`.

### F. Feature / operator / API page (the workhorse)

Mirror `map.md` (simple) / `then.md` (complex). Anatomy:

1. `# <Verb/Feature> · \`Api\`` (e.g. `# Side effects · \`Tap\``)
2. `> ` blockquote, 1–2 sentences — what it does and its one defining trait.
3. A primary `csharp` example (with inline `// comments` showing results/flow).
4. `---`
5. `## When to use` — one short paragraph.
6. `## When *not* to use` — a 2-column table:

   | You want to… | Use instead |
   |---|---|
   | … | [`Other`](other.md) |
7. `---`
8. A technical section — choose the heading that fits: `## Operators` / `## Forms` / `## Available overloads` / `## The N forms (the part that confuses the most)` — usually a **signature/behavior table**, optionally a `csharp` block of signatures. Note async (`Task`/`ValueTask`) and `CancellationToken` variants where relevant.
9. `---`
10. `## Real-world example(s)` — 1–3 examples. Multiple examples use `### 1. Title` / `### Example 1 — title`. Each example: a real `csharp` snippet (handlers, ports, repositories — realistic domain code) followed by **`**Why it pays off:**`** + 1–3 sentences on the concrete benefit.
11. `---`
12. `## See also` — bulleted links to related pages, each `- [Title](page.md) — short reason`.
13. `---`
14. `↩ [Back to <Package> docs](README.md)`

### G. api-reference.md

`# API reference` → blockquote ("complete catalog, grouped by responsibility; use for lookup") → one `## <Responsibility>` section per group, each a table (`Method | Signature | Description` or `Method | Description`) → after each group an arrow link `→ [detail](page.md)` → `## See also` → `↩ Back`.

---

## Formatting conventions

- **Headings:** H1 for the page title only; H2 for sections; H3 for named examples/subsections. Avoid going deeper.
- **Inline code:** backtick every identifier, type, method, package name, namespace, CLI command.
- **Code blocks:** ` ```csharp ` for C#; bare ``` ``` ``` for shell/CLI (`dotnet add package …`) and ASCII diagrams.
- **Blockquotes (`>`):** the page's opening one-liner and the `🌐` language link.
- **Tables:** native Markdown; align columns for readability in source.
- **Emphasis:** `**bold**` for key concepts and the `**Why it pays off:**` lead; `*italics*` for light emphasis.
- **Emojis:** `🌐` language link · `⭐` the single most central feature in the map · `↩` back link · `→` "see the detail" inline arrow. Use sparingly, exactly as AxisResult does.
- **Links:** always **relative**, within the current language tree — a sibling page (`page.md`), a same-family package (`../AxisTelemetry/README.md`), a cross-family package (`../../1-Observability/AxisLogger/README.md`), or the two language-chrome links (`docs/pt-br/README.md` from the repo root; `../../README.md` from `docs/pt-br/README.md`). Never absolute URLs to the repo, and never a path that crosses languages mid-document.
- **Horizontal rules:** `---` between major sections, exactly as in the templates.

---

## Writing voice & style

- Direct, confident, technical. Short sentences. Active voice.
- Lead with the problem the reader feels, then the solution.
- "Code first, theory second" — show, then explain.
- Always close real examples with **`**Why it pays off:**`** describing the *concrete* payoff (fewer lines, no `try/catch`, errors surfaced together, etc.).
- Honest comparisons — name real alternatives and real trade-offs.
- Roughly 40% code / 60% prose. Pages read in 5–10 minutes.

---

## Internationalization — en-us → pt-br rules

1. Write **all** of `en-us/` first and review it.
2. Translate each file into `docs/pt-br/` keeping the **same filename** and **identical structure** (same headings, same number of sections, same examples, same tables).
3. **Translate:** prose, headings, table headers/cells that are prose, `// code comments`, and the `**Why it pays off:**` lead (→ `**Por que compensa:**`).
4. **Do NOT translate:** type names, method names, property names, package names, namespaces, enum members, code identifiers, CLI commands, or string literals that are stable codes (e.g. `"USER_NOT_FOUND"`).
5. Localize the navigation chrome:
   - `🌐 [English (README principal)](../../README.md)` at the top of `pt-br/README.md`.
   - `↩ [Voltar à documentação do <Package>](README.md)` at the foot of every pt-br page.
6. Established term translations (match AxisResult):
   - "Getting started" → "Primeiros passos"
   - "When to use" → "Quando usar" · "When *not* to use" → "Quando *não* usar"
   - "Use instead" → "Use no lugar" · "You want to…" → "Você quer…"
   - "See also" → "Veja também" · "Real-world example(s)" → "Exemplo(s) real(is)"
   - "Available overloads" → "Sobrecargas disponíveis" · "Forms" → "Formas"
   - "Why <Package>?" → "Por que <Package>?" · "Design principles" → "Princípios de design"
   - "The trunk (read first)" → "O tronco (leia primeiro)" · "The map (jump to what you need)" → "O mapa (salte para o que precisa)"
   - "Group / Detail" → "Grupo / Detalhe" · "Rule of thumb" → "Regra prática"
7. Keep full Brazilian-Portuguese orthography: every accent and diacritic (ã, ç, é, í, ó, ê, õ…). Never use ASCII substitutes.

---

## The Axis package families (taxonomy)

Use these families to group packages in the master index, to order learning, and to write cross-package `See also` links. Confirm a package's family by reading its code.

| Family | Purpose | Packages |
|---|---|---|
| **Foundations · Fundamentos** | Typed building blocks used by everything else | `AxisResult`, `AxisTypes`, `AxisMediator.Contracts` |
| **Observability · Observabilidade** | Cross-cutting concerns your operators stare at | `AxisLogger`, `AxisTelemetry` |
| **Application & Flow · Aplicação e Fluxo** | Use-case orchestration, processes, rules | `AxisMediator`, `AxisSaga`, `AxisValidator`, `AxisBus` |
| **Infrastructure & Integration · Infraestrutura e Integração** | Adapters for external resources | `AxisRepository`, `AxisCache`, `AxisStorage`, `AxisEmail` |
| **Edge · Borda** | The HTTP boundary | `AxisResult.HttpResponse` |

---

## Per-package workflow

1. **Read the source.** List and read the package's `.cs` (public types, members, `DependencyInjection`, adapters/implementations). Capture the public surface and the real usage patterns.
2. **Draft the page map.** Choose archetypes proportional to size: small ≈ 4–6 pages, medium ≈ 6–9, large ≈ 10–16. List the filenames before writing.
3. **Write `docs/en-us/<Family>/<Package>/`**, including its `README.md`. Follow the archetypes above. Real examples from the real API.
4. **Translate into `docs/pt-br/<Family>/<Package>/`** (+ its `README.md`) per the i18n rules.
5. **Validate** against the checklist below.
6. **Update the master index** (`README.md` at the repo root + `docs/pt-br/README.md`) with the package's row/link.
7. **Commit** the package (`docs(<Package>): bilingual documentation`).

---

## Quality checklist (run before committing each package)

- [ ] The package's `docs/en-us/<Family>/<Package>/README.md` (English map) + `docs/pt-br/<Family>/<Package>/README.md` (Portuguese map) exist and mirror each other.
- [ ] `docs/en-us/<Family>/<Package>/` and `docs/pt-br/<Family>/<Package>/` contain the **exact same set of filenames**, including `README.md` on both sides.
- [ ] Every page follows its archetype: title, blockquote, examples, When to use / not, See also, `↩` back link.
- [ ] All relative links resolve (in-package, cross-package, language links, back links). No broken links.
- [ ] Every `csharp` example uses **real** type/method names from the package source.
- [ ] Each real example has a `**Why it pays off:**` / `**Por que compensa:**` line.
- [ ] pt-br is a faithful 1:1 translation — same structure, identifiers preserved, comments translated, full diacritics.
- [ ] Tone, emoji usage and formatting match AxisResult.
- [ ] Master index updated with the new package.

---

## Master index (framework-level)

Maintain a bilingual framework landing page mirroring the AxisResult README style:

- `README.md` (English, repo root) and `docs/pt-br/README.md` (Portuguese).
- Intro tagline → the **5 families** with a short description each → a **package table** (`Package · Family · one-liner · docs link`) → a suggested **learning order** → `License`.
- Update it incrementally as each package's docs land, so links never dangle.
