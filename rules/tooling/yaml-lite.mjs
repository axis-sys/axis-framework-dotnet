// Zero-dependency reader for the constrained YAML subset used by rules/*.yaml.
// It is deliberately strict: it understands exactly the shapes documented in _schema.md
// (scalars, folded ">" blocks, block sequences of scalars, and maps nested up to a few levels)
// and throws on anything it does not recognize, so a malformed rule fails loudly rather than
// being silently mis-parsed. Not a general YAML parser — do not use it outside rules/.

import { readdirSync, statSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const RULES_DIR = resolve(HERE, '..');
export const REPO_ROOT = resolve(HERE, '..', '..');
export const RULE_ROOTS = ['framework', 'conventions'];

function stripComment(line) {
  // Only full-line comments are supported (a leading # after optional spaces). Inline
  // comments are not stripped — rule files must not use them (see rules-guide.md).
  return /^\s*#/.test(line) ? '' : line;
}

function unquote(s) {
  const t = s.trim();
  if ((t.startsWith('"') && t.endsWith('"')) || (t.startsWith("'") && t.endsWith("'"))) {
    return t.slice(1, -1);
  }
  return t;
}

function parseInlineList(s) {
  const inner = s.trim().slice(1, -1).trim();
  if (inner === '') return [];
  return inner.split(',').map((x) => unquote(x));
}

function scalar(raw) {
  const t = raw.trim();
  if (t === '') return '';
  if (t.startsWith('[') && t.endsWith(']')) return parseInlineList(t);
  return unquote(t);
}

// lines: array of { indent, text } with comments/blank already handled by caller for blocks.
function parseBlock(lines, i, indent) {
  // Sequence?
  if (i < lines.length && lines[i].indent === indent && /^-\s*/.test(lines[i].text)) {
    const arr = [];
    while (i < lines.length && lines[i].indent === indent && /^-\s*/.test(lines[i].text)) {
      arr.push(scalar(lines[i].text.replace(/^-\s*/, '')));
      i++;
    }
    return [arr, i];
  }
  // Mapping.
  const obj = {};
  while (i < lines.length && lines[i].indent === indent) {
    const m = lines[i].text.match(/^([A-Za-z_][\w.-]*):\s*(.*)$/);
    if (!m) throw new Error(`Unparseable line: "${lines[i].text}"`);
    const key = m[1];
    const rest = m[2];
    if (rest === '>' || rest === '|') {
      // Folded/literal block: gather deeper-indented lines, join with spaces (folded) or newlines.
      i++;
      const parts = [];
      while (i < lines.length && lines[i].indent > indent) {
        parts.push(lines[i].text);
        i++;
      }
      obj[key] = parts.join(rest === '>' ? ' ' : '\n').trim();
    } else if (rest === '') {
      // Nested block (map or sequence) at greater indent, or empty value.
      if (i + 1 < lines.length && lines[i + 1].indent > indent) {
        const childIndent = lines[i + 1].indent;
        const [val, ni] = parseBlock(lines, i + 1, childIndent);
        obj[key] = val;
        i = ni;
      } else {
        obj[key] = null;
        i++;
      }
    } else {
      obj[key] = scalar(rest);
      i++;
    }
  }
  return [obj, i];
}

export function parseYaml(text) {
  const lines = [];
  for (const rawLine of text.split(/\r?\n/)) {
    const line = stripComment(rawLine);
    if (line.trim() === '') continue;
    const indent = line.length - line.trimStart().length;
    lines.push({ indent, text: line.trim() });
  }
  if (lines.length === 0) return {};
  const [obj] = parseBlock(lines, 0, lines[0].indent);
  return obj;
}

// Recursively list rule files (*.yaml) under framework/ and conventions/.
export function listRuleFiles() {
  const out = [];
  const walk = (dir) => {
    for (const name of readdirSync(dir)) {
      const full = join(dir, name);
      if (statSync(full).isDirectory()) walk(full);
      else if (name.endsWith('.yaml')) out.push(full);
    }
  };
  for (const root of RULE_ROOTS) {
    const full = join(RULES_DIR, root);
    try {
      if (statSync(full).isDirectory()) walk(full);
    } catch {
      /* root not present yet */
    }
  }
  return out.sort();
}
