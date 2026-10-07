/* Reuse unchanged closed fixture constructors only; do NOT execute its suite.
   None of the historical/live dispositions below are authenticated real evidence.
   They are explicit modeled producer inputs for authorized nonprivileged tests. */
#define main b6_prior_transition_fixture_main_not_executed
#include "b6-transition-fixture.c"
#undef main
#include "attempt-account-replay.h"
#include <openssl/sha.h>
#include <inttypes.h>

enum { N=32, BYTES=N*B6_SYNTAX_EVENT_FRAME_MAX };
struct scenario {
 unsigned char bytes[BYTES];
 size_t ends[N], count, length, reservations;
 struct b6_replay_original_evidence proof[N];
 struct b6_syntax_account_store_bytes store;
 struct b6_replay_input input;
 struct b6_replayed_record scratch[N], saved_records[N];
 struct b6_replayed_commission commissions[N], saved_commissions[N];
};
static struct scenario *s;
static void hash_bytes(const unsigned char *p,size_t n,struct b6_syntax_hex *out)
{ unsigned char raw[SHA256_DIGEST_LENGTH];static const char digits[]="0123456789abcdef";
 CHECK(SHA256(p,n,raw)!=NULL);
 for(size_t i=0;i<sizeof raw;++i){out->text[i*2]=digits[raw[i]>>4];out->text[i*2+1]=digits[raw[i]&15u];}out->text[64]=0;
}
static void start(const char *property)
{ begin(0,0,property);memset(s,0,sizeof *s);
 struct b6_syntax_account_store store;memset(&store,0,sizeof store);u(&store.store_id);store.format_version=B6_SYNTAX_FORMAT_V1;
 CHECK(b6_syntax_serialize_account_store(&store,&s->store)==B6_SYNTAX_OK);
 s->input.journal=s->bytes;s->input.store_identity=s->store.bytes;s->input.store_identity_length=s->store.length;
 s->input.validated_store_identity=store;s->input.protected_journal_store_custody=B6_EXTERNAL_VALIDATED;
 s->input.historical_identity_continuity=B6_EXTERNAL_VALIDATED;s->input.required_custody_history=B6_EXTERNAL_VALIDATED;
 s->input.scope=B6_REPLAY_COMPLETE_CURRENT_CANDIDATE;
 s->input.complete_current_identity_continuity=B6_EXTERNAL_VALIDATED;
 s->input.original_provisioning=context(0);strcpy(s->input.original_provisioning.commission_id.text,"STORE");
 s->input.records=s->proof;
}
static void identity(unsigned commission,struct b6_syntax_event_frame *f,struct b6_account_evidence *e)
{ char id[65],path[128];int n=snprintf(id,sizeof id,"commission_%u",commission);CHECK(n>0 && (size_t)n<sizeof id);
 strcpy(f->common.commission_id.text,id);strcpy(e->enrollment_scope.commission_id.text,id);
 strcpy(e->current_permit.qualification_id.text,id);
 n=snprintf(path,sizeof path,"/tmp/modeled-b6-output-%u",commission);CHECK(n>0 && (size_t)n<sizeof path);
 strcpy(e->enrollment_scope.output_path.text,path);strcpy(e->current_permit.output_path.text,path);
 e->enrollment_resource_key.text[0]=commission==1?'1':'2';
 if(f->event_type==B6_SYNTAX_REGISTER_COMMISSION)f->data.registration.resource_key=e->enrollment_resource_key;
}
static void append_event(size_t index,size_t source,unsigned commission,uint64_t attempt)
{ CHECK(s->count<N);struct b6_account_context c=context(source);
 struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
 if(index)identity(commission,&f,&e);
 f.common.event_seq=(uint64_t)s->count+1u;f.common.attempt_number=attempt;
 if(s->count){f.common.previous_event.kind=B6_SYNTAX_HEAD_DIGEST;f.common.previous_event.digest=s->proof[s->count-1].original_transition.event_payload_sha256;}
 if(index==2){++s->reservations;e.reserved_assignment.execution_uid=2000u+(uint32_t)s->reservations;
  e.reserved_assignment.execution_gid=e.reserved_assignment.execution_uid;
  e.reserved_assignment.allocation_ordinal=(uint64_t)s->reservations;
  int n=snprintf(e.reserved_assignment.generation.text,sizeof e.reserved_assignment.generation.text,
   "10000000-0000-0000-0000-%012zu",s->reservations);CHECK(n==36);
 }
 bind_proof(&e,&f);e.dispositions.complete_current_history=B6_EXTERNAL_UNVALIDATED;
 struct b6_replay_original_evidence *p=&s->proof[s->count];p->original_transition=e;
 hash_bytes(s->bytes,s->length,&p->preceding_prefix_sha256);
 p->original_preceding_projection=p->original_writer_provenance=p->retained_original_sources=p->original_durable_commit=B6_EXTERNAL_VALIDATED;
 struct b6_syntax_event_bytes b;CHECK(b6_syntax_serialize_event_frame(&f,&b)==B6_SYNTAX_OK);
 CHECK(b.length<=BYTES-s->length);memcpy(s->bytes+s->length,b.bytes,b.length);s->length+=b.length;
 s->ends[s->count]=s->length;++s->count;
 s->input.journal_length=s->length;s->input.record_count=s->count;
 hash_bytes(s->bytes,s->length,&s->input.validated_journal_sha256);
}
static struct b6_account_replay_result replay(enum b6_replay_disposition expected)
{ struct b6_replay_workspace work={s->scratch,N,s->commissions,N};
 struct b6_replay_input old=s->input;
 unsigned char *immutable=malloc(s->input.journal_length?s->input.journal_length:1);CHECK(immutable!=NULL);
 if(s->input.journal_length)memcpy(immutable,s->input.journal,s->input.journal_length);
 struct b6_replay_original_evidence *proof=malloc(sizeof s->proof);CHECK(proof!=NULL);memcpy(proof,s->proof,sizeof s->proof);
 struct b6_account_replay_result r=b6_account_replay(&s->input,work);CHECK(r.disposition==expected);
 CHECK(memcmp(&old,&s->input,sizeof old)==0);CHECK(memcmp(proof,s->proof,sizeof s->proof)==0);
 if(s->input.journal_length)CHECK(memcmp(immutable,s->input.journal,s->input.journal_length)==0);
 free(immutable);free(proof);
 memcpy(s->saved_records,s->scratch,sizeof s->scratch);memcpy(s->saved_commissions,s->commissions,sizeof s->commissions);
 struct b6_account_replay_result twice=b6_account_replay(&s->input,work);
 CHECK(memcmp(&r,&twice,sizeof r)==0);CHECK(memcmp(s->saved_records,s->scratch,sizeof s->scratch)==0);
 CHECK(memcmp(s->saved_commissions,s->commissions,sizeof s->commissions)==0);
 if(expected==B6_REPLAY_INVALID_HISTORY || expected==B6_REPLAY_UNDETERMINED){
  CHECK(r.historical_context_present==B6_ACCOUNT_FACT_UNKNOWN);CHECK(r.current_context_present==B6_ACCOUNT_FACT_UNKNOWN);
  CHECK(!r.records && !r.commissions);CHECK(!r.record_count && !r.commission_count && !r.last_event_seq);
  CHECK(zero(&r.store_context,sizeof r.store_context));CHECK(zero(s->scratch,sizeof s->scratch));CHECK(zero(s->commissions,sizeof s->commissions));
  CHECK(zero(&r.journal_sha256,sizeof r.journal_sha256));CHECK(zero(&r.last_payload_sha256,sizeof r.last_payload_sha256));
 }else{
  CHECK(r.historical_context_present==B6_ACCOUNT_FACT_YES);CHECK(r.record_count==s->input.record_count);
  CHECK(r.last_event_seq==(uint64_t)s->input.record_count);
  CHECK(r.current_context_present==(expected==B6_REPLAY_COMPLETE_CURRENT_STATE?B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO));
  CHECK(memcmp(&r.journal_sha256,&s->input.validated_journal_sha256,sizeof r.journal_sha256)==0);
 }
 return r;
}
static void base(void){append_event(0,0,0,0);append_event(1,2,1,0);}
static void admission(void)
{base();append_event(2,3,1,1);append_event(3,4,1,1);append_event(4,5,1,1);append_event(5,6,1,1);}
static void complete_accepted(void){admission();append_event(6,7,1,1);append_event(8,11,1,1);}
static void final_digest(void){hash_bytes(s->bytes,s->input.journal_length,&s->input.validated_journal_sha256);}
static void repair_local_hash(size_t record)
{size_t off=record?s->ends[record-1]:0,tab=off;while(s->bytes[tab]!='\t')++tab;
 uint64_t length=0;CHECK(b6_syntax_u64plus(s->bytes+off,tab-off,&length)==B6_SYNTAX_OK);
 struct b6_syntax_hex hsh;hash_bytes(s->bytes+tab+66,(size_t)length,&hsh);
 memcpy(s->bytes+tab+1,hsh.text,64);
}
static void rewrite(size_t record,struct b6_syntax_event_frame f)
{size_t offset=record?s->ends[record-1]:0;
 struct b6_syntax_event_bytes b;CHECK(b6_syntax_serialize_event_frame(&f,&b)==B6_SYNTAX_OK);
 CHECK(b.length<=BYTES-offset);memcpy(s->bytes+offset,b.bytes,b.length);
 s->length=offset+b.length;s->ends[record]=s->length;s->count=record+1;
 s->input.journal_length=s->length;s->input.record_count=s->count;
 bind_proof(&s->proof[record].original_transition,&f);final_digest();}
static void positive(void)
{
 start("complete-genesis-only");append_event(0,0,0,0);struct b6_account_replay_result r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);CHECK(r.commission_count==0);
 start("registered-unused-close");base();append_event(9,3,1,0);r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);CHECK(r.commissions[0].context.commission==B6_COMMISSION_CLOSED_UNUSED);
 start("all-admission-reconciliation-acceptance-events");complete_accepted();r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
 CHECK(r.commissions[0].context.commission==B6_COMMISSION_FULFILLED);CHECK(r.commissions[0].context.phase==B6_ATTEMPT_QUALIFICATION_ACCEPTED);
 CHECK(r.commissions[0].context.assignment_consumption==B6_ACCOUNT_COMMITTED_NEVER_REUSE);
 CHECK(r.records[2].transition.next.assignment.execution_uid==2001);CHECK(r.records[4].event.data.binding.output_device==0);
 start("known-failure-new-generation-preserves-old-assignment");base();append_event(2,3,1,1);append_event(7,4,1,1);append_event(2,12,1,2);
 r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);CHECK(r.commissions[0].context.last_reserved_attempt==2);
 CHECK(r.records[2].transition.next.assignment.execution_uid==2001);CHECK(r.records[4].transition.next.assignment.execution_uid==2002);
 CHECK(r.records[3].transition.next.phase==B6_ATTEMPT_RECONCILED_FAILED);
 start("recovery-begin-then-indeterminate-retirement");base();append_event(2,3,1,1);append_event(6,4,1,1);
 struct b6_syntax_event_frame f=s->proof[3].original_transition.validated_event;f.data.reconciliation_begin.cause=B6_SYNTAX_CAUSE_RECOVERY;
 s->proof[3].original_transition.observed_reconciliation_cause=B6_SYNTAX_CAUSE_RECOVERY;rewrite(3,f);
 append_event(10,8,1,1);r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
 CHECK(r.commissions[0].context.commission==B6_COMMISSION_RETIRED_INDETERMINATE);CHECK(r.commissions[0].context.assignment_consumption==B6_ACCOUNT_COMMITTED_NEVER_REUSE);
 start("independent-interleaved-commissions-global-chain");base();append_event(1,2,2,0);append_event(2,3,1,1);append_event(2,3,2,1);append_event(7,4,1,1);append_event(10,4,2,1);
 r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);CHECK(r.commission_count==2);CHECK(r.last_event_seq==7);
 CHECK(r.commissions[0].context.phase==B6_ATTEMPT_RECONCILED_FAILED);CHECK(r.commissions[1].context.phase==B6_ATTEMPT_RETIRED_UNKNOWN);
 start("terminal-commission-does-not-stop-unrelated-commission");base();append_event(9,3,1,0);append_event(1,2,2,0);append_event(2,3,2,1);
 r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);CHECK(r.commission_count==2);
}
static void prefix_matrix(void)
{for(size_t end=1;end<=8;++end)for(unsigned proof=0;proof<4;++proof){
 start("prefix-current-consumption-terminal-boundary");complete_accepted();
 s->input.record_count=end;s->input.journal_length=s->ends[end-1];final_digest();
 s->input.scope=proof==0?B6_REPLAY_HISTORICAL_PREFIX:B6_REPLAY_COMPLETE_CURRENT_CANDIDATE;
 s->input.complete_current_identity_continuity=proof==1?B6_EXTERNAL_VALIDATED:(proof==2?B6_EXTERNAL_UNVALIDATED:B6_EXTERNAL_INVALID);
 /* Strict prefixes of this known complete fixture never have current proof.
    Even a caller selecting current-candidate cannot derive completeness from bytes. */
 if(end<8 && proof==1)s->input.complete_current_identity_continuity=B6_EXTERNAL_UNVALIDATED;
 enum b6_replay_disposition wanted=(end==8 && proof==1)?B6_REPLAY_COMPLETE_CURRENT_STATE:B6_REPLAY_HISTORICAL_STATE;
 struct b6_account_replay_result r=replay(wanted);
 if(end>=2){CHECK(r.commission_count==1);CHECK(r.commissions[0].context.last_reserved_attempt==(end>=3?1u:0u));
  CHECK(r.commissions[0].context.assignment_consumption==(end>=3?B6_ACCOUNT_COMMITTED_NEVER_REUSE:B6_ACCOUNT_NO_LINKED_ASSIGNMENT));
  CHECK(r.commissions[0].context.commission==(end==8?B6_COMMISSION_FULFILLED:B6_COMMISSION_REGISTERED));}
 CHECK(end==8 || r.current_context_present==B6_ACCOUNT_FACT_NO);
 }}
static void evidence_matrix(void)
{for(size_t at=0;at<8;++at)for(unsigned field=0;field<4;++field)for(unsigned value=0;value<3;++value){
 start("original-proof-missing-invalid-or-unknown-tag");complete_accepted();
 enum b6_external_disposition d=value==0?B6_EXTERNAL_UNVALIDATED:(value==1?B6_EXTERNAL_INVALID:(enum b6_external_disposition)99);
 if(field==0)s->proof[at].original_preceding_projection=d;
 if(field==1)s->proof[at].original_writer_provenance=d;
 if(field==2)s->proof[at].retained_original_sources=d;
 if(field==3)s->proof[at].original_durable_commit=d;
 (void)replay(value==1?B6_REPLAY_INVALID_HISTORY:B6_REPLAY_UNDETERMINED);
 }
 for(unsigned field=0;field<3;++field)for(unsigned value=0;value<3;++value){
 start("protected-store-custody-historical-identity-and-dual-ledger-proof");complete_accepted();
 enum b6_external_disposition d=value==0?B6_EXTERNAL_UNVALIDATED:(value==1?B6_EXTERNAL_INVALID:(enum b6_external_disposition)99);
 if(field==0)s->input.protected_journal_store_custody=d;
 if(field==1)s->input.historical_identity_continuity=d;
 if(field==2)s->input.required_custody_history=d;
 (void)replay(value==1?B6_REPLAY_INVALID_HISTORY:B6_REPLAY_UNDETERMINED);
 }
 /* Specific original roles are independently challenged, never satisfied by PID,
    output identifiers, report/ref grammar or a historical generic saved verdict. */
 const struct {size_t at,offset;} roles[]={
 {2,OFFSET(current_human_permit)},{2,OFFSET(current_host_observation)},
 {4,OFFSET(authenticated_process_instance_ancestry)},{4,OFFSET(output_provenance)},
 {4,OFFSET(post_request_k_root)},{4,OFFSET(current_live_binding_context)},
 {5,OFFSET(separate_future_enabling_authority)},{5,OFFSET(final_admission_predicates)},
 {7,OFFSET(accepted_procedure_wait_terminal_host_continuity)},
 {7,OFFSET(process_output_effect_identity_reconciled)}};
 for(size_t i=0;i<sizeof roles/sizeof roles[0];++i)for(unsigned neg=0;neg<2;++neg){
 start("named-original-evidence-role-firewall");complete_accepted();
 enum b6_external_disposition *d=(enum b6_external_disposition *)((unsigned char *)&s->proof[roles[i].at].original_transition.dispositions+roles[i].offset);
 *d=neg?B6_EXTERNAL_INVALID:B6_EXTERNAL_UNVALIDATED;(void)replay(neg?B6_REPLAY_INVALID_HISTORY:B6_REPLAY_UNDETERMINED);
 }
 start("parsed-recovery-reference-does-not-validate-authority");base();append_event(2,3,1,1);append_event(10,4,1,1);
 s->proof[3].original_transition.dispositions.recovery_authority=B6_EXTERNAL_UNVALIDATED;(void)replay(B6_REPLAY_UNDETERMINED);
 start("claimed-original-current-history-flag-cannot-bypass-qualified-prefix-proof");complete_accepted();
 s->proof[3].original_transition.dispositions.complete_current_history=B6_EXTERNAL_VALIDATED;(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("changed-prefix-proof-is-not-current-history");complete_accepted();s->proof[5].preceding_prefix_sha256.text[0]^=1;(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("changed-validated-event-snapshot");complete_accepted();++s->proof[4].original_transition.validated_event.data.binding.output_inode;(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("changed-event-payload-proof");complete_accepted();s->proof[4].original_transition.event_payload_sha256.text[0]^=1;(void)replay(B6_REPLAY_INVALID_HISTORY);
}
static void corruption(void)
{
 for(size_t cut=0;cut<8;++cut){start("malformed-first-middle-or-final-record");complete_accepted();
  size_t offset=cut?s->ends[cut-1]:0;s->bytes[offset]='0';final_digest();(void)replay(B6_REPLAY_INVALID_HISTORY);}
 for(size_t back=1;back<=80;++back){start("partial-final-frame-no-prefix-salvage");complete_accepted();
  s->input.journal_length-=back;final_digest();(void)replay(B6_REPLAY_INVALID_HISTORY);}
 for(unsigned fault=0;fault<10;++fault){start("sequence-head-store-initialization-and-physical-duplicates");base();
  struct b6_syntax_event_frame f=s->proof[1].original_transition.validated_event;
  if(fault==0)f.common.event_seq=3;
  if(fault==1)f.common.event_seq=1;
  if(fault==2)f.common.event_seq=UINT64_MAX;
  if(fault==3)f.common.previous_event.digest.text[0]='f';
  if(fault==4)f.common.store_id.text[0]='1';
  if(fault==5)f.common.writer_pid=0; /* Closed serializer must refuse before replay. */
  if(fault==5){struct b6_syntax_event_bytes b;CHECK(b6_syntax_serialize_event_frame(&f,&b)!=B6_SYNTAX_OK);continue;}
  rewrite(1,f);
  if(fault==6){size_t n=s->ends[0];memcpy(s->bytes+n,s->bytes,n);s->input.journal_length=n*2;final_digest();}
  if(fault==7){s->input.store_identity=s->bytes;s->input.store_identity_length=s->ends[0];}
  if(fault==8)s->input.validated_store_identity.store_id.text[0]='1';
  if(fault==9)s->input.validated_journal_sha256.text[0]='f';
  (void)replay(B6_REPLAY_INVALID_HISTORY);
 }
 for(unsigned fault=0;fault<6;++fault){start("unknown-event-version-CR-NUL-and-corrupted-frame-digest");base();
  size_t off=s->ends[0],tab=off;while(s->bytes[tab]!='\t')++tab;
  if(fault==0)s->bytes[tab+1]='g';
  if(fault==1)s->bytes[tab+66+5]=0;
  if(fault==2)s->bytes[tab+66+5]='\r';
  if(fault>=3){unsigned char *p=(unsigned char *)strstr((char *)s->bytes+off,"B6_ATTEMPT_ACCOUNT_EVENT_V1");CHECK(p!=NULL);
   if(fault==3)p[strlen("B6_ATTEMPT_ACCOUNT_EVENT_V1")-1]='2';
   if(fault==4){p=(unsigned char *)strstr((char *)s->bytes+off,"REGISTER_COMMISSION");CHECK(p!=NULL);p[0]='X';}
   if(fault==5)s->bytes[s->input.journal_length++]='x';
  }
  if(fault>=1 && fault<=4)repair_local_hash(1);
  final_digest();(void)replay(B6_REPLAY_INVALID_HISTORY);
 }
 for(unsigned mode=0;mode<4;++mode){start("duplicate-terminal-application-and-readonly-ack-not-record");base();append_event(9,3,1,0);
  if(mode<3){append_event(9,15,1,0);if(mode==1)s->proof[3].original_transition.operation=B6_ACCOUNT_ACK_COMMITTED;
   if(mode==2)s->proof[3].original_transition.validated_event.data.unused_closure.closure_reason=B6_SYNTAX_CLOSURE_EXPIRY;
  }else append_event(2,15,1,1);
  (void)replay(B6_REPLAY_INVALID_HISTORY);
 }
 start("unused-close-after-reservation-is-unauthorized-history");base();append_event(2,3,1,1);append_event(9,4,1,0);(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("consumed-principal-cannot-reappear-after-another-generation");base();append_event(2,3,1,1);append_event(7,4,1,1);append_event(2,12,1,2);append_event(7,4,1,2);append_event(2,12,1,3);
 s->proof[6].original_transition.reserved_assignment=s->proof[2].original_transition.reserved_assignment;(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("unresolved-other-commission-resource-cannot-be-hidden-by-validated-flag");base();append_event(1,2,2,0);
 struct b6_syntax_event_frame conflicting=s->proof[2].original_transition.validated_event;
 conflicting.data.registration.resource_key=s->proof[1].original_transition.enrollment_resource_key;
 s->proof[2].original_transition.enrollment_resource_key=conflicting.data.registration.resource_key;
 s->proof[2].original_transition.enrollment_scope.output_path=s->proof[1].original_transition.enrollment_scope.output_path;
 s->proof[2].original_transition.current_permit.output_path=s->proof[1].original_transition.current_permit.output_path;
 rewrite(2,conflicting);append_event(2,3,1,1);append_event(2,3,2,1);
 s->proof[4].original_transition.current_permit.output_path=s->proof[1].original_transition.current_permit.output_path;
 (void)replay(B6_REPLAY_INVALID_HISTORY);
 start("omitted-proof-row-no-usable-partial-output");complete_accepted();--s->input.record_count;(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("extra-proof-row-not-matching-history");base();++s->input.record_count;(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("first-record-with-no-authorized-genesis");append_event(1,2,1,0);
 struct b6_syntax_event_frame f=s->proof[0].original_transition.validated_event;f.common.previous_event.kind=B6_SYNTAX_HEAD_DIGEST;h(&f.common.previous_event.digest);rewrite(0,f);(void)replay(B6_REPLAY_INVALID_HISTORY);
 start("zero-byte-account-is-not-free-initial-state");(void)replay(B6_REPLAY_UNDETERMINED);
}
static void writer_firewall(void)
{for(unsigned variant=0;variant<6;++variant){start("historical-writer-output-boot-clock-without-host-lookups");base();append_event(2,3,1,1);append_event(3,4,1,1);append_event(4,5,1,1);
 struct b6_syntax_event_frame f=s->proof[4].original_transition.validated_event;
 if(variant==0){f.common.writer_pid=2147483647u;f.common.writer_starttime=1;}
 if(variant==1){f.common.writer_pid=2;f.common.writer_starttime=999999999u;}
 if(variant==2){f.data.binding.output_device=0;f.data.binding.output_inode=UINT64_MAX;}
 if(variant==3){f.common.recorded_boot_cs=0;}
 if(variant==4){f.common.recorded_boot_cs=1;}
 if(variant==5){f.common.writer_starttime=1;f.common.recorded_boot_cs=0;}
 rewrite(4,f);struct b6_account_replay_result r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
 CHECK(r.records[4].event.common.writer_pid==f.common.writer_pid);
 CHECK(r.records[4].event.common.writer_starttime==f.common.writer_starttime);
 CHECK(r.records[4].event.common.recorded_boot_cs==f.common.recorded_boot_cs);
 }
 start("account-clock-decrease-does-not-import-custody-clock-policy");base();
 struct b6_syntax_event_frame f=s->proof[1].original_transition.validated_event;f.common.recorded_boot_cs=99;rewrite(1,f);
 append_event(2,3,1,1);struct b6_account_replay_result r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
 CHECK(r.records[1].event.common.recorded_boot_cs==99);CHECK(r.records[2].event.common.recorded_boot_cs==0);
 start("writer-boot-change-not-one-comparable-clock");append_event(0,0,0,0);
 f=s->proof[0].original_transition.validated_event;f.common.writer_boot_id.text[0]='1';f.common.recorded_boot_cs=100000;
 rewrite(0,f);append_event(1,2,1,0);(void)replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
}
static void arguments(void)
{start("explicit-output-capacity-unavailable");base();struct b6_replay_workspace w={s->scratch,1,s->commissions,N};
 struct b6_account_replay_result r=b6_account_replay(&s->input,w);CHECK(r.disposition==B6_REPLAY_UNDETERMINED);CHECK(!r.records && !r.current_context_present);
 start("unknown-history-scope-not-current");base();s->input.scope=(enum b6_replay_history_scope)99;(void)replay(B6_REPLAY_UNDETERMINED);
 start("size_t-capacity-overflow-safe");base();w=(struct b6_replay_workspace){s->scratch,SIZE_MAX,s->commissions,N};r=b6_account_replay(&s->input,w);CHECK(r.disposition==B6_REPLAY_UNDETERMINED);CHECK(!r.records);
 start("size_t-proof-count-overflow-safe");base();s->input.record_count=SIZE_MAX;w=(struct b6_replay_workspace){s->scratch,N,s->commissions,N};r=b6_account_replay(&s->input,w);CHECK(r.disposition==B6_REPLAY_UNDETERMINED);CHECK(!r.records);
 start("workspace-aliases-input-no-input-mutation");base();unsigned char original[128];memcpy(original,s->bytes,sizeof original);
 w=(struct b6_replay_workspace){(struct b6_replayed_record *)(void *)s->bytes,1,s->commissions,N};r=b6_account_replay(&s->input,w);CHECK(r.disposition==B6_REPLAY_UNDETERMINED);CHECK(memcmp(original,s->bytes,sizeof original)==0);
 start("stale-success-cannot-escape-following-late-failure");complete_accepted();r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
 s->proof[7].retained_original_sources=B6_EXTERNAL_UNVALIDATED;r=replay(B6_REPLAY_UNDETERMINED);CHECK(!r.records && !r.commissions);
}
static void source_history(size_t row)
{
 if(row==0)return;
 append_event(0,0,0,0);if(row==2)return;
 append_event(1,2,1,0);if(row==3)return;
 if(row==15){append_event(9,3,1,0);return;}
 struct b6_account_context c=context(row);append_event(2,3,1,1);
 if(c.preparation_intent==B6_ACCOUNT_FACT_YES)append_event(3,4,1,1);
 if(c.native_binding==B6_ACCOUNT_FACT_YES)append_event(4,5,1,1);
 if(c.execution_admission==B6_ACCOUNT_FACT_YES)append_event(5,6,1,1);
 if(c.phase==B6_ATTEMPT_RECONCILING)append_event(6,7,1,1);
 if(c.phase==B6_ATTEMPT_RECONCILED_FAILED)append_event(7,4,1,1);
 if(c.phase==B6_ATTEMPT_QUALIFICATION_ACCEPTED)append_event(8,7,1,1);
 if(c.phase==B6_ATTEMPT_RETIRED_UNKNOWN)append_event(10,4,1,1);
}
static void transition_replay_matrix(void)
{
 const enum b6_attempt_phase target[]={B6_ATTEMPT_NONE,B6_ATTEMPT_NONE,B6_ATTEMPT_RESERVED_UNRECONCILED,
  B6_ATTEMPT_RESERVED_UNRECONCILED,B6_ATTEMPT_RESERVED_UNRECONCILED,B6_ATTEMPT_ADMITTED_UNRECONCILED,
  B6_ATTEMPT_RECONCILING,B6_ATTEMPT_RECONCILED_FAILED,B6_ATTEMPT_QUALIFICATION_ACCEPTED,B6_ATTEMPT_NONE,B6_ATTEMPT_RETIRED_UNKNOWN};
 for(size_t row=0;row<CONTEXT_COUNT;++row)for(size_t index=0;index<11;++index){
  if(row==1 || !(legal[row]&(1u<<index)))continue;
  start("every-frozen-legal-transition-through-original-history");source_history(row);
  uint64_t attempt=(index==0 || index==1 || index==9)?0u:(index==2?(row==3?1u:2u):1u);
  append_event(index,row,1,attempt);struct b6_account_replay_result r=replay(B6_REPLAY_COMPLETE_CURRENT_STATE);
  printf("EDGE\t%zu\t%s\tALLOWED\tCASE_%zu\n",row,names[index],cases);
  const struct b6_account_transition_result *last=&r.records[r.record_count-1].transition;
  CHECK(last->effect==(enum b6_transition_effect)(index+1));CHECK(last->next.phase==target[index]);
  CHECK(last->generation_effect==(index==2?B6_GENERATION_NEW_POSITIVE_ATTEMPT:B6_GENERATION_NO_CHANGE));
  CHECK(last->attempt_terminal==(index==7 || index==8 || index==10?B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO));
  CHECK(last->commission_terminal==(index==8 || index==9 || index==10?B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO));
 }
 for(size_t row=2;row<CONTEXT_COUNT;++row)for(size_t index=1;index<11;++index){
  if(legal[row]&(1u<<index))continue;
  start("closed-world-wrong-context-stored-event-invalidates-whole-history");source_history(row);
  uint64_t attempt=(index==1 || index==9)?0u:(index==2?(row==2 || row==3 || row==15?1u:2u):1u);
  append_event(index,row,1,attempt);(void)replay(B6_REPLAY_INVALID_HISTORY);
  printf("EDGE\t%zu\t%s\tREJECTED\tCASE_%zu\n",row,names[index],cases);
 }
}
int main(void)
{s=calloc(1,sizeof *s);if(!s){fprintf(stderr,"fixture allocation failed\n");return 1;}
 positive();prefix_matrix();evidence_matrix();corruption();writer_firewall();arguments();transition_replay_matrix();
 printf("PASS cases=%zu assertions=%zu\n",cases,assertions);free(s);return 0;}
