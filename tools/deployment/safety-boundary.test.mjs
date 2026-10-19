import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { assessRevisions, classifyChanges, parseChanges } from './safety-boundary.mjs';

const ui = 'AqualLifeStyle/9.4.2/aqua-frontend/src/components/Example.tsx';
test('presentation-only changes classify without pretending independent enforcement', () => {
  assert.deepEqual(classifyChanges([{ status: 'M', path: ui, mode: '100644' }]),
    { decision: 'PRESENTATION_PATHS_ONLY', protectedPaths: [], independentEnforcement: 'UNVERIFIED' });
});

for (const path of ['.github/workflows/ci.yml', 'tools/deployment/safety-boundary.mjs',
  'tools/deployment/release-control.test.mjs', 'docker/api/Dockerfile', 'render.yaml',
  'AGENTS.md', 'docs/operations/cicd-safety-contract.md',
  'AqualLifeStyle/9.4.2/aspnet-core/src/Example.cs',
  'AqualLifeStyle/9.4.2/aqua-frontend/package-lock.json', ui.replace('.tsx', '.test.tsx'),
  'AqualLifeStyle/9.4.2/aqua-frontend/src/app/api/route.tsx']) {
  test(`control or unknown path requires separate change review: ${path}`, () => {
    for (const status of ['A', 'M', 'D', 'T'])
      assert.equal(classifyChanges([{ status, path }]).decision, 'CONTROL_REVIEW_REQUIRED');
  });
}
test('deletions and symlink type changes are never presentation-only', () => {
  for (const status of ['D', 'T']) assert.equal(classifyChanges([{ status, path: ui }]).decision, 'CONTROL_REVIEW_REQUIRED');
  for (const mode of [undefined, '120000', '160000'])
    assert.equal(classifyChanges([{ status: 'A', path: ui, mode }]).decision, 'CONTROL_REVIEW_REQUIRED');
});
test('missing, truncated, empty, unmerged and aliased changes fail closed', () => {
  for (const value of [undefined, '', 'M\0foo', 'M\0foo\0A\0']) assert.throws(() => parseChanges(value));
  for (const value of [undefined, [], [{ status: 'U', path: ui }],
    ...['/absolute', '../escape', 'a/../b', 'a//b', 'a\\b', 'a\nb', 'a:b'].map(path => [{ status: 'M', path }])])
    assert.throws(() => classifyChanges(value));
  for (const base of ['main', 'HEAD', '--help', 'a'.repeat(40)])
    assert.throws(() => assessRevisions(base, base));
});
test('immutable Git comparison catches renamed/deleted controls without executing candidate code', () => {
  const cwd = mkdtempSync(join(tmpdir(), 'aqua-boundary-'));
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  try {
    git('init'); git('config', 'user.name', 'Synthetic fixture'); git('config', 'user.email', 'fixture@example.invalid');
    git('config', 'commit.gpgsign', 'false');
    mkdirSync(join(cwd, 'tools/deployment'), { recursive: true });
    writeFileSync(join(cwd, 'tools/deployment/control.test.mjs'), 'throw Error("must never execute candidate");\n');
    git('add', '.'); git('commit', '-m', 'Synthetic base'); const base = git('rev-parse', 'HEAD');
    mkdirSync(join(cwd, ui.slice(0, ui.lastIndexOf('/'))), { recursive: true });
    git('mv', 'tools/deployment/control.test.mjs', ui); git('commit', '-m', 'Synthetic rename');
    const result = assessRevisions(base, git('rev-parse', 'HEAD'), cwd);
    assert.equal(result.decision, 'CONTROL_REVIEW_REQUIRED');
    assert(result.protectedPaths.includes('tools/deployment/control.test.mjs'));
    assert.throws(() => assessRevisions('f'.repeat(40), git('rev-parse', 'HEAD'), cwd), /REVISION_DIFF_UNVERIFIABLE/);
    const ordinaryBase = git('rev-parse', 'HEAD');
    writeFileSync(join(cwd, ui), 'export const Example = () => null;\n');
    git('add', '.'); git('commit', '-m', 'Synthetic regular presentation path');
    assert.equal(assessRevisions(ordinaryBase, git('rev-parse', 'HEAD'), cwd).decision, 'PRESENTATION_PATHS_ONLY');
    const linkBase = git('rev-parse', 'HEAD');
    symlinkSync('Example.tsx', join(cwd, ui.replace('Example.tsx', 'Link.tsx')));
    git('add', '.'); git('commit', '-m', 'Synthetic symlink addition');
    assert.equal(assessRevisions(linkBase, git('rev-parse', 'HEAD'), cwd).decision, 'CONTROL_REVIEW_REQUIRED');
  } finally { rmSync(cwd, { recursive: true, force: true }); }
});
