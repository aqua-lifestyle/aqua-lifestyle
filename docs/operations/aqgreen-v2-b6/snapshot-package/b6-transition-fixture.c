#include "attempt-account-transition.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stddef.h>

static size_t cases,assertions;static const char *active;
enum { CONTEXT_COUNT=22 };
#define CHECK(x) do {++assertions;if(!(x)){fprintf(stderr,"FAIL %s line%d: %s\n",active,__LINE__,#x);exit(1);}}while(0)
static void begin(size_t row,size_t event,const char *property)
{active=property;++cases;printf("CASE\t%zu\t%zu\t%zu\t%s\n",cases,row,event,property);}
static int zero(const void *p,size_t n)
{const unsigned char *b=p;for(size_t i=0;i<n;++i)if(b[i])return 0;return 1;}
static const char uuid[]="01234567-89ab-cdef-0123-456789abcdef";
static const char hex[]="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
static const char *const names[]={"GENESIS","REGISTER_COMMISSION","RESERVE_ATTEMPT","PREPARATION_INTENT","BIND_NATIVE","ADMIT_EXECUTION","BEGIN_RECONCILIATION","RECONCILED_FAILED","QUALIFICATION_ACCEPTED","CLOSE_UNUSED_COMMISSION","RETIRE_INDETERMINATE"};
static const enum b6_syntax_event_type events[]={B6_SYNTAX_GENESIS,B6_SYNTAX_REGISTER_COMMISSION,B6_SYNTAX_RESERVE_ATTEMPT,B6_SYNTAX_PREPARATION_INTENT,B6_SYNTAX_BIND_NATIVE,B6_SYNTAX_ADMIT_EXECUTION,B6_SYNTAX_BEGIN_RECONCILIATION,B6_SYNTAX_RECONCILED_FAILED,B6_SYNTAX_QUALIFICATION_ACCEPTED,B6_SYNTAX_CLOSE_UNUSED_COMMISSION,B6_SYNTAX_RETIRE_INDETERMINATE};
static void h(struct b6_syntax_hex *x){memcpy(x->text,hex,sizeof hex);}
static void u(struct b6_syntax_uuid *x){memcpy(x->text,uuid,sizeof uuid);}
static struct b6_syntax_scope scope(void)
{
 struct b6_syntax_scope s;memset(&s,0,sizeof s);strcpy(s.commission_id.text,"commission_1");
 strcpy(s.grant_reference.text,"grant_1");u(&s.grant_boot_id);s.authority_uid=1000;s.authority_gid=1000;
 h(&s.execution_principal_pool_sha256);strcpy(s.output_path.text,"/tmp/modeled-b6-output");
 h(&s.materialization_manifest_sha256);h(&s.qualification_procedure_sha256);h(&s.host_contract_sha256);
 s.not_before_boot_cs=0;s.not_after_boot_cs=100;return s;
}
static struct b6_syntax_permit permit(void)
{
 struct b6_syntax_scope s=scope();struct b6_syntax_permit p;memset(&p,0,sizeof p);
 p.qualification_id=s.commission_id;p.authority_reference=s.grant_reference;p.boot_id=s.grant_boot_id;
 p.authority_uid=s.authority_uid;p.authority_gid=s.authority_gid;p.output_path=s.output_path;
 p.execution_principal_pool_sha256=s.execution_principal_pool_sha256;
 p.materialization_manifest_sha256=s.materialization_manifest_sha256;
 p.qualification_procedure_sha256=s.qualification_procedure_sha256;p.host_contract_sha256=s.host_contract_sha256;
 h(&p.host_observation_sha256);p.not_before_boot_cs=0;p.not_after_boot_cs=100;return p;
}
static struct b6_syntax_assignment assignment(int fresh)
{
 struct b6_syntax_assignment a;memset(&a,0,sizeof a);u(&a.generation);u(&a.custody_store_id);
 if(fresh) a.generation.text[0]='1';
 h(&a.pool_sha256);a.allocation_ordinal=fresh?2u:1u;
 a.execution_uid=fresh?2001u:2000u;a.execution_gid=a.execution_uid;
 a.prior_custody_head.kind=B6_SYNTAX_HEAD_GENESIS;return a;
}
/* S §2/§3/§4: 22 meaningful account contexts, no flattened unrelated cross-product.
 0provisioning;1unknown;2unregistered;3registered/noattempt;4reserved/bare;
 5reserved/intent;6reserved/bound;7admitted;8-11reconciling/history bare-intent-bound-admit;
 12failed/bare;13failed/admitted;14fulfilled;15closed;16retired/bare;17retired/admitted;
 18failed/intent;19failed/bound;20retired/intent;21retired/bound. */
static struct b6_account_context context(size_t row)
{
 struct b6_account_context c;memset(&c,0,sizeof c);c.kind=B6_ACCOUNT_NORMAL_CONTEXT;u(&c.store_id);
 c.format_version=B6_SYNTAX_FORMAT_V1;c.commission=B6_COMMISSION_REGISTERED;
 strcpy(c.commission_id.text,"commission_1");c.immutable_scope=scope();h(&c.commission_scope_sha256);
 h(&c.resource_key);h(&c.retained_history_sha256);c.phase=B6_ATTEMPT_NONE;
 c.preparation_intent=c.native_binding=c.execution_admission=B6_ACCOUNT_FACT_NO;
 c.assignment_consumption=B6_ACCOUNT_NO_LINKED_ASSIGNMENT;
 if(row==0){c.kind=B6_ACCOUNT_PROVISIONING_CONTEXT;c.format_version=B6_SYNTAX_NO_VERSION;c.commission=B6_COMMISSION_UNREGISTERED;}
 if(row==0) memset(&c.retained_history_sha256,0,sizeof c.retained_history_sha256);
 if(row==1)c.kind=B6_ACCOUNT_UNKNOWN_OR_DAMAGED;
 if(row==2)c.commission=B6_COMMISSION_UNREGISTERED;
 if(row>=4 && row!=15){
  c.last_reserved_attempt=1;c.assignment=assignment(0);c.assignment_consumption=B6_ACCOUNT_COMMITTED_NEVER_REUSE;
  c.original_attempt_permit=permit();h(&c.preparation_descriptor_sha256);c.phase=B6_ATTEMPT_RESERVED_UNRECONCILED;
  if(row==5 || row==6 || row==7 || row==9 || row==10 || row==11 || row==13 || row==14 || row==17 || row>=18)c.preparation_intent=B6_ACCOUNT_FACT_YES;
  if(row==6 || row==7 || row==10 || row==11 || row==13 || row==14 || row==17 || row==19 || row==21){
   c.native_binding=B6_ACCOUNT_FACT_YES;h(&c.binding.native_transaction_id);c.binding.supervisor_pid=2;
   c.binding.supervisor_starttime=1;c.binding.bash_pid=3;c.binding.bash_starttime=2;
   c.binding.output_device=0;c.binding.output_inode=1;
  }
  if(row==7 || row==11 || row==13 || row==14 || row==17)c.execution_admission=B6_ACCOUNT_FACT_YES;
  if(row==7)c.phase=B6_ATTEMPT_ADMITTED_UNRECONCILED;
  if(row>=8 && row<=11)c.phase=B6_ATTEMPT_RECONCILING;
  if(row==12 || row==13 || row==18 || row==19)c.phase=B6_ATTEMPT_RECONCILED_FAILED;
  if(row==14){c.phase=B6_ATTEMPT_QUALIFICATION_ACCEPTED;c.commission=B6_COMMISSION_FULFILLED;}
  if(row==16 || row==17 || row==20 || row==21){c.phase=B6_ATTEMPT_RETIRED_UNKNOWN;c.commission=B6_COMMISSION_RETIRED_INDETERMINATE;}
 }
 if(row==15)c.commission=B6_COMMISSION_CLOSED_UNUSED;
 return c;
}
static struct b6_syntax_event_frame event(size_t index,const struct b6_account_context *c)
{
 struct b6_syntax_event_frame f;memset(&f,0,sizeof f);f.state=B6_SYNTAX_GRAMMAR_ONLY;f.event_type=events[index];
 f.common.store_id=c->store_id;f.common.event_seq=index==0?1u:2u;
 f.common.previous_event.kind=index==0?B6_SYNTAX_HEAD_GENESIS:B6_SYNTAX_HEAD_DIGEST;
 if(index) h(&f.common.previous_event.digest);
 strcpy(f.common.commission_id.text,index?"commission_1":"STORE");
 f.common.attempt_number=(index==0 || index==1 || index==9)?0u:(c->last_reserved_attempt?c->last_reserved_attempt:1u);
 if(index==2)f.common.attempt_number=c->last_reserved_attempt==UINT64_MAX?1u:c->last_reserved_attempt+1u;
 u(&f.common.writer_boot_id);f.common.writer_pid=2;f.common.writer_starttime=1;f.common.recorded_boot_cs=0;
 f.common.evidence.kind=index?B6_SYNTAX_EVIDENCE_DIGEST:B6_SYNTAX_EVIDENCE_NOT_APPLICABLE;
 if(index)h(&f.common.evidence.digest);
 switch(index){
 case 0:f.data.genesis.format_version=B6_SYNTAX_FORMAT_V1;break;
 case 1:strcpy(f.data.registration.grant_reference.text,"grant_1");h(&f.data.registration.enrollment_permit_sha256);h(&f.data.registration.commission_scope_sha256);h(&f.data.registration.resource_key);break;
 case 2:h(&f.data.reservation.permit_snapshot_sha256);h(&f.data.reservation.observation_snapshot_sha256);h(&f.data.reservation.native_preparation_descriptor_sha256);break;
 case 3:h(&f.data.preparation.supervisor_executable_sha256);break;
 case 4:h(&f.data.binding.native_transaction_id);f.data.binding.supervisor_pid=2;f.data.binding.supervisor_starttime=1;f.data.binding.bash_pid=3;f.data.binding.bash_starttime=2;f.data.binding.output_device=0;f.data.binding.output_inode=1;break;
 case 5:h(&f.data.admission.admission_permit_sha256);h(&f.data.admission.admission_observation_sha256);h(&f.data.admission.admission_host_evidence_sha256);break;
 case 6:f.data.reconciliation_begin.cause=B6_SYNTAX_CAUSE_COMPLETION;break;
 case 7:h(&f.data.reconciled_failed.reconciliation_report_sha256);break;
 case 8:h(&f.data.qualification_accepted.reconciliation_report_sha256);break;
 case 9:f.data.unused_closure.closure_reason=B6_SYNTAX_CLOSURE_WITHDRAWAL;break;
 case 10:strcpy(f.data.retirement.recovery_authority_reference.text,"unproven/recovery_1");h(&f.data.retirement.recovery_report_sha256);break;
 default:abort();
 }
 return f;
}
static void bind_proof(struct b6_account_evidence *e,const struct b6_syntax_event_frame *f)
{
 struct b6_syntax_event_bytes b;CHECK(b6_syntax_serialize_event_frame(f,&b)==B6_SYNTAX_OK);
 size_t tab=0;while(tab<b.length && b.bytes[tab]!='\t')++tab;CHECK(tab<b.length);
 memcpy(e->event_payload_sha256.text,b.bytes+tab+1u,64);e->event_payload_sha256.text[64]=0;
 e->validated_payload_sha256=e->event_payload_sha256;e->validated_event=*f;
}
#define FIELDS(X) \
 X(complete_current_history) X(exact_successor_lineage) X(store_identity_continuity) X(event_supporting_evidence) \
 X(provisioning_authority_no_prior_obligation) X(enrollment_human_permit_scope) X(deliberate_invocation) \
 X(current_human_permit) X(current_host_observation) X(source_output_custody) X(resource_conflict_disposition) \
 X(exact_hold_preparation_assignment) X(prior_custody_released) X(preparation_plan_source) \
 X(authenticated_process_instance_ancestry) X(output_provenance) X(assignment_witness_exposed) \
 X(post_request_k_root) X(current_live_binding_context) X(final_admission_predicates) \
 X(separate_future_enabling_authority) X(reconciliation_trigger) X(known_failure_nonadmission) \
 X(process_output_effect_identity_reconciled) X(old_actor_cannot_act) \
 X(accepted_procedure_wait_terminal_host_continuity) X(withdrawal_or_expiry) X(recovery_authority) \
 X(recovery_safe_resource_disposition) X(retained_committed_request_evidence) X(committed_evidence_durability) \
 X(original_request_correlation)
static struct b6_account_evidence evidence(size_t index,const struct b6_syntax_event_frame *f)
{
 struct b6_account_evidence e;memset(&e,0,sizeof e);e.operation=B6_ACCOUNT_APPLY_FRESH;
#define SET(field) e.dispositions.field=B6_EXTERNAL_VALIDATED;
 FIELDS(SET)
#undef SET
 e.current_permit=permit();e.enrollment_scope=scope();h(&e.enrollment_scope_sha256);h(&e.enrollment_resource_key);
 e.reserved_assignment=assignment(1);e.reconciliation_result=index==8?B6_SYNTAX_RESULT_ACCEPTED:
  (index==10?B6_SYNTAX_RESULT_INDETERMINATE:B6_SYNTAX_RESULT_RECONCILED_FAILED);
 h(&e.reconciliation_report_sha256);e.observed_reconciliation_cause=B6_SYNTAX_CAUSE_COMPLETION;
 e.established_closure_reason=B6_SYNTAX_CLOSURE_WITHDRAWAL;bind_proof(&e,f);return e;
}
static struct b6_account_transition_result verify(const struct b6_account_context *c,
 const struct b6_syntax_event_frame *f,const struct b6_account_evidence *e,enum b6_transition_disposition wanted)
{
 struct b6_account_context oldc=*c;struct b6_syntax_event_frame oldf=*f;struct b6_account_evidence olde=*e;
 struct b6_account_transition_result r=b6_account_transition_validate(c,f,e);
 CHECK(r.disposition==wanted);
 CHECK(memcmp(c,&oldc,sizeof oldc)==0);CHECK(memcmp(f,&oldf,sizeof oldf)==0);CHECK(memcmp(e,&olde,sizeof olde)==0);
 struct b6_account_transition_result again=b6_account_transition_validate(c,f,e);
 CHECK(memcmp(&r,&again,sizeof r)==0);
 if(wanted==B6_TRANSITION_REJECTED || wanted==B6_TRANSITION_UNDETERMINED){
  CHECK(r.next_context_present==B6_ACCOUNT_FACT_UNKNOWN);CHECK(zero(&r.next,sizeof r.next));
  CHECK(r.effect==B6_TRANSITION_NO_EFFECT);CHECK(r.generation_effect==B6_GENERATION_NO_CHANGE);
  CHECK(r.consumption_effect==B6_CONSUMPTION_NO_EFFECT);CHECK(r.attempt_terminal==B6_ACCOUNT_FACT_UNKNOWN);
  CHECK(r.commission_terminal==B6_ACCOUNT_FACT_UNKNOWN);CHECK(zero(&r.acknowledged_payload_sha256,sizeof r.acknowledged_payload_sha256));
 }else{
  CHECK(r.next_context_present==B6_ACCOUNT_FACT_YES);
  CHECK(r.consumption_effect==B6_CONSUMPTION_PRESERVE_COMMITTED_ASSIGNMENTS);
  CHECK(memcmp(&r.next.retained_history_sha256,&c->retained_history_sha256,sizeof c->retained_history_sha256)==0);
  if(wanted==B6_TRANSITION_ACKNOWLEDGED){CHECK(memcmp(c,&r.next,sizeof *c)==0);CHECK(r.effect==B6_TRANSITION_NO_EFFECT);CHECK(r.generation_effect==B6_GENERATION_NO_CHANGE);}
 }
 return r;
}
/* Handwritten legal cells from accepted clauses, independent of implementation switch. */
static const unsigned legal[CONTEXT_COUNT]={1u<<0,0,1u<<1,(1u<<2)|(1u<<9),
 (1u<<3)|(1u<<6)|(1u<<7)|(1u<<10),(1u<<4)|(1u<<6)|(1u<<7)|(1u<<10),
 (1u<<5)|(1u<<6)|(1u<<7)|(1u<<10),(1u<<6)|(1u<<7)|(1u<<8)|(1u<<10),
 (1u<<7)|(1u<<10),(1u<<7)|(1u<<10),(1u<<7)|(1u<<10),(1u<<7)|(1u<<8)|(1u<<10),
 1u<<2,1u<<2,0,0,0,0,1u<<2,1u<<2,0,0};
static void matrix(void)
{
 for(size_t row=0;row<CONTEXT_COUNT;++row)for(size_t index=0;index<11;++index){
  begin(row,index,"frozen-context-by-event-disposition");struct b6_account_context c=context(row);
  struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
  enum b6_transition_disposition expected=row==1?B6_TRANSITION_UNDETERMINED:
   ((legal[row]&(1u<<index))?B6_TRANSITION_ALLOWED:B6_TRANSITION_REJECTED);
  struct b6_account_transition_result r=verify(&c,&f,&e,expected);
  if(expected==B6_TRANSITION_ALLOWED){
   CHECK(r.effect==(enum b6_transition_effect)(index+1));
   CHECK(r.generation_effect==(index==2?B6_GENERATION_NEW_POSITIVE_ATTEMPT:B6_GENERATION_NO_CHANGE));
   CHECK(r.next.last_reserved_attempt==(index==2?c.last_reserved_attempt+1u:c.last_reserved_attempt));
   if(index!=2)CHECK(memcmp(&r.next.assignment,&c.assignment,sizeof c.assignment)==0);
   const enum b6_attempt_phase target[]={B6_ATTEMPT_NONE,B6_ATTEMPT_NONE,B6_ATTEMPT_RESERVED_UNRECONCILED,
    B6_ATTEMPT_RESERVED_UNRECONCILED,B6_ATTEMPT_RESERVED_UNRECONCILED,B6_ATTEMPT_ADMITTED_UNRECONCILED,
    B6_ATTEMPT_RECONCILING,B6_ATTEMPT_RECONCILED_FAILED,B6_ATTEMPT_QUALIFICATION_ACCEPTED,B6_ATTEMPT_NONE,B6_ATTEMPT_RETIRED_UNKNOWN};
   CHECK(r.next.phase==target[index]);CHECK(r.attempt_terminal==(index==7||index==8||index==10?B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO));
   CHECK(r.commission_terminal==(index==8||index==9||index==10?B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO));
   CHECK(r.next.commission==(index==0?B6_COMMISSION_UNREGISTERED:(index==8?B6_COMMISSION_FULFILLED:
    (index==9?B6_COMMISSION_CLOSED_UNUSED:(index==10?B6_COMMISSION_RETIRED_INDETERMINATE:B6_COMMISSION_REGISTERED)))));
   if(index==3)CHECK(r.next.preparation_intent==B6_ACCOUNT_FACT_YES);
   if(index==4){CHECK(r.next.native_binding==B6_ACCOUNT_FACT_YES);CHECK(memcmp(&r.next.binding,&f.data.binding,sizeof f.data.binding)==0);}
   if(index==5)CHECK(r.next.execution_admission==B6_ACCOUNT_FACT_YES);
   if(index>=6){CHECK(r.next.preparation_intent==c.preparation_intent);CHECK(r.next.native_binding==c.native_binding);CHECK(r.next.execution_admission==c.execution_admission);}
  }
 }
}
#define OFFSET(field) offsetof(struct b6_account_external_dispositions,field)
struct gate {size_t offset;unsigned events;};
/* Exact external predicates mapped to semantic/primary clauses; irrelevant fields
   are deliberately not needed. Each required gate gets unvalidated/false/bad-tag. */
static const struct gate gates[]={
 {OFFSET(complete_current_history),2047u},{OFFSET(exact_successor_lineage),2047u},
 {OFFSET(store_identity_continuity),2047u},{OFFSET(event_supporting_evidence),2047u},
 {OFFSET(provisioning_authority_no_prior_obligation),1u<<0},
 {OFFSET(enrollment_human_permit_scope),1u<<1},{OFFSET(deliberate_invocation),1u<<2},
 {OFFSET(current_human_permit),(1u<<2)|(1u<<5)},{OFFSET(current_host_observation),(1u<<2)|(1u<<5)},
 {OFFSET(source_output_custody),1u<<2},{OFFSET(resource_conflict_disposition),(1u<<2)|(1u<<9)},
 {OFFSET(exact_hold_preparation_assignment),(1u<<2)|(1u<<3)},{OFFSET(preparation_plan_source),1u<<3},
 {OFFSET(authenticated_process_instance_ancestry),1u<<4},{OFFSET(output_provenance),1u<<4},
 {OFFSET(assignment_witness_exposed),1u<<4},{OFFSET(post_request_k_root),1u<<4},
 {OFFSET(current_live_binding_context),1u<<4},{OFFSET(final_admission_predicates),1u<<5},
 {OFFSET(separate_future_enabling_authority),1u<<5},{OFFSET(reconciliation_trigger),1u<<6},
 {OFFSET(known_failure_nonadmission),1u<<7},{OFFSET(process_output_effect_identity_reconciled),(1u<<7)|(1u<<8)},
 {OFFSET(old_actor_cannot_act),(1u<<7)|(1u<<8)|(1u<<10)},
 {OFFSET(accepted_procedure_wait_terminal_host_continuity),1u<<8},{OFFSET(withdrawal_or_expiry),1u<<9},
 {OFFSET(recovery_authority),1u<<10},{OFFSET(recovery_safe_resource_disposition),1u<<10}};
static void set_gate(struct b6_account_evidence *e,size_t offset,enum b6_external_disposition d)
{memcpy((unsigned char *)&e->dispositions+offset,&d,sizeof d);}
static void external_gates(void)
{
 for(size_t row=0;row<CONTEXT_COUNT;++row)for(size_t index=0;index<11;++index)if(legal[row]&(1u<<index)){
  for(size_t g=0;g<sizeof gates/sizeof gates[0];++g)if(gates[g].events&(1u<<index)){
   for(int v=0;v<3;++v){begin(row,index,"every-required-external-disposition-fails-closed");
    struct b6_account_context c=context(row);struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
    enum b6_external_disposition d=v==0?B6_EXTERNAL_UNVALIDATED:(v==1?B6_EXTERNAL_INVALID:(enum b6_external_disposition)99);
    set_gate(&e,gates[g].offset,d);
    int history=gates[g].offset==OFFSET(complete_current_history)||gates[g].offset==OFFSET(store_identity_continuity);
    (void)verify(&c,&f,&e,v==1&&!history?B6_TRANSITION_REJECTED:B6_TRANSITION_UNDETERMINED);
   }
  }
 }
 for(size_t index=0;index<11;++index){
  size_t row=0;while(!(legal[row]&(1u<<index)))++row;
  struct b6_account_context c=context(row);struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
  for(size_t g=0;g<sizeof gates/sizeof gates[0];++g)if(!(gates[g].events&(1u<<index))){
   begin(row,index,"unrelated-evidence-neither-required-nor-defaulted");struct b6_account_evidence changed=e;
   set_gate(&changed,gates[g].offset,B6_EXTERNAL_UNVALIDATED);(void)verify(&c,&f,&changed,B6_TRANSITION_ALLOWED);
  }
 }
 for(size_t row=12;row<=13;++row){begin(row,2,"retry-needs-old-custody-release");
  struct b6_account_context c=context(row);struct b6_syntax_event_frame f=event(2,&c);struct b6_account_evidence e=evidence(2,&f);
  e.dispositions.prior_custody_released=B6_EXTERNAL_UNVALIDATED;(void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
 }
 begin(4,6,"RECOVERY-cause-needs-valid-recovery-authority");struct b6_account_context c=context(4);
 struct b6_syntax_event_frame f=event(6,&c);f.data.reconciliation_begin.cause=B6_SYNTAX_CAUSE_RECOVERY;
 struct b6_account_evidence e=evidence(6,&f);e.observed_reconciliation_cause=B6_SYNTAX_CAUSE_RECOVERY;
 e.dispositions.recovery_authority=B6_EXTERNAL_UNVALIDATED;(void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
}
static void duplicates(void)
{
 for(size_t source=0;source<CONTEXT_COUNT;++source)for(size_t index=0;index<11;++index)if(legal[source]&(1u<<index)){
  begin(source,index,"fresh-application-then-exact-committed-acknowledgment-no-effects");
  struct b6_account_context c=context(source);struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
  struct b6_account_transition_result applied=verify(&c,&f,&e,B6_TRANSITION_ALLOWED);c=applied.next;
  /* Modeled commit is external to this validator: supply retained history fact
     for acknowledgment, never claim the pure GENESIS result committed itself. */
  if(index==0) h(&c.retained_history_sha256);
  e.operation=B6_ACCOUNT_ACK_COMMITTED;e.committed_event=f;e.committed_payload_sha256=e.event_payload_sha256;
  struct b6_account_transition_result ack=verify(&c,&f,&e,B6_TRANSITION_ACKNOWLEDGED);
  CHECK(memcmp(&ack.acknowledged_payload_sha256,&e.event_payload_sha256,sizeof e.event_payload_sha256)==0);
  for(size_t field=0;field<8;++field){begin(source,index,"changed-event-is-not-exact-committed-acknowledgment");
   struct b6_syntax_event_frame changed=f;struct b6_account_evidence proof=e;
   switch(field){case 0:++changed.common.event_seq;break;case 1:changed.common.recorded_boot_cs=1;break;
   case 2:changed.common.writer_pid=4;break;case 3:changed.common.writer_starttime=2;break;
   case 4:changed.common.writer_boot_id.text[0]='1';break;case 5:++changed.common.attempt_number;break;
   case 6:changed.common.store_id.text[0]='1';break;case 7:changed.common.commission_id.text[0]=index?'X':'S';
    if(!index) strcpy(changed.common.commission_id.text,"STORE");
    break;default:abort();}
   if(field==7 && !index){proof.committed_payload_sha256.text[0]='f';}
   else bind_proof(&proof,&changed);
   (void)verify(&c,&changed,&proof,B6_TRANSITION_REJECTED);
  }
  size_t ack_fields[]={OFFSET(retained_committed_request_evidence),OFFSET(committed_evidence_durability),OFFSET(original_request_correlation)};
  for(size_t g=0;g<3;++g){begin(source,index,"ack-requires-retained-request-evidence-durability-and-correlation");
   struct b6_account_evidence missing=e;set_gate(&missing,ack_fields[g],B6_EXTERNAL_UNVALIDATED);
   (void)verify(&c,&f,&missing,B6_TRANSITION_UNDETERMINED);
  }
  begin(source,index,"duplicate-fresh-event-cannot-repeat-effects");e.operation=B6_ACCOUNT_APPLY_FRESH;
  (void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 }
 /* Historical acknowledgment after a distinct generation returns current context,
    never rolls back to original target or resets the earlier terminal fact. */
 begin(13,7,"old-terminal-ack-after-distinct-generation-retains-current-state");
 struct b6_account_context failed=context(13);struct b6_syntax_event_frame old=event(7,&failed);
 struct b6_account_evidence oldproof=evidence(7,&old);oldproof.operation=B6_ACCOUNT_ACK_COMMITTED;
 oldproof.committed_event=old;oldproof.committed_payload_sha256=oldproof.event_payload_sha256;
 struct b6_account_context newer=context(4);newer.last_reserved_attempt=2;newer.assignment=assignment(1);
 (void)verify(&newer,&old,&oldproof,B6_TRANSITION_ACKNOWLEDGED);
}
static void identity_and_boundaries(void)
{
 for(size_t index=0;index<11;++index){size_t row=0;while(!(legal[row]&(1u<<index)))++row;
  struct b6_account_context c=context(row);struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
  begin(row,index,"wrong-store-proven-identity-rejects");struct b6_account_context changed=c;changed.store_id.text[0]='1';
  (void)verify(&changed,&f,&e,B6_TRANSITION_REJECTED);
  begin(row,index,"wrong-external-event-payload-binding-rejects");struct b6_account_evidence proof=e;proof.event_payload_sha256.text[0]='f';
  (void)verify(&c,&f,&proof,B6_TRANSITION_REJECTED);
  if(index!=0){begin(row,index,"wrong-commission-identity-rejects");
   struct b6_syntax_event_frame wrong=f;wrong.common.commission_id.text[0]='X';proof=e;bind_proof(&proof,&wrong);
   (void)verify(&c,&wrong,&proof,B6_TRANSITION_REJECTED);}
  if(index!=0 && index!=1 && index!=9){begin(row,index,"zero-attempt-is-grammar-valid-not-semantically-allowed");
   struct b6_syntax_event_frame zeroevent=f;zeroevent.common.attempt_number=0;proof=e;bind_proof(&proof,&zeroevent);(void)verify(&c,&zeroevent,&proof,B6_TRANSITION_REJECTED);}
  begin(row,index,"unknown-context-tag-does-not-invent-state");changed=c;changed.kind=(enum b6_account_context_kind)99;(void)verify(&changed,&f,&e,B6_TRANSITION_UNDETERMINED);
  begin(row,index,"unknown-evidence-operation-fails-with-no-next");proof=e;proof.operation=(enum b6_account_operation)99;(void)verify(&c,&f,&proof,B6_TRANSITION_REJECTED);
  begin(row,index,"unknown-event-enum-fails-at-typed-grammar-boundary");struct b6_syntax_event_frame bad=f;bad.event_type=(enum b6_syntax_event_type)99;
  (void)verify(&c,&bad,&e,B6_TRANSITION_REJECTED);
 }
 for(size_t row=4;row<CONTEXT_COUNT;++row)if(row!=15){
  for(size_t field=0;field<6;++field){begin(row,6,"unknown-or-contradictory-state-facts-remain-undetermined");
   struct b6_account_context c=context(row);struct b6_syntax_event_frame f=event(6,&c);struct b6_account_evidence e=evidence(6,&f);
   switch(field){case 0:c.phase=(enum b6_attempt_phase)99;break;case 1:c.commission=(enum b6_commission_disposition)99;break;
   case 2:c.preparation_intent=B6_ACCOUNT_FACT_UNKNOWN;break;case 3:c.last_reserved_attempt=0;break;
   case 4:c.assignment_consumption=B6_ACCOUNT_NO_LINKED_ASSIGNMENT;break;case 5:c.assignment.execution_uid=1000;break;default:abort();}
   (void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
  }
 }
 begin(12,2,"attempt-counter-overflow-no-wrap-or-retry-budget");struct b6_account_context c=context(12);c.last_reserved_attempt=UINT64_MAX;
 struct b6_syntax_event_frame f=event(2,&c);struct b6_account_evidence e=evidence(2,&f);(void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 for(size_t change=0;change<4;++change){begin(12,2,"retry-cannot-reuse-generation-E-or-mismatch-pool");c=context(12);f=event(2,&c);e=evidence(2,&f);
  if(change==0)e.reserved_assignment.generation=c.assignment.generation;
  if(change==1)e.reserved_assignment.execution_uid=e.reserved_assignment.execution_gid=c.assignment.execution_uid;
  if(change==2)e.reserved_assignment.pool_sha256.text[0]='f';
  if(change==3)e.reserved_assignment.execution_gid=2002;
  (void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 }
 for(size_t index=1;index<=5;++index)if(index==1||index==2||index==5){
  size_t row=index==1?2:(index==2?3:6);c=context(row);f=event(index,&c);e=evidence(index,&f);
  for(size_t field=0;field<6;++field){begin(row,index,"immutable-permit-scope-and-window-no-widening");struct b6_account_evidence changed=e;
   switch(field){case 0:changed.current_permit.not_before_boot_cs=101;break;case 1:changed.current_permit.not_after_boot_cs=101;break;
   case 2:changed.current_permit.authority_uid=1001;break;case 3:changed.current_permit.output_path.text[1]='X';break;
   case 4:changed.current_permit.host_contract_sha256.text[0]='f';break;case 5:changed.current_permit.boot_id.text[0]='1';break;default:abort();}
   (void)verify(&c,&f,&changed,B6_TRANSITION_REJECTED);
  }
 }
 c=context(4);
 for(int cause=(int)B6_SYNTAX_CAUSE_COMPLETION;cause<=(int)B6_SYNTAX_CAUSE_RECOVERY;++cause){
  begin(4,6,"recognized-trigger-category-with-external-truth");
  f=event(6,&c);f.data.reconciliation_begin.cause=(enum b6_syntax_cause_value)cause;e=evidence(6,&f);
  e.observed_reconciliation_cause=f.data.reconciliation_begin.cause;(void)verify(&c,&f,&e,B6_TRANSITION_ALLOWED);
 }
 begin(3,9,"expiry-closure-validated-reason-required");c=context(3);f=event(9,&c);f.data.unused_closure.closure_reason=B6_SYNTAX_CLOSURE_EXPIRY;
 e=evidence(9,&f);e.established_closure_reason=B6_SYNTAX_CLOSURE_EXPIRY;(void)verify(&c,&f,&e,B6_TRANSITION_ALLOWED);
 begin(3,9,"recognized-reason-with-wrong-observed-disposition-rejects");e.established_closure_reason=B6_SYNTAX_CLOSURE_WITHDRAWAL;(void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 for(size_t index=7;index<=8;++index){c=context(7);f=event(index,&c);e=evidence(index,&f);
  begin(7,index,"changed-report-reference-does-not-match-validated-report");e.reconciliation_report_sha256.text[0]='f';(void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 }
 begin(5,4,"PID-device-zero-and-valid-native-fields-do-not-authenticate");c=context(5);f=event(4,&c);e=evidence(4,&f);
 e.dispositions.authenticated_process_instance_ancestry=B6_EXTERNAL_UNVALIDATED;(void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
 begin(5,4,"valid-device-inode-do-not-prove-output");e.dispositions.authenticated_process_instance_ancestry=B6_EXTERNAL_VALIDATED;
 e.dispositions.output_provenance=B6_EXTERNAL_UNVALIDATED;(void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
 begin(4,10,"valid-recovery-reference-does-not-validate-authority");c=context(4);f=event(10,&c);e=evidence(10,&f);
 e.dispositions.recovery_authority=B6_EXTERNAL_UNVALIDATED;(void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
 begin(6,5,"current-disabled-execution-cannot-be-enabled-by-pure-input-default");c=context(6);f=event(5,&c);e=evidence(5,&f);
 e.dispositions.separate_future_enabling_authority=B6_EXTERNAL_INVALID;(void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 begin(0,0,"null-inputs-no-stale-usable-result");
 CHECK(b6_account_transition_validate(NULL,&f,&e).disposition==B6_TRANSITION_REJECTED);
 CHECK(b6_account_transition_validate(&c,NULL,&e).disposition==B6_TRANSITION_REJECTED);
 CHECK(b6_account_transition_validate(&c,&f,NULL).disposition==B6_TRANSITION_REJECTED);
}
static void semantic_supplements(void)
{
 for(size_t index=0;index<11;++index){size_t row=0;while(!(legal[row]&(1u<<index)))++row;
  struct b6_account_context source=context(row);struct b6_syntax_event_frame f=event(index,&source);
  struct b6_account_evidence e=evidence(index,&f);
  struct b6_account_context c=b6_account_transition_validate(&source,&f,&e).next;
  if(index==0)h(&c.retained_history_sha256);
  e.operation=B6_ACCOUNT_ACK_COMMITTED;e.committed_event=f;e.committed_payload_sha256=e.event_payload_sha256;
  begin(row,index,"same-generation-changed-event-specific-payload-is-not-ack");
  struct b6_syntax_event_frame changed=f;
  switch(index){case 0:changed.common.evidence.kind=B6_SYNTAX_EVIDENCE_DIGEST;h(&changed.common.evidence.digest);break;
  case 1:changed.data.registration.grant_reference.text[0]='X';break;
  case 2:changed.data.reservation.permit_snapshot_sha256.text[0]='f';break;
  case 3:changed.data.preparation.supervisor_executable_sha256.text[0]='f';break;
  case 4:++changed.data.binding.output_device;break;
  case 5:changed.data.admission.admission_permit_sha256.text[0]='f';break;
  case 6:changed.data.reconciliation_begin.cause=B6_SYNTAX_CAUSE_FAILURE;break;
  case 7:changed.data.reconciled_failed.reconciliation_report_sha256.text[0]='f';break;
  case 8:changed.data.qualification_accepted.reconciliation_report_sha256.text[0]='f';break;
  case 9:changed.data.unused_closure.closure_reason=B6_SYNTAX_CLOSURE_EXPIRY;break;
  case 10:changed.data.retirement.recovery_authority_reference.text[0]='X';break;default:abort();}
  struct b6_account_evidence modified=e;bind_proof(&modified,&changed);
  (void)verify(&c,&changed,&modified,B6_TRANSITION_REJECTED);
  begin(row,index,"different-event-type-not-same-committed-semantic-request");
  changed=event(index==10?9u:index+1u,&c);modified=e;bind_proof(&modified,&changed);
  (void)verify(&c,&changed,&modified,B6_TRANSITION_REJECTED);
  begin(row,index,"missing-payload-identity-does-not-become-valid-zero");
  modified=e;memset(&modified.event_payload_sha256,0,sizeof modified.event_payload_sha256);
  (void)verify(&c,&f,&modified,B6_TRANSITION_UNDETERMINED);
 }
 for(size_t index=7;index<=8;++index)for(int result=0;result<3;++result){
  begin(7,index,"unavailable-report-truth-preserves-unreconciled-state");
  struct b6_account_context c=context(7);struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
  e.reconciliation_result=result==0?B6_SYNTAX_NO_RESULT:(result==1?B6_SYNTAX_RESULT_INDETERMINATE:(enum b6_syntax_report_result)99);
  (void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
 }
 for(size_t index=7;index<=8;++index){begin(7,index,"terminal-reconciliation-can-finish-after-permit-withdrawal");
  struct b6_account_context c=context(7);struct b6_syntax_event_frame f=event(index,&c);struct b6_account_evidence e=evidence(index,&f);
  e.dispositions.current_human_permit=B6_EXTERNAL_INVALID;(void)verify(&c,&f,&e,B6_TRANSITION_ALLOWED);
 }
 for(size_t tag=0;tag<3;++tag){begin(4,6,"unknown-reason-version-tags-fail-closed-before-semantic-layer");
  struct b6_account_context c=context(tag==0?0u:(tag==1?4u:3u));struct b6_syntax_event_frame f=event(tag==0?0u:(tag==1?6u:9u),&c);
  struct b6_account_evidence e=evidence(tag==0?0u:(tag==1?6u:9u),&f);
  if(tag==0)f.data.genesis.format_version=(enum b6_syntax_format_version)99;
  if(tag==1)f.data.reconciliation_begin.cause=(enum b6_syntax_cause_value)99;
  if(tag==2)f.data.unused_closure.closure_reason=(enum b6_syntax_closure_value)99;
  (void)verify(&c,&f,&e,B6_TRANSITION_REJECTED);
 }
 for(size_t field=0;field<7;++field){begin(3,2,"incomplete-or-mismatched-scope-does-not-become-authority");
  struct b6_account_context c=context(3);struct b6_syntax_event_frame f=event(2,&c);struct b6_account_evidence e=evidence(2,&f);
  switch(field){case 0:c.immutable_scope.authority_uid=0;break;case 1:c.immutable_scope.commission_id.text[0]='X';break;
  case 2:memset(&c.commission_scope_sha256,0,sizeof c.commission_scope_sha256);break;
  case 3:memset(&c.immutable_scope.execution_principal_pool_sha256,0,sizeof c.immutable_scope.execution_principal_pool_sha256);break;
  case 4:memset(&c.immutable_scope.output_path,0,sizeof c.immutable_scope.output_path);break;
  case 5:memset(&c.retained_history_sha256,0,sizeof c.retained_history_sha256);break;
  case 6:memset(&c.immutable_scope.grant_reference,'X',sizeof c.immutable_scope.grant_reference);break;default:abort();}
  (void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
 }
 begin(3,2,"current-permit-may-narrow-but-not-extend-registered-window");
 struct b6_account_context c=context(3);struct b6_syntax_event_frame f=event(2,&c);struct b6_account_evidence e=evidence(2,&f);
 e.current_permit.not_before_boot_cs=1;e.current_permit.not_after_boot_cs=99;(void)verify(&c,&f,&e,B6_TRANSITION_ALLOWED);
 begin(4,6,"recovery-begin-needs-old-keeper-inability-not-just-reference");
 c=context(4);f=event(6,&c);f.data.reconciliation_begin.cause=B6_SYNTAX_CAUSE_RECOVERY;e=evidence(6,&f);
 e.observed_reconciliation_cause=B6_SYNTAX_CAUSE_RECOVERY;e.dispositions.old_actor_cannot_act=B6_EXTERNAL_UNVALIDATED;
 (void)verify(&c,&f,&e,B6_TRANSITION_UNDETERMINED);
}
int main(void)
{for(size_t i=0;i<11;++i)printf("EVENT\t%s\n",names[i]);
 matrix();external_gates();duplicates();identity_and_boundaries();semantic_supplements();printf("PASS cases=%zu assertions=%zu\n",cases,assertions);return 0;}
