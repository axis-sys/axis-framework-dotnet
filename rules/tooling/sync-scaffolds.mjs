// sync-scaffolds — inject scaffold code from the Scaffolds sample solution into doc pages.
// The sample (src/scaffolds/**/*.cs) is the source of truth: each `#region scaffold:<id>` … `#endregion`
// is a real, compiled, tested snippet. Doc pages mark where a snippet goes with
// `<!-- scaffold:<id> -->` … `<!-- /scaffold -->`; this tool replaces the block body with the region.
// Default mode writes; `--check` reports drift and exits 1. Zero dependencies. Run by the session-agent.

import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(HERE, '..', '..');
const SAMPLE_DIR = join(REPO_ROOT, 'src', 'scaffolds');
// Both language trees carry the same `<!-- scaffold:id -->` blocks; the injected code is the single
// source (language-agnostic), so en-us and pt-br stay in lockstep with the sample.
const DOCS_DIRS = [join(REPO_ROOT, 'docs', 'en-us'), join(REPO_ROOT, 'docs', 'pt-br')];
const CHECK = process.argv.slice(2).includes('--check');

function walk(dir, ext, out = []) {
  let entries;
  try { entries = readdirSync(dir); } catch { return out; }
  for (const name of entries) {
    if (name === 'bin' || name === 'obj') continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, ext, out);
    else if (name.endsWith(ext)) out.push(full);
  }
  return out;
}

function dedent(lines) {
  const body = [...lines];
  while (body.length && body[0].trim() === '') body.shift();
  while (body.length && body[body.length - 1].trim() === '') body.pop();
  const indents = body.filter((l) => l.trim() !== '').map((l) => l.length - l.trimStart().length);
  const min = indents.length ? Math.min(...indents) : 0;
  return body.map((l) => l.slice(min)).join('\n');
}

// 1) Collect scaffold regions from the sample.
const regions = new Map();
const REGION = /#region\s+scaffold:([\w.-]+)\s*\r?\n([\s\S]*?)#endregion/g;
for (const file of walk(SAMPLE_DIR, '.cs')) {
  const text = readFileSync(file, 'utf8');
  let m;
  while ((m = REGION.exec(text)) !== null) {
    const id = m[1];
    if (regions.has(id)) { console.log(`ERROR duplicate scaffold: ${id}`); process.exitCode = 1; continue; }
    regions.set(id, dedent(m[2].split(/\r?\n/)));
  }
}

// 2) Inject into doc blocks.
const BLOCK = /(<!--\s*scaffold:([\w.-]+)\s*-->)([\s\S]*?)(<!--\s*\/scaffold\s*-->)/g;
let drift = 0, updated = 0, missing = 0;
for (const file of DOCS_DIRS.flatMap((d) => walk(d, '.md'))) {
  const rel = relative(REPO_ROOT, file).split(sep).join('/');
  const text = readFileSync(file, 'utf8');
  let changed = false;
  const next = text.replace(BLOCK, (whole, open, id, _body, close) => {
    if (!regions.has(id)) { console.log(`ERROR ${rel}: no scaffold:${id} region in the sample`); missing++; return whole; }
    const desired = `${open}\n\n\`\`\`csharp\n${regions.get(id)}\n\`\`\`\n\n${close}`;
    if (whole !== desired) { changed = true; if (CHECK) { console.log(`DRIFT ${rel}: block scaffold:${id} differs from the sample`); drift++; } }
    return desired;
  });
  if (changed && !CHECK) { writeFileSync(file, next); updated++; console.log(`updated ${rel}`); }
}

if (CHECK) {
  console.log(`\nsync-scaffolds --check: ${drift} drift, ${missing} block(s) without a region.`);
  process.exit(drift || missing || process.exitCode ? 1 : 0);
} else {
  console.log(`\nsync-scaffolds: ${regions.size} region(s) in the sample, ${updated} doc(s) updated, ${missing} block(s) without a region.`);
  process.exit(missing || process.exitCode ? 1 : 0);
}
