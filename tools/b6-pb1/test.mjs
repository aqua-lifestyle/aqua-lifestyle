import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync,execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const source=path.dirname(fileURLToPath(import.meta.url));
const repo=execFileSync('git',['-C',source,'rev-parse','--show-toplevel'],{encoding:'utf8'}).trim();
const output=process.argv[2];
if(process.argv.length!==3||!path.isAbsolute(output??''))throw new Error('Supply one new absolute external output directory');
const parent=fs.realpathSync(path.dirname(output));
if(path.join(parent,path.basename(output))!==output||output===repo||output.startsWith(repo+path.sep))throw new Error('Output must be canonical and outside repository');
fs.mkdirSync(output,{mode:0o700});
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const sourceNames=['record.h','record.c','pb1.h','pb1.c','fixture.c','tests/unit.c','tests/launch.c','tests/faults.c','tests/process.test.mjs','test.mjs'];
const sources=sourceNames.map(name=>({path:path.relative(repo,path.join(source,name)),sha256:hash(fs.readFileSync(path.join(source,name)))}));
const baseCommit=execFileSync('git',['-C',repo,'rev-parse','HEAD'],{encoding:'utf8'}).trim();
const sourceState=execFileSync('git',['-C',repo,'status','--porcelain=v1','--','tools/b6-pb1'],{encoding:'utf8'});
fs.writeFileSync(path.join(output,'source-evidence.json'),JSON.stringify({classification:'NEW_SOURCE_LOCAL_VALIDATION_NOT_RUNTIME_QUALIFICATION',baseCommit,sourceState,sources},null,2)+'\n',{mode:0o600});
const commands=[];
const cleanEnv={PATH:'/usr/bin:/bin',LC_ALL:'C',TZ:'UTC'};
function run(name,program,args,extraEnv={}) {
  const r=spawnSync(program,args,{cwd:repo,env:{...cleanEnv,...extraEnv},encoding:'utf8',timeout:120000,maxBuffer:16*1024*1024});
  fs.writeFileSync(path.join(output,name+'.stdout'),r.stdout??'',{mode:0o600});
  fs.writeFileSync(path.join(output,name+'.stderr'),r.stderr??'',{mode:0o600});
  commands.push({name,program,args,status:r.status,signal:r.signal,errorCode:r.error?.code??null});
  fs.writeFileSync(path.join(output,'commands.json'),JSON.stringify(commands,null,2)+'\n',{mode:0o600});
  if(r.error||r.signal||r.status!==0)throw new Error(`${name} failed; exact stdout/stderr retained in ${output}`);
  return r.stdout;
}
const cc='/usr/bin/cc';
run('compiler',cc,['--version']);run('openssl','/usr/bin/pkg-config',['--modversion','openssl']);
const flags=['-D_GNU_SOURCE','-std=c11','-O2','-g','-Wall','-Wextra','-Werror','-Wconversion','-Wshadow','-Wpedantic',`-ffile-prefix-map=${repo}=SOURCE`,`-fdebug-prefix-map=${repo}=SOURCE`,'-I',source];
const objectNames=['record','pb1'];
for(const name of objectNames)run('compile-'+name,cc,[...flags,'-c',path.join(source,name+'.c'),'-o',path.join(output,name+'.o')]);
const library=path.join(output,'libb6s-pb1.a');
run('archive','/usr/bin/ar',['rcsD',library,...objectNames.map(x=>path.join(output,x+'.o'))]);
const wrap=['-Wl,--wrap=malloc','-Wl,--wrap=calloc','-Wl,--wrap=write','-Wl,--wrap=EVP_Digest'];
run('build-unit-normal',cc,[...flags,path.join(source,'tests/unit.c'),library,...wrap,'-lcrypto','-o',path.join(output,'unit-normal')]);
const sanFlags=flags.filter(x=>x!=='-O2').concat(['-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie']);
run('build-unit-sanitizer',cc,[...sanFlags,...['record.c','pb1.c','tests/unit.c'].map(x=>path.join(source,x)),...wrap,'-lcrypto','-o',path.join(output,'unit-sanitizer')]);
for(const [name,macro,link]of [['fixture',null,null],['fixture-input-io','B6S_FAULT_INPUT_READ','-Wl,--wrap=read,--wrap=__read_chk'],['fixture-partial-write','B6S_FAULT_PARTIAL_WRITE','-Wl,--wrap=write']]){
  run('build-'+name,cc,[...flags,'-DB6S_TEST_FIXTURE',...(macro?['-D'+macro]:[]),path.join(source,'fixture.c'),
    ...(macro?[path.join(source,'tests/faults.c')]:[]),library,...(link?[link]:[]),'-lcrypto','-o',path.join(output,name)]);
}
run('build-launch',cc,[...flags,path.join(source,'tests/launch.c'),'-o',path.join(output,'launch')]);
const packageDirectory=path.join(repo,'docs/operations/aqgreen-v2-b6-successor/v1');
const unitResults=[];
for(const mode of ['normal','sanitizer']){
  const directory=path.join(output,'unit-'+mode+'-files');fs.mkdirSync(directory,{mode:0o700});
  const stdout=run('unit-'+mode,path.join(output,'unit-'+mode),[packageDirectory,directory],mode==='sanitizer'?{ASAN_OPTIONS:'detect_leaks=1:halt_on_error=1',UBSAN_OPTIONS:'halt_on_error=1:print_stacktrace=1'}:{});
  const match=stdout.match(/B6S_UNIT groups=(\d+) assertions=(\d+) scope=LOCAL_COMPONENT_NOT_RUNTIME_QUALIFICATION/);
  if(!match||+match[1]!==8||+match[2]<1000)throw new Error('Missing complete focused unit result');
  if(fs.statSync(path.join(output,'unit-'+mode+'.stderr')).size)throw new Error('Unit runtime diagnostics present');
  unitResults.push({mode,groups:+match[1],assertions:+match[2]});
}
if(fs.readFileSync(path.join(output,'unit-normal.stdout'),'utf8')!==fs.readFileSync(path.join(output,'unit-sanitizer.stdout'),'utf8'))throw new Error('Unit logical outputs differ');
const tap=run('process-tests',process.execPath,['--test','--test-concurrency=1','--test-reporter=tap',path.join(source,'tests/process.test.mjs')],{B6S_TEST_OUTPUT:output});
const counter=name=>{const m=tap.match(new RegExp('^# '+name+' (\\d+)$','m'));if(!m)throw new Error('Missing TAP counter '+name);return +m[1];};
const processResults={tests:counter('tests'),passed:counter('pass'),failed:counter('fail'),skipped:counter('skipped'),cancelled:counter('cancelled')};
if(processResults.tests!==37||processResults.passed!==37||processResults.failed||processResults.skipped||processResults.cancelled)throw new Error('Incomplete process fixture results');
const artifacts=[];
function inventory(directory,relative=''){
  for(const entry of fs.readdirSync(directory,{withFileTypes:true}).sort((a,b)=>a.name.localeCompare(b.name,'en'))){
    const name=path.join(relative,entry.name);const full=path.join(directory,entry.name);
    if(entry.isDirectory())inventory(full,name);
    else if(entry.isFile()&&!['summary.json','ARTIFACTS.sha256'].includes(name))artifacts.push({path:name,bytes:fs.statSync(full).size,sha256:hash(fs.readFileSync(full))});
    else throw new Error('Unexpected artifact type');
  }
}
inventory(output);
artifacts.sort((a,b)=>a.path<b.path?-1:a.path>b.path?1:0);
const manifest=artifacts.map(a=>`${a.sha256}  ${a.path}\n`).join('');
fs.writeFileSync(path.join(output,'ARTIFACTS.sha256'),manifest,{mode:0o600});
const summary={classification:'LOCAL_NATIVE_COMPONENT_AND_ENTRYPOINT_FIXTURE_NOT_RUNTIME_QUALIFICATION',sources,
  adoptedManifestSha256:hash(fs.readFileSync(path.join(packageDirectory,'MANIFEST.sha256'))),
  units:unitResults,processes:processResults,artifactManifestSha256:hash(manifest),
  executables:artifacts.filter(x=>['unit-normal','unit-sanitizer','fixture','fixture-input-io','fixture-partial-write','launch','libb6s-pb1.a'].includes(x.path)),
  runtimeAdmission:'BLOCKED_NO_SUCCESS_PATH',
  missing:['External authority/controller and typed-manifest/source/supply qualification','Qualified loader/namespace/confinement and custody','Independent observation/campaign/recovery-intent ledger','Full B6/TQ1/BIND/representative/financial acceptance'],
  scopeNotes:['8unit groups contain systematic mutation loops; assertion counts are not distinct attack classes.','Normal and sanitizer execute the same component identities, not twice as many unique tests.','Process fixture is normal-only; ASan address-space requirements are not the128MiB native profile.','No accepted Customer11 or unchanged foundation/.NET/PG suite executed.'],
  compilerExecutableSha256:hash(fs.readFileSync(fs.realpathSync(cc))),node:process.version};
fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(summary,null,2)+'\n',{mode:0o600});
console.log(JSON.stringify(summary,null,2));
