import { readdirSync } from 'node:fs';

const root = new URL('../openspec/changes/', import.meta.url);
const invalid = readdirSync(root, { withFileTypes: true })
  .filter(entry => entry.isDirectory() && entry.name !== 'archive' && !entry.name.startsWith('.'))
  .map(entry => entry.name)
  .filter(name => !/^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/.test(name));
if (invalid.length) {
  console.error('Invalid OpenSpec change names:\n' + invalid.join('\n'));
  process.exitCode = 1;
} else {
  console.log('OpenSpec change names: PASS');
}
