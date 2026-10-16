// Assessment tool, NOT an independent enforcement boundary. Run from a protected
// checkout outside the candidate, with externally verified immutable revisions.
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const sha = /^[a-f0-9]{40}$/;
const frontend = 'AqualLifeStyle/9.4.2/aqua-frontend/';

export function parseChanges(output) {
  if (typeof output !== 'string' || !output.endsWith('\0')) throw Error('DIFF_INCOMPLETE');
  const fields = output.slice(0, -1).split('\0');
  if (fields.length % 2 || fields.length > 40_000) throw Error('DIFF_INCOMPLETE');
  return Array.from({ length: fields.length / 2 }, (_, i) => ({ status: fields[2 * i], path: fields[2 * i + 1] }));
}

export function classifyChanges(changes) {
  if (!Array.isArray(changes) || !changes.length || changes.length > 20_000) throw Error('DIFF_UNVERIFIABLE');
  const protectedPaths = [];
  for (const change of changes) {
    const path = change?.path;
    if (!['A', 'M', 'D', 'T'].includes(change?.status) || typeof path !== 'string' ||
        /[\x00-\x1f\x7f\\:]/.test(path) || path.split('/').some(p => !p || p === '.' || p === '..'))
      throw Error('CHANGE_PATH_UNVERIFIABLE');
    // Deliberately narrow presentation allowlist. Unknown paths require control
    // review; tests, config, dependencies, backend and operational docs do too.
    const presentation = path.startsWith(frontend) &&
      /^(?:src\/(?:components|app|styles)\/.*\.(?:tsx|css)|public\/.*\.(?:png|jpg|jpeg|webp|svg))$/.test(path.slice(frontend.length)) &&
      !/(?:^|\/)(?:api|__tests__|tests)(?:\/|$)|\.(?:test|spec)\./.test(path);
    // Type changes can introduce symlinks; deletion also needs explicit review.
    if (!presentation || !['A', 'M'].includes(change.status) ||
        !['100644', '100755'].includes(change.mode)) protectedPaths.push(path);
  }
  return Object.freeze({ decision: protectedPaths.length ? 'CONTROL_REVIEW_REQUIRED' : 'PRESENTATION_PATHS_ONLY',
    protectedPaths: Object.freeze([...new Set(protectedPaths)].sort()), independentEnforcement: 'UNVERIFIED' });
}

export function assessRevisions(base, head, cwd = process.cwd()) {
  if (!sha.test(base ?? '') || !sha.test(head ?? '') || base === head) throw Error('IMMUTABLE_REVISIONS_REQUIRED');
  const git = args => execFileSync('git', ['-c', 'core.fsmonitor=false', ...args],
    { cwd, encoding: 'utf8', maxBuffer: 4 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
  try {
    git(['merge-base', '--is-ancestor', base, head]);
    // No renames means both source deletion and destination addition are checked.
    // Never execute candidate hooks, diff drivers, textconv or candidate JS.
    const changes = parseChanges(git(['diff', '--no-ext-diff', '--no-textconv', '--no-renames',
      '--name-status', '-z', base, head, '--']));
    const tree = git(['ls-tree', '-r', '-z', '--full-tree', head]);
    if (!tree.endsWith('\0')) throw Error('DIFF_TREE_INCOMPLETE');
    const modes = new Map(tree.slice(0, -1).split('\0').map(row => {
      const tab = row.indexOf('\t');
      if (tab < 0) throw Error('DIFF_TREE_INCOMPLETE');
      return [row.slice(tab + 1), row.slice(0, 6)];
    }));
    return classifyChanges(changes.map(change => ({ ...change, mode: modes.get(change.path) })));
  } catch (error) {
    if (/^(?:DIFF_|CHANGE_)/.test(error.message)) throw error;
    throw Error('REVISION_DIFF_UNVERIFIABLE');
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    if (process.argv.length !== 4) throw Error('IMMUTABLE_REVISIONS_REQUIRED');
    const result = assessRevisions(process.argv[2], process.argv[3]);
    console.log(JSON.stringify(result));
    process.exitCode = result.decision === 'PRESENTATION_PATHS_ONLY' ? 0 : 2;
  } catch (error) {
    console.error(JSON.stringify({ decision: 'BLOCKED', reason: error.message }));
    process.exitCode = 1;
  }
}
