// check-enforcement — verify the link between canonical rules and the Roslyn analyzers that
// deterministically enforce them (ADR-0004). It is the drift gate for `derives_to.hooks`.
//
// Errors (exit 1):
//   - a rule declares a hook `AXISNNNN` that no analyzer descriptor defines (dangling hook);
//   - an analyzer descriptor `AXISNNNN` that no rule points back to via derives_to.hooks (orphan enforcer).
// Warnings (exit 0):
//   - a descriptor's help link points at a different rule than the one that declares its hook.
// Info (exit 0):
//   - coverage: framework `must` rules with no hook yet (candidates for a future analyzer).
//
// Zero dependencies; reuses the rules YAML reader.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep, basename } from 'node:path';
import { parseYaml, listRuleFiles, RULES_DIR, REPO_ROOT } from './yaml-lite.mjs';

const HOOK_RE = /^AXIS\d+$/;

// --- 1. Collect analyzer descriptors from every *.Analyzers project under src/ ---------------
function listAnalyzerSources(dir, out = []) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (name.endsWith('.UnitTests')) continue;
      listAnalyzerSources(full, out);
    } else if (name.endsWith('.cs') && full.split(sep).some((s) => s.endsWith('.Analyzers'))) {
      out.push(full);
    }
  }
  return out;
}

// Pair each `id: "AXISNNNN"` with the help-link slug in the same DiagnosticDescriptor block.
const DESCRIPTOR_RE = /id:\s*"(AXIS\d+)"[\s\S]*?"([A-Za-z0-9._/-]+?)\.yaml"/g;

const descriptors = new Map(); // AXISNNNN -> { slug, file }
const srcRoot = join(REPO_ROOT, 'src');
for (const file of listAnalyzerSources(srcRoot)) {
  const text = readFileSync(file, 'utf8');
  for (const m of text.matchAll(DESCRIPTOR_RE)) {
    const id = m[1];
    const slug = basename(m[2]);
    if (!descriptors.has(id)) descriptors.set(id, { slug, file: relative(REPO_ROOT, file).split(sep).join('/') });
  }
}

// --- 2. Collect hooks declared by rules -----------------------------------------------------
const ruleHooks = new Map(); // AXISNNNN -> ruleId
const mustWithoutHook = [];
let ruleCount = 0;

for (const file of listRuleFiles()) {
  const rule = parseYaml(readFileSync(file, 'utf8'));
  ruleCount++;
  const hooks = (rule.derives_to && rule.derives_to.hooks) || [];
  const axisHooks = (Array.isArray(hooks) ? hooks : []).filter((h) => HOOK_RE.test(String(h).trim()));
  for (const h of axisHooks) ruleHooks.set(String(h).trim(), rule.id);

  if (rule.tier === 'framework' && rule.severity === 'must' && axisHooks.length === 0) {
    mustWithoutHook.push(rule.id);
  }
}

// --- 3. Cross-check --------------------------------------------------------------------------
const errors = [];
const warnings = [];

for (const [id, ruleId] of ruleHooks) {
  if (!descriptors.has(id)) errors.push(`rule "${ruleId}" declares hook ${id}, but no analyzer descriptor defines it`);
}
for (const [id, d] of descriptors) {
  const ruleId = ruleHooks.get(id);
  if (!ruleId) {
    errors.push(`analyzer descriptor ${id} (${d.file}) is not referenced by any rule's derives_to.hooks`);
  } else if (d.slug !== ruleId) {
    warnings.push(`descriptor ${id} help link points at "${d.slug}", but the hook is declared by rule "${ruleId}"`);
  }
}

// --- 4. Report -------------------------------------------------------------------------------
for (const w of warnings) console.log(`  warn  ${w}`);
for (const e of errors) console.log(`  ERROR ${e}`);

console.log(
  `\ncheck-enforcement: ${descriptors.size} descriptor(s), ${ruleHooks.size} hooked rule(s) across ${ruleCount} rule(s); ` +
  `${errors.length} error(s), ${warnings.length} warning(s).`,
);
if (mustWithoutHook.length) {
  console.log(`  info  ${mustWithoutHook.length} framework 'must' rule(s) with no analyzer yet: ${mustWithoutHook.join(', ')}`);
}

process.exit(errors.length ? 1 : 0);
