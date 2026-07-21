# Axis agents for Claude Code

Canonical, versioned source of the Claude Code agents and commands that ship with the Axis pattern.
Claude Code does **not** read this folder — it reads `.claude/agents/`+`.claude/commands/` (project
scope, gitignored in this repo) and `~/.claude/agents/`+`~/.claude/commands/` (user/global scope,
outside any repo). This folder is the source of truth; both `.claude/` locations are runtime copies.

## Install

Install to **both** scopes — they serve different purposes:

- **Project** (`<repo>/.claude/`) — active only while working inside this repo (e.g. developing the
  agents themselves, or dogfooding them on the framework repo).
- **Global** (`~/.claude/`) — active in **every** repo on the machine. Required for `axis-bc-migrator`
  and `/axis-audit`, which are meant to run against *other*, non-Axis-compliant repositories — they are
  useless if scoped only to `axis-framework-dotnet`. `axis-reviewer`/`axis-architect` also benefit from
  being available everywhere the `axis-dotnet` plugin is used.

```powershell
# Project scope (from the repo root)
Copy-Item .agents/claude/agents/*   .claude/agents/   -Force
Copy-Item .agents/claude/commands/* .claude/commands/ -Force

# Global scope (any cwd)
Copy-Item .agents/claude/agents/*   $HOME/.claude/agents/   -Force
Copy-Item .agents/claude/commands/* $HOME/.claude/commands/ -Force
```

Then restart Claude Code (the agents register at session start; if either `agents`/`commands` directory
did not exist before, a restart is mandatory — the file watcher only covers directories that existed at
session start). Project-level always wins over global for the same agent name, so having both in sync is
safe — verify with a fresh, non-interactive check rather than asking chat "which agents do you have":

```powershell
claude -p "List every custom subagent name registered and available via the Agent/Task tool, one per line."
```

## Contents

| File | Kind | Role |
|---|---|---|
| `agents/axis-reviewer.md` | subagent (read-only) | pre-commit review gate — deterministic gates first, then the §A–D judgment of `axis-review`; emits findings + `VERDICT: BLOCK/CLEAR` |
| `agents/axis-architect.md` | subagent (read-only) | planning gate — BC boundary, subdomain, aggregate level, cross-BC channel; emits a decision plan with rule anchors |
| `agents/axis-bc-migrator.md` | subagent (can edit) | migrates ONE bounded context toward Axis; fixes SAFE-FIX items, defers PENDING-DECISION; structured report |
| `commands/axis-audit.md` | slash command (`/axis-audit`) | orchestrates the audit of a whole repo: maps BCs, fans out one `axis-bc-migrator` per BC (sequential by default), consolidates the report |

## Editing rules

- Edit HERE, then re-copy to **both** `.claude/` and `~/.claude/` (see Install). Never edit only a
  runtime copy — it is not versioned and the other copy (or a fresh clone) will silently drift back to
  the old behavior.
- Subagent frontmatter gotchas (learned the hard way):
  - `Skill` is NOT a valid entry in `tools:` — an invalid tool name silently prevents registration.
    Preload skills via the `skills:` frontmatter field instead.
  - `skills:` must be a **YAML list**, not a comma-separated string (unlike `tools:`, which IS a
    comma-separated string — the two fields are inconsistent with each other by design):
    ```yaml
    tools: Bash, Read, Grep, Glob      # comma string
    skills:
      - axis-dotnet:axis-review        # YAML list
    ```
  - If an `agents`/`commands` directory (project or global) did not exist before the session started,
    restart Claude Code — the file watcher does not detect a newly created directory mid-session.
- The agents reference skills by their plugin-qualified name (`axis-dotnet:<skill>`), so the
  `axis-dotnet` plugin must be installed (globally, or in the target repo) for them to work.
