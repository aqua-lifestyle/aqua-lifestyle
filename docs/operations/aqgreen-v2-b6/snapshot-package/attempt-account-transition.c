#include "attempt-account-transition.h"
#include <string.h>

/* Branch authority aliases are bound in PURE-TRANSITION-AUTHORITY-TO-CODE.tsv.
   S = accepted semantic clarification; A = ATTEMPT; V = AUTH V2. */
static int text_equal(const char *a,const char *b,size_t n)
{ /* Bounded typed strings, no unterminated read or padding-dependent equality. */
 for(size_t i=0;i<n;++i) { if(a[i]!=b[i]) return 0; if(a[i]=='\0') return 1; }
 return 0;
}
#define EQ(a,b) text_equal((a).text,(b).text,sizeof (a).text)
static int digest_known(const struct b6_syntax_hex *h)
{ struct b6_syntax_hex checked;return h->text[64]==0
  && b6_syntax_hex((const unsigned char *)h->text,64,&checked)==B6_SYNTAX_OK; }
static int uuid_known(const struct b6_syntax_uuid *u)
{ struct b6_syntax_uuid checked;return u->text[36]==0
  && b6_syntax_uuid((const unsigned char *)u->text,36,&checked)==B6_SYNTAX_OK; }
static size_t bounded_length(const char *s,size_t cap)
{ size_t n=0;while(n<cap && s[n])++n;return n; }
static int scope_known(const struct b6_syntax_scope *s)
{ /* Reuse closed scalar types; no object load or evidence truth check. */
 struct b6_syntax_ref ref;struct b6_syntax_path path;
 return uuid_known(&s->grant_boot_id) && digest_known(&s->execution_principal_pool_sha256)
  && digest_known(&s->materialization_manifest_sha256) && digest_known(&s->qualification_procedure_sha256)
  && digest_known(&s->host_contract_sha256) && s->authority_uid>0 && s->authority_uid<UINT32_MAX
  && s->authority_gid>0 && s->authority_gid<UINT32_MAX && s->not_before_boot_cs<s->not_after_boot_cs
  && s->not_after_boot_cs<=(uint64_t)INT64_MAX
  && b6_syntax_ref((const unsigned char *)s->grant_reference.text,bounded_length(s->grant_reference.text,sizeof s->grant_reference.text),&ref)==B6_SYNTAX_OK
  && b6_syntax_path((const unsigned char *)s->output_path.text,bounded_length(s->output_path.text,sizeof s->output_path.text),&path)==B6_SYNTAX_OK;
}
static int fact_known(enum b6_account_fact f)
{ return f==B6_ACCOUNT_FACT_NO || f==B6_ACCOUNT_FACT_YES; }
static enum b6_transition_disposition required(enum b6_external_disposition d)
{ /* S §6: missing truth != proven false. Unknown enum never means validated. */
 if(d==B6_EXTERNAL_VALIDATED) return B6_TRANSITION_ALLOWED;
 if(d==B6_EXTERNAL_INVALID) return B6_TRANSITION_REJECTED;
 return B6_TRANSITION_UNDETERMINED;
}
static struct b6_account_transition_result empty_result(enum b6_transition_disposition d)
{ struct b6_account_transition_result r; memset(&r,0,sizeof r);r.disposition=d;return r; }
static int event_equal(const struct b6_syntax_event_frame *a,const struct b6_syntax_event_frame *b)
{ /* S §5 exact typed fields; canonical grammar gives one byte representation.
     Ignore C padding/inactive union storage. No codec, hashing or IO here. */
 if(a->state!=B6_SYNTAX_GRAMMAR_ONLY || b->state!=B6_SYNTAX_GRAMMAR_ONLY
  || a->event_type!=b->event_type || !EQ(a->common.store_id,b->common.store_id)
  || a->common.event_seq!=b->common.event_seq || a->common.previous_event.kind!=b->common.previous_event.kind
  || !EQ(a->common.commission_id,b->common.commission_id) || a->common.attempt_number!=b->common.attempt_number
  || !EQ(a->common.writer_boot_id,b->common.writer_boot_id) || a->common.writer_pid!=b->common.writer_pid
  || a->common.writer_starttime!=b->common.writer_starttime || a->common.recorded_boot_cs!=b->common.recorded_boot_cs
  || a->common.evidence.kind!=b->common.evidence.kind) return 0;
 if(a->common.previous_event.kind==B6_SYNTAX_HEAD_DIGEST && !EQ(a->common.previous_event.digest,b->common.previous_event.digest)) return 0;
 if(a->common.evidence.kind==B6_SYNTAX_EVIDENCE_DIGEST && !EQ(a->common.evidence.digest,b->common.evidence.digest)) return 0;
 switch(a->event_type) {
 case B6_SYNTAX_GENESIS:return a->data.genesis.format_version==b->data.genesis.format_version;
 case B6_SYNTAX_REGISTER_COMMISSION:return EQ(a->data.registration.grant_reference,b->data.registration.grant_reference)
  && EQ(a->data.registration.enrollment_permit_sha256,b->data.registration.enrollment_permit_sha256)
  && EQ(a->data.registration.commission_scope_sha256,b->data.registration.commission_scope_sha256)
  && EQ(a->data.registration.resource_key,b->data.registration.resource_key);
 case B6_SYNTAX_RESERVE_ATTEMPT:return EQ(a->data.reservation.permit_snapshot_sha256,b->data.reservation.permit_snapshot_sha256)
  && EQ(a->data.reservation.observation_snapshot_sha256,b->data.reservation.observation_snapshot_sha256)
  && EQ(a->data.reservation.native_preparation_descriptor_sha256,b->data.reservation.native_preparation_descriptor_sha256);
 case B6_SYNTAX_PREPARATION_INTENT:return EQ(a->data.preparation.supervisor_executable_sha256,b->data.preparation.supervisor_executable_sha256);
 case B6_SYNTAX_BIND_NATIVE:return EQ(a->data.binding.native_transaction_id,b->data.binding.native_transaction_id)
  && a->data.binding.supervisor_pid==b->data.binding.supervisor_pid && a->data.binding.supervisor_starttime==b->data.binding.supervisor_starttime
  && a->data.binding.bash_pid==b->data.binding.bash_pid && a->data.binding.bash_starttime==b->data.binding.bash_starttime
  && a->data.binding.output_device==b->data.binding.output_device && a->data.binding.output_inode==b->data.binding.output_inode;
 case B6_SYNTAX_ADMIT_EXECUTION:return EQ(a->data.admission.admission_permit_sha256,b->data.admission.admission_permit_sha256)
  && EQ(a->data.admission.admission_observation_sha256,b->data.admission.admission_observation_sha256)
  && EQ(a->data.admission.admission_host_evidence_sha256,b->data.admission.admission_host_evidence_sha256);
 case B6_SYNTAX_BEGIN_RECONCILIATION:return a->data.reconciliation_begin.cause==b->data.reconciliation_begin.cause;
 case B6_SYNTAX_RECONCILED_FAILED:return EQ(a->data.reconciled_failed.reconciliation_report_sha256,b->data.reconciled_failed.reconciliation_report_sha256);
 case B6_SYNTAX_QUALIFICATION_ACCEPTED:return EQ(a->data.qualification_accepted.reconciliation_report_sha256,b->data.qualification_accepted.reconciliation_report_sha256);
 case B6_SYNTAX_CLOSE_UNUSED_COMMISSION:return a->data.unused_closure.closure_reason==b->data.unused_closure.closure_reason;
 case B6_SYNTAX_RETIRE_INDETERMINATE:return EQ(a->data.retirement.recovery_authority_reference,b->data.retirement.recovery_authority_reference)
  && EQ(a->data.retirement.recovery_report_sha256,b->data.retirement.recovery_report_sha256);
 default:return 0;
 }
}
static int permit_scope(const struct b6_syntax_permit *p,const struct b6_syntax_scope *s)
{ /* V §3/§8; IPC §AG: immutable mapping + permitted narrowing, no current clock. */
 return EQ(p->qualification_id,s->commission_id) && EQ(p->authority_reference,s->grant_reference)
  && EQ(p->boot_id,s->grant_boot_id) && p->authority_uid==s->authority_uid
  && p->authority_gid==s->authority_gid && EQ(p->execution_principal_pool_sha256,s->execution_principal_pool_sha256)
  && EQ(p->output_path,s->output_path) && EQ(p->materialization_manifest_sha256,s->materialization_manifest_sha256)
  && EQ(p->qualification_procedure_sha256,s->qualification_procedure_sha256)
  && EQ(p->host_contract_sha256,s->host_contract_sha256)
  && s->not_before_boot_cs<=p->not_before_boot_cs && p->not_before_boot_cs<p->not_after_boot_cs
  && p->not_after_boot_cs<=s->not_after_boot_cs;
}
static int enrollment_exact(const struct b6_syntax_permit *p,const struct b6_syntax_scope *s)
{ return permit_scope(p,s) && p->not_before_boot_cs==s->not_before_boot_cs
  && p->not_after_boot_cs==s->not_after_boot_cs; }
static int assignment_scope(const struct b6_syntax_assignment *a,const struct b6_syntax_scope *s)
{ /* V §6/§8: separate existing committed assignment, never allocation here. */
 return a->allocation_ordinal>0 && a->execution_uid>0 && a->execution_uid<UINT32_MAX
  && a->execution_uid==a->execution_gid && a->execution_uid!=s->authority_uid
  && a->execution_gid!=s->authority_gid && EQ(a->pool_sha256,s->execution_principal_pool_sha256);
}
static enum b6_transition_disposition context_valid(const struct b6_account_context *c)
{ /* S §2/§3/§6: no invented empty/default; separate fact dimensions. */
 if(!uuid_known(&c->store_id))
  return B6_TRANSITION_UNDETERMINED;
 if(c->kind==B6_ACCOUNT_PROVISIONING_CONTEXT) {
  if(c->commission!=B6_COMMISSION_UNREGISTERED || c->phase!=B6_ATTEMPT_NONE
   || c->last_reserved_attempt!=0 || c->assignment_consumption!=B6_ACCOUNT_NO_LINKED_ASSIGNMENT
   || c->preparation_intent!=B6_ACCOUNT_FACT_NO || c->native_binding!=B6_ACCOUNT_FACT_NO
   || c->execution_admission!=B6_ACCOUNT_FACT_NO) return B6_TRANSITION_UNDETERMINED;
  return B6_TRANSITION_ALLOWED;
 }
 if(c->kind!=B6_ACCOUNT_NORMAL_CONTEXT || c->format_version!=B6_SYNTAX_FORMAT_V1
  || c->commission<B6_COMMISSION_UNREGISTERED || c->commission>B6_COMMISSION_RETIRED_INDETERMINATE
  || c->phase<B6_ATTEMPT_NONE || c->phase>B6_ATTEMPT_RETIRED_UNKNOWN
  || !fact_known(c->preparation_intent) || !fact_known(c->native_binding)
  || !fact_known(c->execution_admission)) return B6_TRANSITION_UNDETERMINED;
 if(!digest_known(&c->retained_history_sha256)) return B6_TRANSITION_UNDETERMINED;
 if(c->commission!=B6_COMMISSION_UNREGISTERED && (!scope_known(&c->immutable_scope)
  || !EQ(c->commission_id,c->immutable_scope.commission_id) || !digest_known(&c->commission_scope_sha256)
  || !digest_known(&c->resource_key))) return B6_TRANSITION_UNDETERMINED;
 if(c->phase==B6_ATTEMPT_NONE) {
  if(c->last_reserved_attempt!=0 || c->assignment_consumption!=B6_ACCOUNT_NO_LINKED_ASSIGNMENT
   || c->preparation_intent!=B6_ACCOUNT_FACT_NO || c->native_binding!=B6_ACCOUNT_FACT_NO
   || c->execution_admission!=B6_ACCOUNT_FACT_NO || c->commission==B6_COMMISSION_FULFILLED
   || c->commission==B6_COMMISSION_RETIRED_INDETERMINATE) return B6_TRANSITION_UNDETERMINED;
 } else {
  if(c->last_reserved_attempt==0 || c->assignment_consumption!=B6_ACCOUNT_COMMITTED_NEVER_REUSE
   || c->commission==B6_COMMISSION_UNREGISTERED || c->commission==B6_COMMISSION_CLOSED_UNUSED)
   return B6_TRANSITION_UNDETERMINED;
  if(!assignment_scope(&c->assignment,&c->immutable_scope)) return B6_TRANSITION_UNDETERMINED;
  if(!uuid_known(&c->assignment.generation) || !uuid_known(&c->assignment.custody_store_id))
   return B6_TRANSITION_UNDETERMINED;
  if(c->native_binding==B6_ACCOUNT_FACT_YES && c->preparation_intent!=B6_ACCOUNT_FACT_YES)
   return B6_TRANSITION_UNDETERMINED;
  if(c->execution_admission==B6_ACCOUNT_FACT_YES && c->native_binding!=B6_ACCOUNT_FACT_YES)
   return B6_TRANSITION_UNDETERMINED;
  if(c->phase==B6_ATTEMPT_RESERVED_UNRECONCILED && c->execution_admission!=B6_ACCOUNT_FACT_NO)
   return B6_TRANSITION_UNDETERMINED;
  if((c->phase==B6_ATTEMPT_ADMITTED_UNRECONCILED || c->phase==B6_ATTEMPT_QUALIFICATION_ACCEPTED)
   && c->execution_admission!=B6_ACCOUNT_FACT_YES) return B6_TRANSITION_UNDETERMINED;
  if((c->commission==B6_COMMISSION_FULFILLED)!=(c->phase==B6_ATTEMPT_QUALIFICATION_ACCEPTED)
   || (c->commission==B6_COMMISSION_RETIRED_INDETERMINATE)!=(c->phase==B6_ATTEMPT_RETIRED_UNKNOWN))
   return B6_TRANSITION_UNDETERMINED;
 }
 return B6_TRANSITION_ALLOWED;
}
static int unresolved(enum b6_attempt_phase p)
{ return p==B6_ATTEMPT_RESERVED_UNRECONCILED || p==B6_ATTEMPT_ADMITTED_UNRECONCILED
  || p==B6_ATTEMPT_RECONCILING; }
static void terminal_metadata(struct b6_account_transition_result *r)
{ /* S §§3/4/7; A §K: failed attempt terminal, commission still conditional. */
 r->attempt_terminal=(r->next.phase==B6_ATTEMPT_RECONCILED_FAILED
  || r->next.phase==B6_ATTEMPT_QUALIFICATION_ACCEPTED || r->next.phase==B6_ATTEMPT_RETIRED_UNKNOWN)
  ? B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO;
 r->commission_terminal=(r->next.commission==B6_COMMISSION_FULFILLED
  || r->next.commission==B6_COMMISSION_CLOSED_UNUSED || r->next.commission==B6_COMMISSION_RETIRED_INDETERMINATE)
  ? B6_ACCOUNT_FACT_YES:B6_ACCOUNT_FACT_NO;
}
#define NEED(field) do { enum b6_transition_disposition need=required(e->dispositions.field); \
 if(need!=B6_TRANSITION_ALLOWED) return empty_result(need); } while(0)
#define PURE(condition) do { if(!(condition)) return empty_result(B6_TRANSITION_REJECTED); } while(0)
struct b6_account_transition_result b6_account_transition_validate(
 const struct b6_account_context *c,const struct b6_syntax_event_frame *f,
 const struct b6_account_evidence *e)
{
 if(!c || !f || !e) return empty_result(B6_TRANSITION_REJECTED);
 if(f->state!=B6_SYNTAX_GRAMMAR_ONLY || f->event_type<B6_SYNTAX_GENESIS
  || f->event_type>B6_SYNTAX_RETIRE_INDETERMINATE) return empty_result(B6_TRANSITION_REJECTED);
 if((f->event_type==B6_SYNTAX_GENESIS && f->data.genesis.format_version!=B6_SYNTAX_FORMAT_V1)
  || (f->event_type==B6_SYNTAX_BEGIN_RECONCILIATION && (f->data.reconciliation_begin.cause<B6_SYNTAX_CAUSE_COMPLETION
   || f->data.reconciliation_begin.cause>B6_SYNTAX_CAUSE_RECOVERY))
  || (f->event_type==B6_SYNTAX_CLOSE_UNUSED_COMMISSION && (f->data.unused_closure.closure_reason<B6_SYNTAX_CLOSURE_WITHDRAWAL
   || f->data.unused_closure.closure_reason>B6_SYNTAX_CLOSURE_EXPIRY))) return empty_result(B6_TRANSITION_REJECTED);
 if(e->dispositions.complete_current_history!=B6_EXTERNAL_VALIDATED) return empty_result(B6_TRANSITION_UNDETERMINED);
 NEED(exact_successor_lineage);
 enum b6_transition_disposition cv=context_valid(c);
 if(cv!=B6_TRANSITION_ALLOWED) return empty_result(cv);
 if(e->dispositions.store_identity_continuity!=B6_EXTERNAL_VALIDATED) return empty_result(B6_TRANSITION_UNDETERMINED);
 NEED(event_supporting_evidence);
 if(!digest_known(&e->event_payload_sha256) || !digest_known(&e->validated_payload_sha256))
  return empty_result(B6_TRANSITION_UNDETERMINED);
 PURE(EQ(e->event_payload_sha256,e->validated_payload_sha256));
 PURE(event_equal(f,&e->validated_event)); PURE(EQ(c->store_id,f->common.store_id));
 struct b6_account_transition_result r=empty_result(B6_TRANSITION_ALLOWED);
 /* S §5: separate read-only acknowledgment; no current-phase reapplication. */
 if(e->operation==B6_ACCOUNT_ACK_COMMITTED) {
  NEED(retained_committed_request_evidence); NEED(committed_evidence_durability);
  NEED(original_request_correlation);
  if(!digest_known(&e->committed_payload_sha256)) return empty_result(B6_TRANSITION_UNDETERMINED);
  PURE(EQ(e->event_payload_sha256,e->committed_payload_sha256));
  PURE(event_equal(f,&e->committed_event));
  PURE(c->kind==B6_ACCOUNT_NORMAL_CONTEXT);
  /* GENESIS belongs to the store; all other exact historical events to commission. */
  if(f->event_type!=B6_SYNTAX_GENESIS) PURE(EQ(c->commission_id,f->common.commission_id));
  r.disposition=B6_TRANSITION_ACKNOWLEDGED;r.next=c[0];r.next_context_present=B6_ACCOUNT_FACT_YES;
  r.consumption_effect=B6_CONSUMPTION_PRESERVE_COMMITTED_ASSIGNMENTS;
  r.acknowledged_payload_sha256=e->committed_payload_sha256;terminal_metadata(&r);return r;
 }
 PURE(e->operation==B6_ACCOUNT_APPLY_FRESH);
 if(f->event_type!=B6_SYNTAX_GENESIS) {
  PURE(c->kind==B6_ACCOUNT_NORMAL_CONTEXT && EQ(c->commission_id,f->common.commission_id));
  if(f->event_type==B6_SYNTAX_REGISTER_COMMISSION || f->event_type==B6_SYNTAX_CLOSE_UNUSED_COMMISSION)
   PURE(f->common.attempt_number==0);
  else if(f->event_type!=B6_SYNTAX_RESERVE_ATTEMPT)
   PURE(f->common.attempt_number>0 && f->common.attempt_number==c->last_reserved_attempt);
 }
 r.next=c[0];
 switch(f->event_type) {
 case B6_SYNTAX_GENESIS: /* S §2/§6; A §H/§I. */
  PURE(c->kind==B6_ACCOUNT_PROVISIONING_CONTEXT && f->common.attempt_number==0
   && f->common.event_seq==1 && f->data.genesis.format_version==B6_SYNTAX_FORMAT_V1);
  NEED(provisioning_authority_no_prior_obligation);
  r.next.kind=B6_ACCOUNT_NORMAL_CONTEXT;r.next.format_version=f->data.genesis.format_version;
  r.effect=B6_EFFECT_INITIAL_STORE;break;
 case B6_SYNTAX_REGISTER_COMMISSION: /* S §2/§6; V §3; A §J. */
  PURE(c->commission==B6_COMMISSION_UNREGISTERED && c->phase==B6_ATTEMPT_NONE);
  NEED(enrollment_human_permit_scope);
  if(!scope_known(&e->enrollment_scope)) return empty_result(B6_TRANSITION_UNDETERMINED);
  PURE(EQ(f->common.commission_id,e->enrollment_scope.commission_id)
   && EQ(f->data.registration.grant_reference,e->enrollment_scope.grant_reference)
   && EQ(f->data.registration.commission_scope_sha256,e->enrollment_scope_sha256)
   && EQ(f->data.registration.resource_key,e->enrollment_resource_key)
   && enrollment_exact(&e->current_permit,&e->enrollment_scope));
  r.next.commission=B6_COMMISSION_REGISTERED;r.next.immutable_scope=e->enrollment_scope;
  r.next.commission_scope_sha256=e->enrollment_scope_sha256;r.next.resource_key=e->enrollment_resource_key;
  r.effect=B6_EFFECT_REGISTER_SCOPE;break;
 case B6_SYNTAX_RESERVE_ATTEMPT: /* S §2/§6/§7; A §O/§R; V §6. */
  PURE(c->commission==B6_COMMISSION_REGISTERED
   && (c->phase==B6_ATTEMPT_NONE || c->phase==B6_ATTEMPT_RECONCILED_FAILED));
  PURE(c->last_reserved_attempt!=UINT64_MAX && f->common.attempt_number==c->last_reserved_attempt+1u);
  NEED(deliberate_invocation);NEED(current_human_permit);NEED(current_host_observation);
  NEED(source_output_custody);NEED(resource_conflict_disposition);NEED(exact_hold_preparation_assignment);
  if(c->phase==B6_ATTEMPT_RECONCILED_FAILED) {
   NEED(prior_custody_released);
   PURE(!EQ(c->assignment.generation,e->reserved_assignment.generation)
    && c->assignment.execution_uid!=e->reserved_assignment.execution_uid);
  }
  PURE(permit_scope(&e->current_permit,&c->immutable_scope)
   && assignment_scope(&e->reserved_assignment,&c->immutable_scope));
  if(!uuid_known(&e->reserved_assignment.generation) || !uuid_known(&e->reserved_assignment.custody_store_id))
   return empty_result(B6_TRANSITION_UNDETERMINED);
  r.next.phase=B6_ATTEMPT_RESERVED_UNRECONCILED;r.next.last_reserved_attempt=f->common.attempt_number;
  r.next.preparation_intent=B6_ACCOUNT_FACT_NO;r.next.native_binding=B6_ACCOUNT_FACT_NO;
  r.next.execution_admission=B6_ACCOUNT_FACT_NO;memset(&r.next.binding,0,sizeof r.next.binding);
  r.next.original_attempt_permit=e->current_permit;r.next.assignment=e->reserved_assignment;
  r.next.assignment_consumption=B6_ACCOUNT_COMMITTED_NEVER_REUSE;
  r.next.preparation_descriptor_sha256=f->data.reservation.native_preparation_descriptor_sha256;
  r.effect=B6_EFFECT_RESERVE_GENERATION;r.generation_effect=B6_GENERATION_NEW_POSITIVE_ATTEMPT;break;
 case B6_SYNTAX_PREPARATION_INTENT: /* S §3 exact intent row. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && c->phase==B6_ATTEMPT_RESERVED_UNRECONCILED
   && c->preparation_intent==B6_ACCOUNT_FACT_NO && c->native_binding==B6_ACCOUNT_FACT_NO
   && c->execution_admission==B6_ACCOUNT_FACT_NO);
  NEED(exact_hold_preparation_assignment);NEED(preparation_plan_source);
  r.next.preparation_intent=B6_ACCOUNT_FACT_YES;r.effect=B6_EFFECT_PREPARATION_MAY_EXIST;break;
 case B6_SYNTAX_BIND_NATIVE: /* S §3; A §L.4-6; accepted ROOT §2/§3. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && c->phase==B6_ATTEMPT_RESERVED_UNRECONCILED
   && c->preparation_intent==B6_ACCOUNT_FACT_YES && c->native_binding==B6_ACCOUNT_FACT_NO
   && c->execution_admission==B6_ACCOUNT_FACT_NO);
  NEED(authenticated_process_instance_ancestry);NEED(output_provenance);
  NEED(assignment_witness_exposed);NEED(post_request_k_root);NEED(current_live_binding_context);
  PURE(EQ(f->common.writer_boot_id,c->original_attempt_permit.boot_id));
  r.next.native_binding=B6_ACCOUNT_FACT_YES;r.next.binding=f->data.binding;
  r.effect=B6_EFFECT_ATTACH_NATIVE_BINDING;break;
 case B6_SYNTAX_ADMIT_EXECUTION: /* S §3 future abstract edge, never runtime grant. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && c->phase==B6_ATTEMPT_RESERVED_UNRECONCILED
   && c->preparation_intent==B6_ACCOUNT_FACT_YES && c->native_binding==B6_ACCOUNT_FACT_YES
   && c->execution_admission==B6_ACCOUNT_FACT_NO);
  NEED(current_human_permit);NEED(current_host_observation);NEED(final_admission_predicates);
  NEED(separate_future_enabling_authority);
  PURE(permit_scope(&e->current_permit,&c->immutable_scope)
   && EQ(f->common.writer_boot_id,c->original_attempt_permit.boot_id));
  r.next.execution_admission=B6_ACCOUNT_FACT_YES;r.next.phase=B6_ATTEMPT_ADMITTED_UNRECONCILED;
  r.effect=B6_EFFECT_ADMISSION_MAY_HAVE_EXECUTED;break;
 case B6_SYNTAX_BEGIN_RECONCILIATION: /* S §3 begin row and no fresh repeated begin. */
  PURE(c->commission==B6_COMMISSION_REGISTERED
   && (c->phase==B6_ATTEMPT_RESERVED_UNRECONCILED || c->phase==B6_ATTEMPT_ADMITTED_UNRECONCILED));
  NEED(reconciliation_trigger);
  PURE(f->data.reconciliation_begin.cause==e->observed_reconciliation_cause);
  if(f->data.reconciliation_begin.cause==B6_SYNTAX_CAUSE_RECOVERY) {
   NEED(recovery_authority);NEED(old_actor_cannot_act);
  }
  r.next.phase=B6_ATTEMPT_RECONCILING;r.effect=B6_EFFECT_BEGIN_RECONCILIATION;break;
 case B6_SYNTAX_RECONCILED_FAILED: /* S §3 direct/BEGIN; A §I/§O/§T.5. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && unresolved(c->phase));
  if(e->reconciliation_result==B6_SYNTAX_RESULT_INDETERMINATE || e->reconciliation_result<B6_SYNTAX_RESULT_ACCEPTED
   || e->reconciliation_result>B6_SYNTAX_RESULT_INDETERMINATE)
   return empty_result(B6_TRANSITION_UNDETERMINED);
  NEED(known_failure_nonadmission);NEED(process_output_effect_identity_reconciled);NEED(old_actor_cannot_act);
  PURE(e->reconciliation_result==B6_SYNTAX_RESULT_RECONCILED_FAILED
   && EQ(f->data.reconciled_failed.reconciliation_report_sha256,e->reconciliation_report_sha256));
  r.next.phase=B6_ATTEMPT_RECONCILED_FAILED;r.effect=B6_EFFECT_OLD_ATTEMPT_FAILED;break;
 case B6_SYNTAX_QUALIFICATION_ACCEPTED: /* S §3; A §I/§N: not Gate-Q by label. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && c->execution_admission==B6_ACCOUNT_FACT_YES
   && (c->phase==B6_ATTEMPT_ADMITTED_UNRECONCILED || c->phase==B6_ATTEMPT_RECONCILING));
  if(e->reconciliation_result==B6_SYNTAX_RESULT_INDETERMINATE || e->reconciliation_result<B6_SYNTAX_RESULT_ACCEPTED
   || e->reconciliation_result>B6_SYNTAX_RESULT_INDETERMINATE)
   return empty_result(B6_TRANSITION_UNDETERMINED);
  NEED(accepted_procedure_wait_terminal_host_continuity);NEED(process_output_effect_identity_reconciled);
  NEED(old_actor_cannot_act);
  PURE(e->reconciliation_result==B6_SYNTAX_RESULT_ACCEPTED
   && EQ(f->data.qualification_accepted.reconciliation_report_sha256,e->reconciliation_report_sha256));
  r.next.phase=B6_ATTEMPT_QUALIFICATION_ACCEPTED;r.next.commission=B6_COMMISSION_FULFILLED;
  r.effect=B6_EFFECT_COMMISSION_FULFILLED;break;
 case B6_SYNTAX_CLOSE_UNUSED_COMMISSION: /* S §4: never-reserved only, not English inference. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && c->phase==B6_ATTEMPT_NONE
   && c->last_reserved_attempt==0);
  NEED(withdrawal_or_expiry);NEED(resource_conflict_disposition);
  PURE(f->data.unused_closure.closure_reason==e->established_closure_reason);
  r.next.commission=B6_COMMISSION_CLOSED_UNUSED;r.effect=B6_EFFECT_UNUSED_COMMISSION_CLOSED;break;
 case B6_SYNTAX_RETIRE_INDETERMINATE: /* S §3; A §T.6 preserves UNKNOWN. */
  PURE(c->commission==B6_COMMISSION_REGISTERED && unresolved(c->phase));
  NEED(recovery_authority);NEED(old_actor_cannot_act);NEED(recovery_safe_resource_disposition);
  if(e->reconciliation_result<B6_SYNTAX_RESULT_ACCEPTED || e->reconciliation_result>B6_SYNTAX_RESULT_INDETERMINATE)
   return empty_result(B6_TRANSITION_UNDETERMINED);
  PURE(e->reconciliation_result==B6_SYNTAX_RESULT_INDETERMINATE);
  r.next.commission=B6_COMMISSION_RETIRED_INDETERMINATE;r.next.phase=B6_ATTEMPT_RETIRED_UNKNOWN;
  r.effect=B6_EFFECT_COMMISSION_RETIRED_UNKNOWN;break;
 default: return empty_result(B6_TRANSITION_REJECTED); /* Closed event enum; no fallbacks. */
 }
 r.next_context_present=B6_ACCOUNT_FACT_YES;
 r.consumption_effect=B6_CONSUMPTION_PRESERVE_COMMITTED_ASSIGNMENTS;terminal_metadata(&r);
 return r;
}
