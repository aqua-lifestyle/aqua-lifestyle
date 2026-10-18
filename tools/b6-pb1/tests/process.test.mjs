import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';

const root = process.env.B6S_TEST_OUTPUT;
assert.ok(root && path.isAbsolute(root), 'owned external output required');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const binaries = process.env.B6S_TEST_BINARIES ?? root;
const fixture = path.join(binaries, 'fixture');
const launcher = path.join(binaries, 'launch');
const packageHash = '0711c7d483f16bddf8bb83ecacb7b4dedda978adc85295c4704516514e81fd7b';
const specHash = 'e9fe2bc58b1c38d0e4f1dd39d26d81f1243970072b21f016c2ca966022ed215a';
const input = Buffer.from('schema\tB6S_PB1_RESULT_V1\nversion\tB6S1\n' +
  `descriptor_sha256\t${'a'.repeat(64)}\ninput_sha256\t${'b'.repeat(64)}\n` +
  'input_bytes\t0\nschema_id\tB6S_TEST_PAYLOAD\noutcome\tREPRESENTATION_CHECKED\nreason\tNONE\n');
const argv = ['b6-pb1-v1', '--request-fd', '3', '--input-fd', '4', '--result-fd', '5'];
let sequence = 0;
const observations = [];

function descriptor(binary, payload, changes = {}) {
  const fields = {
    schema: 'B6S_PB1_INVOCATION_V1', version: 'B6S1', realm: 'ISOLATED_QUALIFICATION',
    successor_manifest_sha256: packageHash, invocation_spec_sha256: specHash,
    qualification_plan_sha256: hash('TEST_ONLY_UNQUALIFIED_PLAN'),
    release_manifest_sha256: hash('TEST_ONLY_UNQUALIFIED_RELEASE'),
    entrypoint_source_sha256: hash('TEST_ONLY_UNQUALIFIED_SOURCE'),
    executable_sha256: hash(fs.readFileSync(binary)), parser_manifest_sha256: hash('TEST_ONLY_PARSER'),
    grammar_manifest_sha256: hash('TEST_ONLY_GRAMMAR'), schema_id: 'B6S_PB1_RESULT_V1',
    schema_manifest_sha256: hash('TEST_ONLY_SCHEMA'), layout: 'CANONICAL_TEXT',
    configuration_sha256: hash('TEST_ONLY_CONFIGURATION'), platform_manifest_sha256: hash('TEST_ONLY_PLATFORM'),
    selected_supply_sha256: hash('TEST_ONLY_SUPPLY'), input_sha256: hash(payload),
    input_bytes: String(payload.length), invocation_id: '11111111-1111-4111-8111-111111111111',
    ...changes,
  };
  return Buffer.from(Object.entries(fields).map(([k,v]) => `${k}\t${v}\n`).join(''));
}
function run(name, options = {}) {
  const directory = path.join(root, 'process', `${String(++sequence).padStart(3,'0')}-${name}`);
  fs.mkdirSync(directory, {mode: 0o700, recursive: true});
  const binary = options.binary ?? fixture;
  const payload = options.input ?? input;
  const request = options.request ?? descriptor(binary, payload, options.changes);
  const requestPath = path.join(directory,'request'); const inputPath = path.join(directory,'input');
  const resultPath = options.aliasOutput ? inputPath : path.join(directory,'result');
  const diagnosticPath = path.join(directory,'diagnostic');
  fs.writeFileSync(requestPath, request, {mode:0o600,flag:'wx'});
  fs.writeFileSync(inputPath, payload, {mode:0o600,flag:'wx'});
  if (!options.aliasOutput) fs.writeFileSync(resultPath, options.oldOutput ?? '', {mode:0o600,flag:'wx'});
  fs.writeFileSync(diagnosticPath, '', {mode:0o600,flag:'wx'});
  if (options.publicDiagnostics) fs.chmodSync(diagnosticPath,0o640);
  const opened = [];
  const open = (p,flags) => {const fd=fs.openSync(p,flags);opened.push(fd);return fd;};
  let status;
  try {
    const diagnosticTarget = options.aliasDiagnosticInput ? inputPath : options.aliasDiagnosticRequest ? requestPath :
      options.aliasDiagnosticResult ? resultPath : diagnosticPath;
    const stdio = [open('/dev/null','r'),open(options.nonInertOutput ? diagnosticPath : '/dev/null','w'),
      open(diagnosticTarget,fs.constants.O_WRONLY),open(requestPath,options.writeRequest ? 'r+' : 'r'),
      open(inputPath,options.writeInput ? 'r+' : 'r'),
      open(resultPath,options.readResult ? 'r' : fs.constants.O_WRONLY),open(binary,'r')];
    if (options.extraFd) stdio.push(open(inputPath,'r'));
    const child=spawnSync(launcher,options.argv ?? argv,{cwd:directory,
      env:options.env ?? {LC_ALL:'C',TZ:'UTC'},stdio,timeout:15000});
    status={status:child.status,signal:child.signal,errorCode:child.error?.code??null};
  } finally { for (const fd of opened) fs.closeSync(fd); }
  const result=fs.readFileSync(resultPath); const diagnostic=fs.readFileSync(diagnosticPath,'utf8');
  const observation={name,attemptId:crypto.randomUUID(),classification:'TEST_ONLY_UNQUALIFIED_ENTRYPOINT_FIXTURE',
    requestSha256:hash(request),inputSha256:hash(payload),executableSha256:hash(fs.readFileSync(binary)),
    requestAfterSha256:hash(fs.readFileSync(requestPath)),inputAfterSha256:hash(fs.readFileSync(inputPath)),
    diagnosticChannelEstablished:!(options.aliasDiagnosticInput||options.aliasDiagnosticRequest||options.aliasDiagnosticResult),
    ...status,resultBytes:result.length,resultSha256:hash(result),diagnosticBytes:Buffer.byteLength(diagnostic),
    runtimeAdmission:'BLOCKED_CONTROLLER_CUSTODY_CONFINEMENT_TYPED_MANIFESTS_AND_INDEPENDENT_LEDGER',
    directory:path.basename(directory)};
  fs.writeFileSync(path.join(directory,'observation.json'),JSON.stringify(observation,null,2)+'\n',{mode:0o600});
  observations.push(observation);
  assert.equal(status.errorCode,null); assert.equal(status.signal,null);
  assert.ok(Buffer.byteLength(diagnostic)<=4096);
  assert.deepEqual(fs.readFileSync(requestPath),request,'descriptor unchanged');
  assert.deepEqual(fs.readFileSync(inputPath),payload,'input unchanged');
  return {result,diagnostic,...status,request};
}
function record(bytes) {
  assert.ok(bytes.length && bytes.at(-1)===10);
  return Object.fromEntries(bytes.toString().trimEnd().split('\n').map(line=>line.split('\t')));
}
function rejected(name, options, exit) {
  test(name,()=>{const r=run(name,options);assert.equal(r.status,exit);
    assert.deepEqual(r.result,options.aliasOutput ? (options.input??input) : Buffer.from(options.oldOutput??''));});
}

test('locally valid request remains blocked, with exact complete negative result',()=>{
  const r=run('valid-blocked'); assert.equal(r.status,12);
  assert.deepEqual(r.result,Buffer.from('schema\tB6S_PB1_RESULT_V1\nversion\tB6S1\n'+
    `descriptor_sha256\t${hash(r.request)}\ninput_sha256\t${hash(input)}\ninput_bytes\t${input.length}\n`+
    'schema_id\tB6S_PB1_RESULT_V1\noutcome\tUNDETERMINED\nreason\tDEPENDENCY\n'));
  assert.match(r.diagnostic,/FIXTURE_ONLY/);
});
test('replay preserves separate attempts and deterministic complete local negative bytes',()=>{
  const a=run('replay-a');const b=run('replay-b');assert.deepEqual(a.request,b.request);assert.deepEqual(a.result,b.result);
  assert.equal(a.status,12);assert.equal(b.status,12);
});
rejected('wrong-argv0',{argv:['wrong',...argv.slice(1)]},10);
rejected('missing-argv',{argv:argv.slice(0,6)},10);
rejected('extra-argv',{argv:[...argv,'--extra']},10);
rejected('combined-argument',{argv:[argv[0],'--request-fd=3',...argv.slice(3)]},10);
rejected('extra-environment',{env:{LC_ALL:'C',TZ:'UTC',PATH:'/not-selected'}},10);
rejected('reordered-environment',{env:{TZ:'UTC',LC_ALL:'C'}},10);
rejected('wrong-environment',{env:{LC_ALL:'C.UTF-8',TZ:'UTC'}},10);
rejected('request-not-readonly',{writeRequest:true},10);
rejected('input-not-readonly',{writeInput:true},10);
rejected('result-not-writeonly',{readResult:true},10);
rejected('result-not-empty',{oldOutput:'RETAINED_PRIOR_OUTPUT'},10);
rejected('extra-inherited-fd',{extraFd:true},10);
rejected('aliased-output-input',{aliasOutput:true},10);
rejected('public-diagnostics',{publicDiagnostics:true},10);
rejected('unsafe-diagnostic-input-alias',{aliasDiagnosticInput:true},10);
rejected('unsafe-diagnostic-empty-input-alias',{aliasDiagnosticInput:true,input:Buffer.alloc(0)},10);
rejected('unsafe-diagnostic-input-alias-before-argv',{aliasDiagnosticInput:true,argv:['wrong',...argv.slice(1)]},10);
rejected('unsafe-diagnostic-request-alias',{aliasDiagnosticRequest:true},10);
rejected('unsafe-diagnostic-result-alias',{aliasDiagnosticResult:true},10);
rejected('unsupported-inert-endpoint-subprofile',{nonInertOutput:true},12);
rejected('descriptor-truncated',{request:descriptor(fixture,input).subarray(0,100)},10);
rejected('descriptor-unknown-version',{changes:{version:'B6S0'}},11);
rejected('descriptor-operational-realm',{changes:{realm:'OPERATIONAL_READONLY'}},11);
for(const [name,changes,exit,reason]of [
  ['wrong-package',{successor_manifest_sha256:'a'.repeat(64)},10,'CONTEXT'],
  ['wrong-spec',{invocation_spec_sha256:'a'.repeat(64)},10,'CONTEXT'],
  ['wrong-executable',{executable_sha256:'a'.repeat(64)},10,'CONTEXT'],
  ['wrong-input-digest',{input_sha256:'a'.repeat(64)},10,'INPUT_DIGEST'],
  ['wrong-input-count',{input_bytes:'1'},10,'INPUT_DIGEST'],
  ['unsupported-schema',{schema_id:'NOT_COMPILED'},11,'SCHEMA'],
  ['unsupported-native-layout',{layout:'a'.repeat(64)},11,'SCHEMA'],
])test(name,()=>{const r=run(name,{changes});assert.equal(r.status,exit);assert.equal(record(r.result).reason,reason);});
test('fully observed malformed input is invalid grammar',()=>{
  const r=run('bad-input',{input:Buffer.from('not a canonical record\n')});assert.equal(r.status,10);assert.equal(record(r.result).reason,'GRAMMAR');
});
test('observed empty input is zero bytes, never missing-evidence success',()=>{
  const r=run('empty-input',{input:Buffer.alloc(0)});assert.equal(r.status,10);
  assert.equal(record(r.result).input_bytes,'0');assert.equal(record(r.result).reason,'GRAMMAR');
});
rejected('input-over-bound',{input:Buffer.alloc(1048577,65),changes:{input_bytes:'1048576'}},10);
test('injected input IO failure emits no fabricated complete result',()=>{
  const r=run('input-io',{binary:path.join(binaries,'fixture-input-io')});assert.equal(r.status,12);assert.equal(r.result.length,0);assert.match(r.diagnostic,/IO/);
});
test('injected partial output remains retained non-success',()=>{
  const r=run('partial-write',{binary:path.join(binaries,'fixture-partial-write')});assert.equal(r.status,12);assert.equal(r.result.length,17);assert.match(r.diagnostic,/IO/);
  assert.throws(()=>record(r.result));
});
test.after(()=>{
  assert.ok(observations.every(r=>r.status!==0),'fixture never grants full admission');
  fs.writeFileSync(path.join(root,'process-observations.json'),JSON.stringify(observations,null,2)+'\n',{mode:0o600});
});
