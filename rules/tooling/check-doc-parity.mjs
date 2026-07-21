// check-doc-parity — guard docs/pt-br/** as a faithful STRUCTURAL projection of docs/en-us/**.
// The pt-br tree is a translation, not a divergent source: every en-us page must have a pt-br
// counterpart with the same heading skeleton, the same number of fenced code blocks and the same set
// of relative links. It catches the class of silent gap nothing else checks (sync-scaffolds only walks
// en-us; bundle-plugin copies both verbatim): a missing section/example/table that drops a heading or
// code block, a dropped cross-link, or a page never translated. It does NOT judge translation wording.
// Zero dependencies. Reports drift and exits 1. Run in CI next to lint-rules / sync-scaffolds.
//
// Ratcheted: GRANDFATHERED carries the drift that predates this guard so it can go live now and block
// NEW drift. The list only shrinks — bring a page to parity, then delete its entries here (CI fails if
// a listed entry is already clean, so a fix and its de-listing land in the same change).

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(HERE, '..', '..');
const EN_DIR = join(REPO_ROOT, 'docs', 'en-us');
const PT_DIR = join(REPO_ROOT, 'docs', 'pt-br');

// signature = `${pt-br path} :: ${kind}`, kind ∈ missing | headings | codeblocks | links.
// Empty: the pt-br tree is at full structural parity with en-us. Add an entry only to grandfather a
// genuinely pre-existing gap that cannot be fixed in the same change — the list only shrinks.
const GRANDFATHERED = new Set([]);

function walk(dir, ext, out = []) {
  let entries;
  try { entries = readdirSync(dir); } catch { return out; }
  for (const name of entries) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, ext, out);
    else if (name.endsWith(ext)) out.push(full);
  }
  return out;
}

const rel = (f) => relative(REPO_ROOT, f).split(sep).join('/');

// Structural signature of a markdown page. Content inside fenced code blocks is ignored: a `#` there is
// C# `#region`/`#if`, not a heading, and a `](...)` there is code, not a cross-reference.
function signature(text) {
  const headings = []; // sequence of heading depths (1..6), in document order
  const links = [];    // relative link targets (not http/mailto/anchor), lang-neutralised
  let codeBlocks = 0;
  let inFence = false;
  let marker = '';
  const LINK = /\]\(([^)]+)\)/g;
  for (const line of text.split(/\r?\n/)) {
    const fence = line.match(/^\s*(```+|~~~+)/);
    if (fence) {
      if (!inFence) { inFence = true; marker = fence[1][0]; codeBlocks++; }
      else if (line.trimStart().startsWith(marker)) inFence = false;
      continue;
    }
    if (inFence) continue;
    const h = line.match(/^(#{1,6})\s+\S/);
    if (h) headings.push(h[1].length);
    let m;
    while ((m = LINK.exec(line)) !== null) {
      const target = m[1].trim().split(/\s+/)[0].split('#')[0]; // drop optional "title" and #anchor
      if (!target || /^(https?:|mailto:)/.test(target)) continue;
      // A link crossing the language boundary is the en<->pt switcher: it is SUPPOSED to differ
      // (en pages point at ../pt-br/…, pt pages at ../en-us/…). Neutralise so it is not flagged.
      links.push(target.replace(/\/(en-us|pt-br)\//g, '/<lang>/'));
    }
  }
  return { headings, links: links.sort(), codeBlocks };
}

const drifts = []; // { ptRel, sig, msg }
const addDrift = (ptRel, kind, msg) => drifts.push({ ptRel, sig: `${ptRel} :: ${kind}`, msg });

const enFiles = walk(EN_DIR, '.md');
for (const enFile of enFiles) {
  const ptFile = join(PT_DIR, relative(EN_DIR, enFile));
  const ptRel = rel(ptFile);
  let ptText;
  try { ptText = readFileSync(ptFile, 'utf8'); }
  catch { addDrift(ptRel, 'missing', `missing pt-br mirror of ${rel(enFile)}`); continue; }

  const en = signature(readFileSync(enFile, 'utf8'));
  const pt = signature(ptText);

  if (en.headings.length !== pt.headings.length || en.headings.some((v, i) => v !== pt.headings[i]))
    addDrift(ptRel, 'headings', `heading skeleton differs (en ${en.headings.length} [${en.headings.join('')}], pt ${pt.headings.length} [${pt.headings.join('')}])`);
  if (en.codeBlocks !== pt.codeBlocks)
    addDrift(ptRel, 'codeblocks', `code-block count differs (en ${en.codeBlocks}, pt ${pt.codeBlocks})`);
  const onlyEn = [...new Set(en.links.filter((l) => !pt.links.includes(l)))];
  const onlyPt = [...new Set(pt.links.filter((l) => !en.links.includes(l)))];
  if (onlyEn.length || onlyPt.length)
    addDrift(ptRel, 'links', `relative links differ (only-en: [${onlyEn.join(', ')}], only-pt: [${onlyPt.join(', ')}])`);
}

const seen = new Set(drifts.map((d) => d.sig));
const fresh = drifts.filter((d) => !GRANDFATHERED.has(d.sig));
const known = drifts.filter((d) => GRANDFATHERED.has(d.sig));
const resolved = [...GRANDFATHERED].filter((sig) => !seen.has(sig));

for (const d of fresh) console.log(`DRIFT ${d.msg.startsWith('missing') ? '' : d.ptRel + ': '}${d.msg}`);
for (const sig of resolved) console.log(`RESOLVED ${sig} — now at parity; delete it from GRANDFATHERED.`);

// pt-br pages with no en-us source are allowed (e.g. a pt-only index): reported, not failed.
const orphans = walk(PT_DIR, '.md').filter((f) => {
  try { statSync(join(EN_DIR, relative(PT_DIR, f))); return false; } catch { return true; }
});
if (orphans.length) console.log(`\nnote: ${orphans.length} pt-br page(s) with no en-us source (allowed): ${orphans.map(rel).join(', ')}`);

console.log(`\ncheck-doc-parity: ${fresh.length} new drift(s), ${resolved.length} resolved-but-listed, ${known.length} grandfathered across ${enFiles.length} en-us page(s).`);
process.exit(fresh.length || resolved.length ? 1 : 0);
