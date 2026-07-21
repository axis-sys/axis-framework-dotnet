// check-freshness — report rules whose ledger.reviewed_against is behind the current framework HEAD.
// This is the review backlog that drives per-cluster sessions. Report only; always exits 0.
// Zero dependencies.

import { readFileSync } from 'node:fs';
import { basename, relative, sep } from 'node:path';
import { execSync } from 'node:child_process';
import { parseYaml, listRuleFiles, RULES_DIR, REPO_ROOT } from './yaml-lite.mjs';

let head = 'UNKNOWN';
try {
  // Framework HEAD = the last commit that touched src/ (the code the rules describe). Rules/skills/docs-only
  // commits never advance it, so a ratified rule stays current until the framework CODE actually changes.
  head = execSync('git log -1 --format=%H -- src', { cwd: REPO_ROOT }).toString().trim();
} catch {
  console.log('check-freshness: not a git repo or git unavailable; cannot compare against HEAD.');
}
const shortHead = head.slice(0, 12);

const stale = [];
const unstamped = [];
const current = [];

for (const file of listRuleFiles()) {
  const r = parseYaml(readFileSync(file, 'utf8'));
  const rel = relative(RULES_DIR, file).split(sep).join('/');
  const id = r.id || basename(file, '.yaml');
  const ra = (r.ledger && r.ledger.reviewed_against) || '';
  if (!ra || ra === 'TBD') unstamped.push({ id, rel });
  else if (head !== 'UNKNOWN' && !head.startsWith(ra) && !ra.startsWith(shortHead)) stale.push({ id, rel, ra });
  else current.push({ id, rel });
}

console.log(`Framework HEAD: ${shortHead}`);
console.log(`current: ${current.length} · stale: ${stale.length} · unstamped: ${unstamped.length}\n`);
if (stale.length) {
  console.log('STALE (reviewed against an older commit — schedule a session):');
  for (const s of stale) console.log(`  ${s.rel}  (reviewed_against ${s.ra})`);
  console.log('');
}
if (unstamped.length) {
  console.log('UNSTAMPED (never ratified against a commit):');
  for (const u of unstamped) console.log(`  ${u.rel}`);
}
process.exit(0);
