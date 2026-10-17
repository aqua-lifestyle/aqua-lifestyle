import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { buildNotRunResult, canonicalJson, caseHash, validateJsonSchema, validateResultData,
  validateLinks, validateSupportedSchema, validateRepository, ValidationError } from './validate.mjs';

const root = fileURLToPath(new URL('../..', import.meta.url));
const command = fileURLToPath(new URL('./validate.mjs', import.meta.url));
const entries = JSON.parse(readFileSync(join(root, 'docs/agent-evals/cases/skill-routing.json'), 'utf8')).cases;
const cases = new Map(entries.map(entry => [entry.id, entry]));
const schema = JSON.parse(readFileSync(join(root, 'docs/agent-evals/result.schema.json'), 'utf8'));
const first = entries[0];
const passing = entry => ({ ...buildNotRunResult(entry), outcome: 'PASS', skills_loaded: [...entry.expected_skills],
  assertions: entry.expected_assertions.map(item => ({ id: item.id, outcome: 'PASS', evidence: 'synthetic test only' })) });

test('CLI validates repository and built-in negatives from outside its working directory', () => {
  const result = spawnSync(process.execPath, [command], { cwd: tmpdir(), encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /PASS: 6 skills, 16 routing cases/u);
});

test('canonical JSON keeps sorted keys, UTF-8 and compact encoding for case hashes', () => {
  assert.equal(canonicalJson({ z: ['é', { b: false, a: null }], a: '雪' }), '{"a":"雪","z":["é",{"a":null,"b":false}]}');
  assert.equal(canonicalJson({ 2: 'two', 10: 'ten' }), '{"10":"ten","2":"two"}');
  assert.equal(caseHash({ b: 2, a: 1 }), '43258cff783fe7036d8a43033f830adfc60ec037382473548ac742b888292777');
});

test('every routing case accepts coherent NOT_RUN and successful results', () => {
  for (const entry of entries) {
    validateResultData(buildNotRunResult(entry), entry.id, cases, schema);
    validateResultData(passing(entry), entry.id, cases, schema);
  }
});

test('dirty evidence requires the same full commit and an exact diff hash', () => {
  const data = passing(first);
  data.worktree_dirty = true;
  data.snapshot_identity += `+diff-sha256:${'a'.repeat(64)}`;
  validateResultData(data, 'synthetic-dirty', cases, schema);
  for (const identity of [`git:${'1'.repeat(40)}+diff-sha256:${'a'.repeat(64)}`, `${data.snapshot_identity}\n`, `git:${data.commit}`]) {
    assert.throws(() => validateResultData({ ...data, snapshot_identity: identity }, 'synthetic-dirty', cases, schema), ValidationError);
  }
});

const invalidResults = [
  ['unknown field', data => { data.extra = true; }],
  ['wrong boolean type', data => { data.worktree_dirty = 'false'; }],
  ['unknown case', data => { data.case_id = 'unknown-case'; }],
  ['prototype-named case', data => { data.case_id = 'constructor'; }],
  ['forged case hash', data => { data.case_definition_sha256 = '0'.repeat(64); }],
  ['different classification', data => { data.trigger_expectation = 'negative'; }],
  ['missing expected skill', data => { data.expected_skills = []; }],
  ['unloaded required skill', data => { data.skills_loaded = []; }],
  ['unknown loaded skill', data => { data.skills_loaded = ['unknown-skill']; }],
  ['failed assertion', data => { data.assertions[0].outcome = 'FAIL'; }],
  ['unchecked assertion', data => { data.assertions[0].outcome = 'NOT_CHECKED'; }],
  ['missing assertion', data => { data.assertions.pop(); }],
  ['renamed assertion', data => { data.assertions[0].id = 'different'; }],
  ['duplicate assertion', data => { data.assertions.push(structuredClone(data.assertions[0])); }],
  ['commit trailing newline', data => { data.commit += '\n'; data.snapshot_identity = `git:${data.commit}`; }],
  ['NOT_RUN claiming execution', data => { data.outcome = 'NOT_RUN'; }],
  ['negative metric', data => { data.metrics = { elapsed_seconds: -1 }; }],
  ['boolean integer metric', data => { data.metrics = { input_tokens: true }; }],
];
for (const [label, mutate] of invalidResults) test(`rejects ${label}`, () => {
  const data = passing(first);
  mutate(data);
  assert.throws(() => validateResultData(data, 'synthetic-invalid', cases, schema), ValidationError);
});

test('composition PASS requires every expected skill in the selected order', () => {
  const entry = entries.find(item => item.trigger_expectation === 'composition');
  const data = passing(entry);
  data.skills_loaded.reverse();
  assert.throws(() => validateResultData(data, 'synthetic-order', cases, schema), ValidationError);
});

test('schema rejects unsupported keywords and structured duplicates', () => {
  assert.throws(() => validateSupportedSchema({ properties: { hidden: { oneOf: [] } } }), ValidationError);
  assert.throws(() => validateSupportedSchema({ properties: null }), ValidationError);
  assert.throws(() => validateJsonSchema([{ a: 1, b: 2 }, { b: 2, a: 1 }], { uniqueItems: true }), ValidationError);
  assert.throws(() => validateJsonSchema('value\n', { pattern: 'value' }), ValidationError);
  validateJsonSchema('😀', { minLength: 1 });
  assert.throws(() => validateJsonSchema('😀', { minLength: 2 }), ValidationError);
});

test('repository mutations fail on missing safeguards, duplicate skills, routing drift and schema drift', t => {
  const directory = mkdtempSync(join(tmpdir(), 'aqua-agent-structure-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  mkdirSync(join(directory, '.agents'), { recursive: true });
  mkdirSync(join(directory, 'docs/agent-evals/cases'), { recursive: true });
  cpSync(join(root, '.agents/skills'), join(directory, '.agents/skills'), { recursive: true });
  const agents = readFileSync(join(root, 'AGENTS.md'), 'utf8');
  writeFileSync(join(directory, 'AGENTS.md'), agents.replace('Backend authorization is authoritative', 'Synthetic missing safeguard'));
  assert.throws(() => validateRepository(directory), /missing no-skill safeguard/u);
  writeFileSync(join(directory, 'AGENTS.md'), agents);
  const skill = join(directory, '.agents/skills/diagnose-bug/SKILL.md');
  const skillText = readFileSync(skill, 'utf8');
  writeFileSync(skill, skillText.replace('name: diagnose-bug', 'name: diagnose-bug\nname: diagnose-bug'));
  assert.throws(() => validateRepository(directory), /duplicate name/u);
  writeFileSync(skill, skillText);
  const casePath = join(directory, 'docs/agent-evals/cases/skill-routing.json');
  const definitions = JSON.parse(readFileSync(join(root, 'docs/agent-evals/cases/skill-routing.json'), 'utf8'));
  definitions.cases.push(structuredClone(definitions.cases[0]));
  writeFileSync(casePath, JSON.stringify(definitions));
  assert.throws(() => validateRepository(directory), /duplicate case ID/u);
  definitions.cases.pop();
  const stable = definitions.cases.find(entry => entry.authority_mode === 'stable-synthetic');
  const prompt = stable.prompt;
  stable.prompt = 'Synthetic AQG-V2-D10';
  writeFileSync(casePath, JSON.stringify(definitions));
  assert.throws(() => validateRepository(directory), /mutable authority term/u);
  stable.prompt = prompt;
  writeFileSync(casePath, JSON.stringify(definitions));
  writeFileSync(join(directory, 'docs/agent-evals/result.schema.json'), JSON.stringify({ ...schema, oneOf: [] }));
  assert.throws(() => validateRepository(directory), /unsupported keywords/u);
});

test('CLI accepts a coherent file and exits nonzero for malformed or invalid evidence', t => {
  const directory = mkdtempSync(join(tmpdir(), 'aqua-agent-evals-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const path = join(directory, 'synthetic-result.json');
  const run = () => spawnSync(process.execPath, [command, path], { cwd: directory, encoding: 'utf8' });
  writeFileSync(path, JSON.stringify(passing(first)));
  assert.equal(run().status, 0);
  for (const value of ['{', 'null', '[]', '{"outcome":"PASS"}']) {
    writeFileSync(path, value);
    const result = run();
    assert.equal(result.status, 1);
    assert.match(result.stderr, /^FAIL:/u);
    assert.doesNotMatch(result.stdout, /PASS:/u);
  }
  rmSync(path);
  assert.equal(run().status, 1);
});

test('CLI aliases execute validation, including when preserving main-module symlinks', t => {
  const directory = mkdtempSync(join(tmpdir(), 'aqua-agent-alias-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const alias = join(directory, 'validate.mjs');
  symlinkSync(command, alias, 'file');
  for (const flags of [[], ['--preserve-symlinks-main']]) {
    const good = spawnSync(process.execPath, [...flags, alias], { cwd: directory, encoding: 'utf8' });
    assert.equal(good.status, 0, good.stderr);
    assert.match(good.stdout, /^PASS: 6 skills/u);
    const bad = spawnSync(process.execPath, [...flags, alias, join(directory, 'missing.json')], { cwd: directory, encoding: 'utf8' });
    assert.equal(bad.status, 1);
    assert.match(bad.stderr, /^FAIL:/u);
    assert.doesNotMatch(bad.stdout, /PASS:/u);
  }
});

test('Markdown file symlinks validate their links and dangling sources fail visibly', t => {
  const directory = mkdtempSync(join(tmpdir(), 'aqua-agent-links-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const files = ['AGENTS.md', 'AqualLifeStyle/9.4.2/aqua-frontend/AGENTS.md',
    'docs/aqua-system/README.md', 'docs/aqua-system/authority.md', 'docs/agent-instruction-system.md'];
  for (const path of files) {
    mkdirSync(dirname(join(directory, path)), { recursive: true });
    writeFileSync(join(directory, path), 'Synthetic link-only fixture.\n');
  }
  for (const path of ['.agents/skills', 'docs/exec-plans', 'docs/agent-evals']) mkdirSync(join(directory, path), { recursive: true });
  const source = join(directory, '.agents/skills/linked.md');
  const target = join(directory, 'synthetic.md');
  const authority = join(directory, 'docs/aqua-system/authority.md');
  symlinkSync(target, source, 'file');
  writeFileSync(target, '[valid](../../AGENTS.md)\n');
  validateLinks(directory, authority);
  writeFileSync(target, '[broken](missing.md)\n');
  assert.throws(() => validateLinks(directory, authority), /broken local link/u);
  rmSync(target);
  assert.throws(() => validateLinks(directory, authority), /ENOENT/u);
});

test('CLI rejects malformed UTF-8 and BOM-prefixed result JSON instead of normalizing evidence', t => {
  const directory = mkdtempSync(join(tmpdir(), 'aqua-agent-encoding-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const path = join(directory, 'result.json');
  const data = { ...passing(first), model: 'synthetic-encoding-marker' };
  const encoded = Buffer.from(JSON.stringify(data));
  const corrupt = Buffer.from(encoded);
  corrupt[corrupt.indexOf('synthetic-encoding-marker')] = 0xff;
  for (const bytes of [corrupt, Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), encoded])]) {
    writeFileSync(path, bytes);
    const result = spawnSync(process.execPath, [command, path], { encoding: 'utf8' });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /^FAIL:/u);
    assert.doesNotMatch(result.stdout, /PASS:/u);
  }
});
