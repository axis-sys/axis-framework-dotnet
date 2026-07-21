// lint-rules — validate every rules/*.yaml against _schema.md.
// Errors (exit 1): missing required field, bad enum, id != file name, duplicate id,
//   folder/tier disagreement, a reference/example path that does not exist on disk.
// Warnings (exit 0): missing derives_to, doc target not yet generated, ledger not stamped.
// Zero dependencies.

import { readFileSync, existsSync, readdirSync, statSync } from 'node:fs';
import { basename, dirname, relative, join, sep } from 'node:path';
import { parseYaml, listRuleFiles, RULES_DIR, REPO_ROOT } from './yaml-lite.mjs';

const TIERS = ['framework', 'convention'];
const FAMILIES = ['0-foundations', '1-observability', '2-application-flow', '3-infra', '4-edge'];
const CONCERNS = ['package-mechanics', 'architecture', 'domain', 'persistence', 'edge', 'testing', 'style', 'process'];
const LAYERS = ['kernel', 'domain', 'application', 'contracts', 'driven', 'driving', 'tests', 'build', 'any'];
const SEVERITIES = ['must', 'should', 'may'];
const STATUSES = ['canonical', 'proposed', 'stale', 'superseded'];
const PACKAGE_KEBAB = {
  AxisResult: 'axis-result', AxisTypes: 'axis-types', 'AxisMediator.Contracts': 'axis-mediator-contracts',
  AxisDependencyInjections: 'axis-dependency-injections',
  AxisLogger: 'axis-logger', AxisTelemetry: 'axis-telemetry', AxisMediator: 'axis-mediator',
  AxisSaga: 'axis-saga', AxisValidator: 'axis-validator', AxisBus: 'axis-bus',
  // AxisRepository hosts two distinct rule-cluster folders: the runtime data-access side (axis-repository)
  // and the schema/DDL/migration subsystem it ships (axis-migrations). A package may map to several folders.
  AxisRepository: ['axis-repository', 'axis-migrations'],
  AxisCache: 'axis-cache', AxisStorage: 'axis-storage', AxisEmail: 'axis-email',
  'AxisResult.HttpResponse': 'axis-result-httpresponse',
};

// Anchor ratchet — a must-severity architecture/edge/testing convention rule is exactly the kind a
// reader (human or AI) resolves role-only prose against, so it may not merge with an empty anchor:
// reference.at or examples.correct must point at real code (src/scaffolds or framework source).
// GRANDFATHERED_NO_ANCHOR is the pre-existing debt at the time this gate landed — it only ever
// shrinks; never add an id to it.
const ANCHOR_REQUIRED_CONCERNS = ['architecture', 'edge', 'testing'];
const GRANDFATHERED_NO_ANCHOR = new Set([
  // The scaffold has no honest anchor for these yet — each needs real code, not just a YAML pointer:
  'architecture-workers-vertical-slices',   // no background-worker slice exists in the sample
  'edge-permission-authorization',          // only a coarse claim-policy exists; the granular filter + authz facade is unbuilt
  'edge-tenant-scoping-middleware',         // scaffold is deliberately single-tenant (the rule's own exception)
  'testing-concurrency-invariants-both-dbs', // no concurrency-invariant race test exists
]);

// derives_to.scaffolds ids must resolve to a real `#region scaffold:<id>` marker in the sample.
function collectScaffoldRegions() {
  const regions = new Set();
  const walk = (dir) => {
    let entries;
    try { entries = readdirSync(dir); } catch { return; }
    for (const entry of entries) {
      const full = join(dir, entry);
      if (statSync(full).isDirectory()) {
        if (entry === 'bin' || entry === 'obj' || entry === '.idea') continue;
        walk(full);
      } else if (entry.endsWith('.cs')) {
        for (const m of readFileSync(full, 'utf8').matchAll(/#region scaffold:([\w-]+)/g)) regions.add(m[1]);
      }
    }
  };
  walk(join(REPO_ROOT, 'src', 'scaffolds'));
  return regions;
}
const SCAFFOLD_REGIONS = collectScaffoldRegions();

const errors = [];
const warnings = [];
const seenIds = new Map();

function anchorPath(entry) {
  // "relative/path.cs:12-20 — note" -> "relative/path.cs"
  const token = String(entry).trim().split(/\s+/)[0];
  return token.replace(/:\d+(-\d+)?$/, '');
}

function anchorLine(entry) {
  // "relative/path.cs:12-20 — note" -> 12 (null when the anchor carries no line)
  const token = String(entry).trim().split(/\s+/)[0];
  const m = token.match(/:(\d+)(-\d+)?$/);
  return m ? Number(m[1]) : null;
}

const fileLineCounts = new Map();
function lineCount(absPath) {
  if (!fileLineCounts.has(absPath)) {
    try { fileLineCounts.set(absPath, readFileSync(absPath, 'utf8').split('\n').length); }
    catch { fileLineCounts.set(absPath, null); }
  }
  return fileLineCounts.get(absPath);
}

for (const file of listRuleFiles()) {
  const rel = relative(RULES_DIR, file).split(sep).join('/');
  const at = (msg) => `${rel}: ${msg}`;
  let rule;
  try {
    rule = parseYaml(readFileSync(file, 'utf8'));
  } catch (e) {
    errors.push(at(`parse error — ${e.message}`));
    continue;
  }

  const stem = basename(file, '.yaml');
  const dirRel = relative(RULES_DIR, dirname(file)).split(sep).join('/');

  // id
  if (!rule.id) errors.push(at('missing id'));
  else {
    if (rule.id !== stem) errors.push(at(`id "${rule.id}" != file name "${stem}"`));
    if (seenIds.has(rule.id)) errors.push(at(`duplicate id "${rule.id}" (also ${seenIds.get(rule.id)})`));
    else seenIds.set(rule.id, rel);
  }

  // required scalar fields
  for (const f of ['title', 'tier', 'concern', 'layer', 'severity', 'status', 'description']) {
    if (!rule[f]) errors.push(at(`missing ${f}`));
  }
  if (!Array.isArray(rule.invariants) || rule.invariants.length === 0) errors.push(at('missing/empty invariants'));

  // enums
  if (rule.tier && !TIERS.includes(rule.tier)) errors.push(at(`bad tier "${rule.tier}"`));
  if (rule.concern && !CONCERNS.includes(rule.concern)) errors.push(at(`bad concern "${rule.concern}"`));
  if (rule.layer && !LAYERS.includes(rule.layer)) errors.push(at(`bad layer "${rule.layer}"`));
  if (rule.severity && !SEVERITIES.includes(rule.severity)) errors.push(at(`bad severity "${rule.severity}"`));
  if (rule.status && !STATUSES.includes(rule.status)) errors.push(at(`bad status "${rule.status}"`));

  // tier <-> folder agreement
  if (rule.tier === 'framework') {
    if (!rule.family || !FAMILIES.includes(rule.family)) errors.push(at(`framework rule needs valid family (got "${rule.family}")`));
    if (!rule.package || !PACKAGE_KEBAB[rule.package]) errors.push(at(`framework rule needs valid package (got "${rule.package}")`));
    else {
      const folders = [].concat(PACKAGE_KEBAB[rule.package]);
      const allowed = folders.map((f) => `framework/${rule.family}/${f}`);
      if (!allowed.includes(dirRel)) errors.push(at(`framework rule in "${dirRel}", expected ${allowed.map((a) => `"${a}"`).join(' or ')}`));
    }
  } else if (rule.tier === 'convention') {
    if (rule.family || rule.package) warnings.push(at('convention rule should have null family/package'));
    if (rule.concern) {
      const expect = `conventions/${rule.concern}`;
      if (dirRel !== expect) errors.push(at(`convention rule in "${dirRel}", expected "${expect}"`));
    }
  }

  // reference + examples paths must exist
  const checkPaths = (list, label, hard) => {
    if (!Array.isArray(list)) return;
    for (const entry of list) {
      const p = anchorPath(entry);
      if (!p || p === 'none' || p === 'TBD') continue;
      const abs = join(REPO_ROOT, p);
      if (!existsSync(abs)) {
        (hard ? errors : warnings).push(at(`${label} path not found: ${p}`));
        continue;
      }
      const line = anchorLine(entry);
      const total = line === null ? null : lineCount(abs);
      if (line !== null && total !== null && line > total)
        warnings.push(at(`${label} anchor line ${line} is past the end of ${p} (${total} lines) — stale anchor`));
    }
  };
  if (rule.reference && rule.reference.kind !== 'none-yet') checkPaths(rule.reference.at, 'reference.at', true);
  if (rule.examples) {
    checkPaths(rule.examples.correct, 'examples.correct', true);
    checkPaths(rule.examples.incorrect, 'examples.incorrect', true);
  }

  // anchor ratchet (hard for new/touched rules; grandfathered debt warns and only shrinks)
  if (rule.tier === 'convention' && rule.severity === 'must' && ANCHOR_REQUIRED_CONCERNS.includes(rule.concern)) {
    const realAnchors = (list) => (Array.isArray(list) ? list : []).filter((entry) => {
      const p = anchorPath(entry);
      return p && p !== 'none' && p !== 'TBD';
    });
    const hasReference = !!rule.reference && rule.reference.kind !== 'none-yet'
      && realAnchors(rule.reference.at).length > 0;
    const hasExample = !!rule.examples && realAnchors(rule.examples.correct).length > 0;
    if (!hasReference && !hasExample) {
      const msg = `must-severity ${rule.concern} rule has no concrete anchor — point reference.at or examples.correct at real code ("kind: none-yet" and placeholder entries are not accepted for ${ANCHOR_REQUIRED_CONCERNS.join('/')} at severity must)`;
      (GRANDFATHERED_NO_ANCHOR.has(rule.id) ? warnings : errors).push(at(msg));
    } else if (GRANDFATHERED_NO_ANCHOR.has(rule.id)) {
      // The debt list only shrinks: once a grandfathered rule gains its anchor, remove it from the
      // list so a later regression to none-yet becomes a hard error, not a tolerated warning.
      errors.push(at('rule is anchored but still listed in GRANDFATHERED_NO_ANCHOR — remove its id from the list in lint-rules.mjs'));
    }
  }

  // derives_to (docs soft; scaffold ids hard — every id must match a #region scaffold:<id> in the sample)
  if (!rule.derives_to) warnings.push(at('missing derives_to'));
  else {
    if (Array.isArray(rule.derives_to.docs)) checkPaths(rule.derives_to.docs, 'derives_to.docs', false);
    if (Array.isArray(rule.derives_to.scaffolds)) {
      for (const id of rule.derives_to.scaffolds) {
        if (!SCAFFOLD_REGIONS.has(String(id).trim()))
          errors.push(at(`derives_to.scaffolds id "${id}" has no matching '#region scaffold:${id}' under src/scaffolds`));
      }
    }
  }

  // ledger (soft)
  if (rule.status === 'canonical') {
    const ra = rule.ledger && rule.ledger.reviewed_against;
    if (!ra || ra === 'TBD') warnings.push(at('status canonical but ledger.reviewed_against not stamped'));
  }
}

const total = seenIds.size;
for (const w of warnings) console.log(`  warn  ${w}`);
for (const e of errors) console.log(`  ERROR ${e}`);
console.log(`\nlint-rules: ${total} rule(s), ${errors.length} error(s), ${warnings.length} warning(s).`);
process.exit(errors.length ? 1 : 0);
