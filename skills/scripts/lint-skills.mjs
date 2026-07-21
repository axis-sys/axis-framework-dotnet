#!/usr/bin/env node
'use strict';
// Lint for the Axis skills (Node, zero dependencies). Validates:
//  - frontmatter delimited by --- is present
//  - name == folder name
//  - description present and <= 1024 characters ("folded" length)
//  - "## See also" section present
//  - cross-links: references to `axis-...` that are neither a known skill nor plugin (warning)
// Exits with code 1 if there is any ERROR; warnings do not block.

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const PLUGINS_DIR = join(ROOT, 'plugins');

const ls = (d) => { try { return readdirSync(d); } catch { return []; } };
const isDir = (p) => { try { return statSync(p).isDirectory(); } catch { return false; } };

const plugins = ls(PLUGINS_DIR).filter((p) => isDir(join(PLUGINS_DIR, p)));
const skills = []; // { plugin, name, file }
for (const p of plugins) {
  const skillsDir = join(PLUGINS_DIR, p, 'skills');
  for (const s of ls(skillsDir).filter((s) => isDir(join(skillsDir, s)))) {
    skills.push({ plugin: p, name: s, file: join(skillsDir, s, 'SKILL.md') });
  }
}
const skillNames = new Set(skills.map((s) => s.name));
const pluginNames = new Set(plugins);
// Tokens that appear in prose but are NOT skills (Axis package names referenced in rule links).
const MODEL_TOKENS = new Set(['axis-framework', 'axis-result', 'axis-saga', 'axis-bus', 'axis-cache', 'axis-storage', 'axis-email', 'axis-mediator', 'axis-validator', 'axis-repository', 'axis-logger', 'axis-telemetry', 'axis-types']);

let errors = 0, warns = 0;
const err = (m) => { console.error('ERROR ' + m); errors++; };
const warn = (m) => { console.warn('warn  ' + m); warns++; };

for (const s of skills) {
  let txt;
  try { txt = readFileSync(s.file, 'utf-8'); } catch { err(`${s.plugin}/${s.name}: SKILL.md missing`); continue; }
  const m = txt.match(/^---\s*$([\s\S]*?)^---\s*$/m);
  if (!m) { err(`${s.plugin}/${s.name}: no frontmatter`); continue; }
  const fm = m[1];
  const nameMatch = fm.match(/^name:\s*(.+?)\s*$/m);
  const name = nameMatch ? nameMatch[1].trim() : '';
  if (name !== s.name) err(`${s.plugin}/${s.name}: name "${name}" != folder "${s.name}"`);
  const dMatch = fm.match(/^description:\s*(>-?|\|-?)?\s*([\s\S]*)$/m);
  let desc = '';
  if (dMatch) desc = dMatch[2].split('\n').map((l) => l.trim()).filter(Boolean).join(' ').trim();
  if (!desc) err(`${s.plugin}/${s.name}: empty description`);
  else if (desc.length > 1024) err(`${s.plugin}/${s.name}: description ${desc.length} > 1024`);
  const body = txt.slice(m.index + m[0].length);
  if (!/^##\s*See\s+also/im.test(body)) warn(`${s.plugin}/${s.name}: no "See also" section`);
  // Deep-relative links escape the installable plugin subtree and are dead in every real installation;
  // links must target the bundled copies (run skills/scripts/bundle-plugin.mjs to rewrite them).
  for (const bad of ['](../../../../../rules/', '](../../../../../docs/', '](../../../../../src/scaffolds/']) {
    if (body.includes(bad)) err(`${s.plugin}/${s.name}: dead deep-relative link "${bad}..." — run bundle-plugin.mjs so links target the bundled ../../ copies`);
  }
  const prose = body.replace(/```[\s\S]*?```/g, '');
  const refs = new Set((prose.match(/axis-[a-z0-9-]+/g) || []));
  for (const r of refs) {
    if (!skillNames.has(r) && !pluginNames.has(r) && !MODEL_TOKENS.has(r)) warn(`${s.plugin}/${s.name}: reference "${r}" resolves to neither a skill nor a plugin`);
  }
}

console.log(`\n${skills.length} skills across ${plugins.length} plugins | ${errors} error(s), ${warns} warning(s)`);
process.exit(errors ? 1 : 0);
