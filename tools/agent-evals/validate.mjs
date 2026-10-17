#!/usr/bin/env node
// Validate skill structure, routing fixtures, result schemas and evidence claims.
// Uses only the Node.js runtime already required by repository tooling.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, realpathSync } from 'node:fs';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isDeepStrictEqual, TextDecoder } from 'node:util';

const MODULE_PATH = realpathSync(fileURLToPath(import.meta.url));
const ROOT = resolve(dirname(MODULE_PATH), '../..');
const EXPECTED_SKILLS = new Set(['resolve-authority', 'diagnose-bug', 'review-change',
  'verify-stateful-change', 'validate-evidence', 'verify-workflow']);
const NAME_PATTERN = '[a-z0-9]+(?:-[a-z0-9]+)*';
const CASE_KEYS = ['id', 'trigger_expectation', 'skills_under_test', 'expected_skills',
  'forbidden_skills', 'authority_mode', 'prompt', 'expected_assertions'];
const SCHEMA_KEYS = new Set(['$schema', 'title', 'type', 'additionalProperties', 'required',
  'properties', 'enum', 'pattern', 'minLength', 'minimum', 'minItems', 'uniqueItems', 'items']);
const ROOT_PHRASES = ['Backend authorization is authoritative', 'UI visibility is not authorization',
  'direct API or legacy paths must not bypass', 'Changes to shared durable state must preserve atomicity',
  'database constraints', 'idempotency', 'retry safety', 'concurrency correctness',
  'Migrations and historical operations must not fabricate or destroy',
  'SQLite or mock evidence alone cannot establish migration safety', 'Never make tests or validation green'];
const AUTHORITY_PHRASES = ['`UNRESOLVED`', '`PROPOSED`', '`CONFIRMED`', '`SUPERSEDED`', 'decision ID',
  'authorizing owner identity or role', 'confirmation date', 'durable source or evidence locator',
  'effective boundary', 'superseded decision ID'];

export class ValidationError extends Error {}
const check = (condition, message) => { if (!condition) throw new ValidationError(message); };
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
// Reject corrupt evidence instead of silently substituting replacement characters.
// Retain a BOM as text, matching strict UTF-8 reads rather than normalizing inputs.
const decoder = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true });
const read = path => decoder.decode(readFileSync(path));
const readJson = path => JSON.parse(read(path));
const length = text => [...text].length;
// Unlike a bare JS "$", this also rejects a trailing newline.
const fullMatch = (pattern, value) => typeof value === 'string' &&
  new RegExp(`^(?:${pattern})(?![\\s\\S])`, 'u').test(value);
const sameKeys = (value, keys) => object(value) &&
  isDeepStrictEqual(Object.keys(value).sort(), [...keys].sort());

export function canonicalJson(value) {
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`;
  if (object(value)) return `{${Object.keys(value).sort().map(key =>
    `${JSON.stringify(key)}:${canonicalJson(value[key])}`).join(',')}}`;
  check(value === null || ['string', 'boolean', 'number'].includes(typeof value), 'Non-JSON value');
  check(typeof value !== 'number' || Number.isFinite(value), 'Non-finite JSON number');
  return JSON.stringify(value);
}

export const caseHash = value => createHash('sha256').update(canonicalJson(value), 'utf8').digest('hex');

function frontmatter(path, root) {
  const lines = read(path).split(/\r?\n/u);
  const label = relative(root, path);
  check(lines[0] === '---', `${label}: missing opening frontmatter`);
  const end = lines.indexOf('---', 1);
  check(end !== -1, `${label}: missing closing frontmatter`);
  const metadata = Object.create(null);
  for (const line of lines.slice(1, end)) {
    if (!line.trim()) continue;
    check(line.includes(':'), `${label}: unsupported frontmatter line`);
    const colon = line.indexOf(':');
    const key = line.slice(0, colon).trim();
    const value = line.slice(colon + 1).trim().replace(/^"+|"+$/gu, '').replace(/^'+|'+$/gu, '');
    check(!Object.hasOwn(metadata, key), `${label}: duplicate ${key}`);
    metadata[key] = value;
  }
  check(sameKeys(metadata, ['name', 'description']), `${label}: frontmatter must contain only name and description`);
  return metadata;
}

function validateSkills(root) {
  const skillsRoot = join(root, '.agents/skills');
  const paths = readdirSync(skillsRoot, { withFileTypes: true })
    .filter(entry => entry.isDirectory() && existsSync(join(skillsRoot, entry.name, 'SKILL.md')))
    .map(entry => join(skillsRoot, entry.name, 'SKILL.md')).sort();
  const discovered = new Set();
  for (const path of paths) {
    const { name, description } = frontmatter(path, root);
    check(fullMatch(NAME_PATTERN, name) && length(name) <= 64, `${relative(root, path)}: invalid skill name`);
    check(basename(dirname(path)) === name, `${relative(root, path)}: name does not match directory`);
    check(length(description) >= 1 && length(description) <= 1024, `${relative(root, path)}: description must be 1-1024 characters`);
    check(!discovered.has(name), `duplicate skill ID ${name}`);
    discovered.add(name);
  }
  check(isDeepStrictEqual(discovered, EXPECTED_SKILLS), 'skill set mismatch');
  const agents = read(join(root, 'AGENTS.md'));
  for (const phrase of ROOT_PHRASES) check(agents.includes(phrase), `AGENTS.md: missing no-skill safeguard ${phrase}`);
  for (const name of EXPECTED_SKILLS) check(agents.includes(`\`${name}\``), `AGENTS.md: absent route for ${name}`);
  const searchable = [agents, ...paths.map(read)].join('\n');
  check(!searchable.includes('semantic-' + 'integrity-review'), 'retired standalone skill reference found');
}

function stringList(id, field, value) {
  check(Array.isArray(value) && value.every(item => typeof item === 'string'), `${id}: ${field} must be a string array`);
  check(value.length === new Set(value).size, `${id}: ${field} contains duplicates`);
  return value;
}

function validateCases(root) {
  const data = readJson(join(root, 'docs/agent-evals/cases/skill-routing.json'));
  check(sameKeys(data, ['version', 'authority_drift_policy', 'cases']), 'routing cases: invalid envelope fields');
  check(data.version === 2 && Array.isArray(data.cases), 'routing cases: invalid envelope');
  check(typeof data.authority_drift_policy === 'string' && data.authority_drift_policy.trim(), 'routing cases: authority_drift_policy is required');
  const cases = new Map();
  const coverage = new Map([...EXPECTED_SKILLS].map(name => [name, new Set()]));
  let compositions = 0;
  for (const entry of data.cases) {
    check(sameKeys(entry, CASE_KEYS), 'routing case has missing or unknown fields');
    const id = entry.id;
    check(fullMatch(NAME_PATTERN, id), 'invalid case ID');
    check(!cases.has(id), `duplicate case ID ${id}`);
    const trigger = entry.trigger_expectation;
    check(['positive', 'negative', 'composition'].includes(trigger), `${id}: invalid trigger expectation`);
    const target = stringList(id, 'skills_under_test', entry.skills_under_test);
    const expected = stringList(id, 'expected_skills', entry.expected_skills);
    const forbidden = stringList(id, 'forbidden_skills', entry.forbidden_skills);
    check(target.length > 0, `${id}: skills_under_test cannot be empty`);
    for (const [field, skills] of [['skills_under_test', target], ['expected_skills', expected], ['forbidden_skills', forbidden]]) {
      check(skills.every(skill => EXPECTED_SKILLS.has(skill)), `${id}: ${field} has unknown skills`);
    }
    check(!expected.some(skill => forbidden.includes(skill)), `${id}: expected and forbidden skills overlap`);
    if (trigger === 'positive') check(target.every(skill => expected.includes(skill)), `${id}: positive target must be expected`);
    else if (trigger === 'negative') check(target.every(skill => forbidden.includes(skill)), `${id}: negative target must be forbidden`);
    else {
      check(expected.length >= 2 && target.every(skill => expected.includes(skill)), `${id}: composition must expect every target skill`);
      compositions++;
    }
    check(['stable-synthetic', 'current-authority'].includes(entry.authority_mode), `${id}: invalid authority_mode`);
    check(typeof entry.prompt === 'string' && entry.prompt.trim(), `${id}: prompt is required`);
    check(entry.authority_mode !== 'stable-synthetic' ||
      !/AQG-V2-D\d+|\bD(?:03B|08|09|10)\b|5\/25\/125|provider finality/iu.test(entry.prompt),
    `${id}: mutable authority term requires current-authority mode`);
    check(Array.isArray(entry.expected_assertions) && entry.expected_assertions.length, `${id}: expected assertions are required`);
    const ids = new Set();
    for (const assertion of entry.expected_assertions) {
      check(sameKeys(assertion, ['id', 'text']), `${id}: invalid assertion fields`);
      check(fullMatch(NAME_PATTERN, assertion.id), `${id}: invalid assertion ID`);
      check(!ids.has(assertion.id), `${id}: duplicate assertion ID ${assertion.id}`);
      ids.add(assertion.id);
      check(typeof assertion.text === 'string' && assertion.text.trim(), `${id}: assertion text is required`);
    }
    check(entry.authority_mode !== 'current-authority' || ids.has('resolves-current-authority'), `${id}: current-authority case must assert authority resolution`);
    for (const skill of target) coverage.get(skill).add(trigger);
    cases.set(id, entry);
  }
  for (const [skill, triggers] of coverage) check(triggers.has('positive') && triggers.has('negative'), `${skill}: missing positive or negative boundary case`);
  check(compositions >= 3, 'at least three composition cases are required');
  return cases;
}

export function validateSupportedSchema(schema, path = '$') {
  check(object(schema), `result schema ${path}: must be an object`);
  check(Object.keys(schema).every(key => SCHEMA_KEYS.has(key)), `result schema ${path}: unsupported keywords`);
  const properties = Object.hasOwn(schema, 'properties') ? schema.properties : {};
  check(object(properties), `result schema ${path}: properties must be an object`);
  for (const [name, child] of Object.entries(properties)) validateSupportedSchema(child, `${path}.${name}`);
  if (Object.hasOwn(schema, 'items')) validateSupportedSchema(schema.items, `${path}[]`);
}

function matchesType(value, expected) {
  switch (expected) {
    case 'null': return value === null;
    case 'boolean': return typeof value === 'boolean';
    case 'integer': return Number.isInteger(value);
    case 'number': return typeof value === 'number' && Number.isFinite(value);
    case 'string': return typeof value === 'string';
    case 'array': return Array.isArray(value);
    case 'object': return object(value);
    default: throw new ValidationError(`result schema uses unsupported type ${expected}`);
  }
}

export function validateJsonSchema(value, schema, path = '$') {
  if (Object.hasOwn(schema, 'enum')) check(schema.enum.some(item => isDeepStrictEqual(item, value)), `${path}: value is not in the allowed enum`);
  if (Object.hasOwn(schema, 'type')) {
    const types = typeof schema.type === 'string' ? [schema.type] : schema.type;
    check(Array.isArray(types) && types.every(type => typeof type === 'string'), `result schema ${path}: invalid type declaration`);
    check(types.some(type => matchesType(value, type)), `${path}: expected type ${types}`);
  }
  if (object(value)) {
    check((schema.required ?? []).every(key => Object.hasOwn(value, key)), `${path}: missing required fields`);
    const properties = schema.properties ?? {};
    if (schema.additionalProperties === false) check(Object.keys(value).every(key => Object.hasOwn(properties, key)), `${path}: unknown fields`);
    for (const [key, child] of Object.entries(properties)) if (Object.hasOwn(value, key)) validateJsonSchema(value[key], child, `${path}.${key}`);
  }
  if (Array.isArray(value)) {
    check(value.length >= (schema.minItems ?? 0), `${path}: too few items`);
    if (schema.uniqueItems) check(new Set(value.map(canonicalJson)).size === value.length, `${path}: items must be unique`);
    if (Object.hasOwn(schema, 'items')) value.forEach((item, index) => validateJsonSchema(item, schema.items, `${path}[${index}]`));
  }
  if (typeof value === 'string') {
    check(length(value) >= (schema.minLength ?? 0), `${path}: string is too short`);
    if (schema.pattern !== undefined) check(fullMatch(schema.pattern, value), `${path}: string does not match pattern`);
  }
  if (typeof value === 'number' && schema.minimum !== undefined) check(value >= schema.minimum, `${path}: value is below minimum ${schema.minimum}`);
}

export function validateResultData(data, source, cases, schema) {
  validateJsonSchema(data, schema);
  const entry = cases.get(data.case_id);
  check(entry, `${source}: unknown case_id`);
  check(data.case_definition_sha256 === caseHash(entry), `${source}: case definition hash does not match selected case`);
  for (const field of ['trigger_expectation', 'skills_under_test', 'expected_skills']) check(isDeepStrictEqual(data[field], entry[field]), `${source}: ${field} does not match case`);
  check(data.skills_loaded.every(skill => EXPECTED_SKILLS.has(skill)), `${source}: unknown loaded skills`);
  check(fullMatch('[0-9a-f]{40}', data.commit), `${source}: commit must be a full lowercase SHA`);
  const identity = `git:${data.commit}`;
  check(data.worktree_dirty ? fullMatch(`${identity}\\+diff-sha256:[0-9a-f]{64}`, data.snapshot_identity) :
    data.snapshot_identity === identity, `${source}: commit/snapshot identity mismatch`);
  check(isDeepStrictEqual(data.assertions.map(item => item.id), entry.expected_assertions.map(item => item.id)), `${source}: assertion IDs/order do not match selected case`);
  if (data.outcome === 'PASS') {
    check(data.assertions.every(item => item.outcome === 'PASS'), `${source}: overall PASS requires every assertion to pass`);
    check(isDeepStrictEqual(data.skills_loaded, entry.expected_skills), `${source}: overall PASS requires exactly every expected skill to load`);
    check(!data.skills_loaded.some(skill => entry.forbidden_skills.includes(skill)), `${source}: forbidden skill loaded in passing result`);
  }
  if (data.outcome === 'NOT_RUN') check(data.skills_loaded.length === 0 && data.assertions.every(item => item.outcome === 'NOT_CHECKED'), `${source}: NOT_RUN requires no loaded skills and all assertions NOT_CHECKED`);
}

export function buildNotRunResult(entry) {
  const commit = '0'.repeat(40);
  return { case_id: entry.id, case_definition_sha256: caseHash(entry),
    trigger_expectation: entry.trigger_expectation, harness: 'codex', model: 'not-run', reasoning_level: 'not-run',
    commit, snapshot_identity: `git:${commit}`, worktree_dirty: false,
    skills_under_test: [...entry.skills_under_test], expected_skills: [...entry.expected_skills], skills_loaded: [],
    assertions: entry.expected_assertions.map(item => ({ id: item.id, outcome: 'NOT_CHECKED', evidence: '' })),
    outcome: 'NOT_RUN', notes: 'Deterministic validator self-test fixture.' };
}

function resultSelfTests(cases, schema) {
  const first = cases.values().next().value;
  const valid = buildNotRunResult(first);
  validateResultData(valid, 'self-test-valid', cases, schema);
  const invalid = (base, label, mutate) => {
    const data = structuredClone(base);
    mutate(data);
    try { validateResultData(data, `self-test-${label}`, cases, schema); }
    catch (error) { if (error instanceof ValidationError) return; throw error; }
    throw new ValidationError(`result validator self-test failed to reject ${label}`);
  };
  invalid(valid, 'unknown field', data => { data.unexpected = true; });
  invalid(valid, 'wrong field type', data => { data.worktree_dirty = 'false'; });
  invalid(valid, 'unknown case', data => { data.case_id = 'unknown-case'; });
  invalid(valid, 'unknown skill', data => { data.skills_loaded = ['unknown-skill']; });
  invalid(valid, 'classification mismatch', data => { data.trigger_expectation = first.trigger_expectation === 'negative' ? 'positive' : 'negative'; });
  invalid(valid, 'snapshot mismatch', data => { data.snapshot_identity = `git:${'1'.repeat(40)}`; });
  invalid(valid, 'assertion mismatch', data => { data.assertions[0].id = 'wrong-assertion'; });
  const passing = { ...structuredClone(valid), outcome: 'PASS', skills_loaded: [...first.expected_skills],
    assertions: first.expected_assertions.map(item => ({ id: item.id, outcome: 'PASS', evidence: 'self-test' })) };
  validateResultData(passing, 'self-test-pass', cases, schema);
  invalid(passing, 'failed assertion with PASS', data => { data.assertions[0].outcome = 'FAIL'; });
  const composition = [...cases.values()].find(entry => entry.trigger_expectation === 'composition');
  const missing = { ...buildNotRunResult(composition), outcome: 'PASS', skills_loaded: composition.expected_skills.slice(0, -1),
    assertions: composition.expected_assertions.map(item => ({ id: item.id, outcome: 'PASS', evidence: 'self-test' })) };
  invalid(missing, 'missing composition skill', () => {});
}

function markdownFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name);
    // Include file symlinks: reading them validates their targets; dangling links
    // fail visibly. Do not silently omit Markdown or recurse through directory links.
    return entry.name.endsWith('.md') ? [path] : entry.isDirectory() ? markdownFiles(path) : [];
  });
}

export function validateLinks(root, authorityPath) {
  const frontend = join(root, 'AqualLifeStyle/9.4.2/aqua-frontend/AGENTS.md');
  const files = new Set([join(root, 'AGENTS.md'), frontend, join(root, 'docs/aqua-system/README.md'), authorityPath,
    join(root, 'docs/agent-instruction-system.md'), ...markdownFiles(join(root, '.agents/skills')),
    ...markdownFiles(join(root, 'docs/exec-plans')), ...markdownFiles(join(root, 'docs/agent-evals'))]);
  for (const path of [...files].sort()) {
    for (const match of read(path).matchAll(/(?<!!)\[[^\]]+\]\(([^)]+)\)/gu)) {
      const target = match[1].trim().replace(/^<+|>+$/gu, '').split('#', 1)[0];
      if (!target || /^(?:https?:\/\/|mailto:)/u.test(target)) continue;
      check(existsSync(resolve(dirname(path), decodeURIComponent(target.split(' ', 1)[0]))), `${relative(root, path)}: broken local link ${match[1]}`);
    }
  }
  check(realpathSync(resolve(dirname(frontend), '../../../AGENTS.md')) === realpathSync(join(root, 'AGENTS.md')), 'frontend AGENTS.md bootstrap does not resolve to root');
}

export function validateRepository(root = ROOT, resultPaths = []) {
  validateSkills(root);
  const cases = validateCases(root);
  const schema = readJson(join(root, 'docs/agent-evals/result.schema.json'));
  validateSupportedSchema(schema);
  resultSelfTests(cases, schema);
  const authorityPath = join(root, 'docs/aqua-system/07-verification-decision-and-risk-register.md');
  validateLinks(root, authorityPath);
  const authority = read(authorityPath);
  for (const phrase of AUTHORITY_PHRASES) check(authority.includes(phrase), `authority convention missing ${phrase}`);
  check(read(join(root, 'docs/aqua-system/README.md')).includes('`UNRESOLVED` means no confirmed policy currently exists'), 'authority index does not map UNRESOLVED lifecycle state');
  for (const argument of resultPaths) {
    const path = resolve(argument);
    const data = readJson(path);
    check(object(data), `${path}: result root must be an object`);
    validateResultData(data, path, cases, schema);
  }
  return { skills: EXPECTED_SKILLS.size, cases: cases.size, resultFiles: resultPaths.length };
}

if (process.argv[1] && existsSync(process.argv[1]) && realpathSync(process.argv[1]) === MODULE_PATH) {
  try {
    const result = validateRepository(ROOT, process.argv.slice(2));
    const suffix = result.resultFiles ? `, ${result.resultFiles} result file(s)` : '';
    console.log(`PASS: ${result.skills} skills, ${result.cases} routing cases, result schema and coherence, authority contract, root safeguards, local links${suffix}`);
  } catch (error) {
    console.error(`FAIL: ${error.message}`);
    process.exitCode = 1;
  }
}
