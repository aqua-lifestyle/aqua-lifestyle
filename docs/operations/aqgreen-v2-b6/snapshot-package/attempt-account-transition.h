#ifndef B6_ATTEMPT_ACCOUNT_TRANSITION_H
#define B6_ATTEMPT_ACCOUNT_TRANSITION_H
#include "attempt-account-schema.h"

/* Pure model of accepted SEMANTIC §§2-7 / ATTEMPT §§H-T. These C tags are
   representations of separate facts, not extra persisted states or wire schemas. */
enum b6_account_context_kind { B6_ACCOUNT_CONTEXT_UNESTABLISHED=0,
 B6_ACCOUNT_PROVISIONING_CONTEXT, B6_ACCOUNT_NORMAL_CONTEXT,
 B6_ACCOUNT_UNKNOWN_OR_DAMAGED };
enum b6_commission_disposition { B6_COMMISSION_UNESTABLISHED=0,
 B6_COMMISSION_UNREGISTERED, B6_COMMISSION_REGISTERED,
 B6_COMMISSION_FULFILLED, B6_COMMISSION_CLOSED_UNUSED,
 B6_COMMISSION_RETIRED_INDETERMINATE };
enum b6_attempt_phase { B6_ATTEMPT_UNESTABLISHED=0, B6_ATTEMPT_NONE,
 B6_ATTEMPT_RESERVED_UNRECONCILED, B6_ATTEMPT_ADMITTED_UNRECONCILED,
 B6_ATTEMPT_RECONCILING, B6_ATTEMPT_RECONCILED_FAILED,
 B6_ATTEMPT_QUALIFICATION_ACCEPTED, B6_ATTEMPT_RETIRED_UNKNOWN };
enum b6_account_fact { B6_ACCOUNT_FACT_UNKNOWN=0,
 B6_ACCOUNT_FACT_NO, B6_ACCOUNT_FACT_YES };
enum b6_account_consumption { B6_ACCOUNT_ASSIGNMENT_UNESTABLISHED=0,
 B6_ACCOUNT_NO_LINKED_ASSIGNMENT, B6_ACCOUNT_COMMITTED_NEVER_REUSE };

struct b6_account_context {
 enum b6_account_context_kind kind;
 struct b6_syntax_uuid store_id;
 enum b6_syntax_format_version format_version;
 enum b6_commission_disposition commission;
 struct b6_syntax_id commission_id;
 struct b6_syntax_scope immutable_scope;
 struct b6_syntax_hex commission_scope_sha256, resource_key;
 /* Full older history is an explicit validated input dependency, not replay here.
    This retained identity is never rewritten by a pure effect. */
 struct b6_syntax_hex retained_history_sha256;
 enum b6_attempt_phase phase;
 uint64_t last_reserved_attempt;
 enum b6_account_fact preparation_intent, native_binding, execution_admission;
 struct b6_syntax_native_binding binding;
 struct b6_syntax_permit original_attempt_permit;
 struct b6_syntax_hex preparation_descriptor_sha256;
 enum b6_account_consumption assignment_consumption;
 struct b6_syntax_assignment assignment;
};

/* Each named field is a disposition from a SEPARATE validated evidence producer.
   Zero means unavailable/unvalidated, never false or permitted. No adapter exists
   here. A caller can fabricate these C values: this API supplies no authenticity. */
enum b6_external_disposition { B6_EXTERNAL_UNVALIDATED=0,
 B6_EXTERNAL_VALIDATED, B6_EXTERNAL_INVALID };
struct b6_account_external_dispositions {
 enum b6_external_disposition complete_current_history;
 enum b6_external_disposition exact_successor_lineage;
 enum b6_external_disposition store_identity_continuity;
 enum b6_external_disposition event_supporting_evidence;
 enum b6_external_disposition provisioning_authority_no_prior_obligation;
 enum b6_external_disposition enrollment_human_permit_scope;
 enum b6_external_disposition deliberate_invocation;
 enum b6_external_disposition current_human_permit;
 enum b6_external_disposition current_host_observation;
 enum b6_external_disposition source_output_custody;
 enum b6_external_disposition resource_conflict_disposition;
 enum b6_external_disposition exact_hold_preparation_assignment;
 enum b6_external_disposition prior_custody_released;
 enum b6_external_disposition preparation_plan_source;
 enum b6_external_disposition authenticated_process_instance_ancestry;
 enum b6_external_disposition output_provenance;
 enum b6_external_disposition assignment_witness_exposed;
 enum b6_external_disposition post_request_k_root;
 enum b6_external_disposition current_live_binding_context;
 enum b6_external_disposition final_admission_predicates;
 enum b6_external_disposition separate_future_enabling_authority;
 enum b6_external_disposition reconciliation_trigger;
 enum b6_external_disposition known_failure_nonadmission;
 enum b6_external_disposition process_output_effect_identity_reconciled;
 enum b6_external_disposition old_actor_cannot_act;
 enum b6_external_disposition accepted_procedure_wait_terminal_host_continuity;
 enum b6_external_disposition withdrawal_or_expiry;
 enum b6_external_disposition recovery_authority;
 enum b6_external_disposition recovery_safe_resource_disposition;
 enum b6_external_disposition retained_committed_request_evidence;
 enum b6_external_disposition committed_evidence_durability;
 enum b6_external_disposition original_request_correlation;
};
enum b6_account_operation { B6_ACCOUNT_NO_OPERATION=0,
 B6_ACCOUNT_APPLY_FRESH, B6_ACCOUNT_ACK_COMMITTED };
struct b6_account_evidence {
 enum b6_account_operation operation;
 struct b6_account_external_dispositions dispositions;
 /* Exact candidate payload identity from the already closed grammar boundary;
    validated_event/payload are the evidence producer's matching subject snapshot.
    This module neither hashes nor serializes/parses any record. */
 struct b6_syntax_hex event_payload_sha256;
 struct b6_syntax_hex validated_payload_sha256;
 struct b6_syntax_event_frame validated_event;
 struct b6_syntax_permit current_permit;
 struct b6_syntax_scope enrollment_scope;
 struct b6_syntax_hex enrollment_scope_sha256, enrollment_resource_key;
 struct b6_syntax_assignment reserved_assignment;
 /* Report truth is validated externally; these are only correlation/result facts. */
 enum b6_syntax_report_result reconciliation_result;
 struct b6_syntax_hex reconciliation_report_sha256;
 enum b6_syntax_cause_value observed_reconciliation_cause;
 enum b6_syntax_closure_value established_closure_reason;
 /* Only used for an explicit read-only historical acknowledgment operation.
    This original event is supplied by independently validated retained history. */
 struct b6_syntax_event_frame committed_event;
 struct b6_syntax_hex committed_payload_sha256;
};

enum b6_transition_disposition { B6_TRANSITION_NO_RESULT=0,
 B6_TRANSITION_ALLOWED, B6_TRANSITION_REJECTED,
 B6_TRANSITION_UNDETERMINED, B6_TRANSITION_ACKNOWLEDGED };
enum b6_transition_effect { B6_TRANSITION_NO_EFFECT=0,
 B6_EFFECT_INITIAL_STORE, B6_EFFECT_REGISTER_SCOPE, B6_EFFECT_RESERVE_GENERATION,
 B6_EFFECT_PREPARATION_MAY_EXIST, B6_EFFECT_ATTACH_NATIVE_BINDING,
 B6_EFFECT_ADMISSION_MAY_HAVE_EXECUTED, B6_EFFECT_BEGIN_RECONCILIATION,
 B6_EFFECT_OLD_ATTEMPT_FAILED, B6_EFFECT_COMMISSION_FULFILLED,
 B6_EFFECT_UNUSED_COMMISSION_CLOSED, B6_EFFECT_COMMISSION_RETIRED_UNKNOWN };
enum b6_generation_effect { B6_GENERATION_NO_CHANGE=0,
 B6_GENERATION_NEW_POSITIVE_ATTEMPT };
enum b6_consumption_effect { B6_CONSUMPTION_NO_EFFECT=0,
 B6_CONSUMPTION_PRESERVE_COMMITTED_ASSIGNMENTS };
struct b6_account_transition_result {
 enum b6_transition_disposition disposition;
 enum b6_account_fact next_context_present;
 struct b6_account_context next;
 enum b6_transition_effect effect;
 enum b6_generation_effect generation_effect;
 enum b6_consumption_effect consumption_effect;
 enum b6_account_fact attempt_terminal, commission_terminal;
 struct b6_syntax_hex acknowledged_payload_sha256;
};
/* No IO, wall time, boot/PID/user/environment lookup, global mutable state,
   journal sequence/chain validation, evidence authentication, commit or action.
   Return by value avoids output/input aliasing. Every rejection/undetermined
   result contains no usable next context or stale effect. All inputs immutable. */
struct b6_account_transition_result b6_account_transition_validate(
 const struct b6_account_context *, const struct b6_syntax_event_frame *,
 const struct b6_account_evidence *);
#endif
