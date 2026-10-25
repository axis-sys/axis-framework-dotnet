#!/usr/bin/env node
// Collects the articles of every package repository listed in site/packages.json and the package list the API
// reference is built from.
//
//   node site/collect-docs.mjs <root>
//
// <root> holds the package repositories side by side (<root>/<repository>/docs/). It writes site/articles/ (the
// articles of each repository, their index and toc.yml), site/api/index.md and site/ApiReference/Packages.g.props, all
// generated and git-ignored; docfx metadata then fills site/api/ from the assemblies ApiReference lays out.
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const site = dirname(fileURLToPath(import.meta.url));
const root = process.argv[2];
if (!root) {
  console.error('usage: node site/collect-docs.mjs <folder holding the package repositories>');
  process.exit(2);
}

const repositories = JSON.parse(readFileSync(join(site, 'packages.json'), 'utf8'));
const articles = join(site, 'articles');
const api = join(site, 'api');
for (const folder of [articles, api]) {
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
}

const toc = ['- name: Overview', '  href: index.md'];
const rows = [];
const packages = [];
for (const { repository, packages: ids } of repositories) {
  const docs = join(resolve(root), repository, 'docs');
  if (!existsSync(join(docs, 'toc.yml'))) {
    console.error(`missing ${join(docs, 'toc.yml')}: clone ${repository} under ${resolve(root)}`);
    process.exit(1);
  }
  cpSync(docs, join(articles, repository), { recursive: true });
  toc.push(`- name: ${ids[0]}`, `  href: ${repository}/toc.yml`, `  topicHref: ${repository}/index.md`);
  rows.push({ repository, ids });
  packages.push(...ids);
  console.log(`collected ${repository} (${ids.join(', ')})`);
}
writeFileSync(join(articles, 'toc.yml'), `${toc.join('\n')}\n`);

const table = articlesFolder => [
  '| Package | Ids | Repository |',
  '|---|---|---|',
  ...rows.map(({ repository, ids }) =>
    `| [${ids[0]}](${articlesFolder}${repository}/index.md) | ${ids.map(id => `\`${id}\``).join(', ')} `
      + `| [${repository}](https://github.com/axis-sys/${repository}) |`),
].join('\n');
writeFileSync(
  join(articles, 'index.md'),
  `# Packages\n\nThe articles of each package, taken from the \`docs/\` folder of its repository.\n\n${table('')}\n`,
);
writeFileSync(
  join(api, 'index.md'),
  '# API reference\n\nGenerated from the XML documentation of every public member of the packages below, the same text '
    + `IntelliSense shows. Pick a namespace in the menu.\n\n${table('../articles/')}\n`,
);

const references = packages.map(id => `    <PackageReference Include="${id}" />\n    <ApiReferencePackage Include="${id}" />`);
writeFileSync(
  join(site, 'ApiReference', 'Packages.g.props'),
  `<Project>\n  <ItemGroup>\n${references.join('\n')}\n  </ItemGroup>\n</Project>\n`,
);
