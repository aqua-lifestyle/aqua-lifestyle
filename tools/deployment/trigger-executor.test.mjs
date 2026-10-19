import assert from 'node:assert/strict';
import {test} from 'node:test';
import {mkdtempSync,writeFileSync,rmSync} from 'node:fs';
import {join} from 'node:path';
import {tmpdir} from 'node:os';
import {trigger} from './trigger-executor.mjs';
import {requiredJobs,targets} from './release-control.mjs';

function fixture(){
  const dir=mkdtempSync(join(tmpdir(),'aqua-dispatch-'));
  const path=join(dir,'event.json'),sha='c'.repeat(40);
  const run={id:123,run_attempt:1,repository:{full_name:targets.repository},head_repository:{full_name:targets.repository},
    event:'push',head_branch:'main',head_sha:sha,path:'.github/workflows/ci.yml',name:'Continuous integration',status:'completed',conclusion:'success'};
  const event={action:'completed',workflow_run:run};
  const env={GITHUB_REPOSITORY:targets.repository,GITHUB_EVENT_NAME:'workflow_run',GITHUB_REF:'refs/heads/main',
    GITHUB_EVENT_PATH:path,EXECUTOR_TRIGGER_TOKEN:'trigger-only-canary'};
  const api={gh:async route=>route.includes('/jobs?')?{total_count:17,jobs:requiredJobs.map(name=>({name,status:'completed',conclusion:'success',run_id:123,run_attempt:1}))}:
    route==='/git/ref/heads/main'?{object:{sha}}:run};
  const posts=[];const fetcher=async(url,options)=>{posts.push({url,options});return {status:204};};
  return {event,env,run,api,posts,fetcher,execute:async()=>{writeFileSync(path,JSON.stringify(event));await trigger(env,fetcher,api);},close:()=>rmSync(dir,{recursive:true,force:true})};
}
test('successful main CI automatically dispatches only fixed executor and exact SHA/run/attempt hints',async()=>{
  const f=fixture();try{await f.execute();assert.equal(f.posts.length,1);
    assert.equal(f.posts[0].url,'https://api.github.com/repos/aqua-lifestyle/aqua-deployment/actions/workflows/deploy.yml/dispatches');
    assert.deepEqual(JSON.parse(f.posts[0].options.body),{ref:'main',inputs:{ci_run_id:'123',ci_run_attempt:'1',sha:f.run.head_sha}});
    assert.equal(f.posts[0].options.redirect,'error');assert.doesNotMatch(f.posts[0].options.body,/credential|token|script|target|artifact/i);
  }finally{f.close();}
});
for(const [name,change]of[
  ['failed CI',f=>{f.run.conclusion='failure';}],['PR CI',f=>{f.run.event='pull_request';}],
  ['fork CI',f=>{f.run.head_repository.full_name='untrusted/fork';}],['wrong branch',f=>{f.run.head_branch='develop';}],
  ['missing trigger identity',f=>{delete f.env.EXECUTOR_TRIGGER_TOKEN;}],['wrong CI attempt',f=>{f.api.gh=async route=>route.includes('/jobs?')?{total_count:0,jobs:[]}:f.run;}]
])test(`${name} cannot dispatch the executor`,async()=>{const f=fixture();try{change(f);await assert.rejects(f.execute());assert.equal(f.posts.length,0);}finally{f.close();}});
test('an ambiguous automatic dispatch is never blindly retried',async()=>{
  const f=fixture();let calls=0;try{f.fetcher=async()=>{calls++;throw Error('secret-response');};writeFileSync(f.env.GITHUB_EVENT_PATH,JSON.stringify(f.event));
    await assert.rejects(trigger(f.env,f.fetcher,f.api),/AMBIGUOUS_INSPECT/);assert.equal(calls,1);
  }finally{f.close();}
});
