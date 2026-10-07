#ifndef B6_ATTEMPT_ACCOUNT_REPLAY_H
#define B6_ATTEMPT_ACCOUNT_REPLAY_H
#include "attempt-account-transition.h"

/* These are in-memory claim qualifiers, NOT persisted schemas or account states.
   R = exact owner-frozen replay clarification §§2-5; A = ATTEMPT §§H-T.
   Callers separately obtain/validate protected bytes and all retained evidence.
   No evidence producer, IO, process check, clock, writer or lock exists here. */
enum b6_replay_history_scope { B6_REPLAY_SCOPE_UNESTABLISHED=0,
 B6_REPLAY_HISTORICAL_PREFIX, B6_REPLAY_COMPLETE_CURRENT_CANDIDATE };
enum b6_replay_disposition { B6_REPLAY_NO_RESULT=0,
 B6_REPLAY_HISTORICAL_STATE, B6_REPLAY_COMPLETE_CURRENT_STATE,
 B6_REPLAY_INVALID_HISTORY, B6_REPLAY_UNDETERMINED };

struct b6_replay_original_evidence {
 /* Correlation to EXACT original preceding bytes, not a generic saved PASS.
    Each disposition is supplied by a separate validated historical producer.
    Absence/invalidity remains distinct. This API cannot authenticate callers. */
 struct b6_syntax_hex preceding_prefix_sha256;
 enum b6_external_disposition original_preceding_projection;
 enum b6_external_disposition original_writer_provenance;
 enum b6_external_disposition retained_original_sources;
 enum b6_external_disposition original_durable_commit;
 /* Named role dispositions here mean valid AT THE ORIGINAL EVENT POINT (R2).
    All event/digest/scope/report/assignment correlations remain unchanged.
    complete_current_history MUST be UNVALIDATED here: it cannot claim today's
    complete history. Replay maps original_preceding_projection to the existing
    validator's history prerequisite ONLY for its original-point invocation (R3).
    ACK_COMMITTED is read-only, never a fresh durable replay record. */
 struct b6_account_evidence original_transition;
};
struct b6_replay_input {
 const unsigned char *journal;
 size_t journal_length;
 const unsigned char *store_identity;
 size_t store_identity_length;
 /* Independently validated identity snapshot of the exact supplied byte view.
    An internally valid chain/hash is not proof that later events are absent. */
 struct b6_syntax_account_store validated_store_identity;
 struct b6_syntax_hex validated_journal_sha256;
 enum b6_external_disposition protected_journal_store_custody;
 enum b6_external_disposition historical_identity_continuity;
 enum b6_external_disposition required_custody_history;
 enum b6_replay_history_scope scope;
 enum b6_external_disposition complete_current_identity_continuity;
 /* Original provisioning context, NOT permission to create/reset a store. */
 struct b6_account_context original_provisioning;
 const struct b6_replay_original_evidence *records;
 size_t record_count;
};
struct b6_replayed_record {
 struct b6_syntax_event_frame event;
 struct b6_syntax_hex payload_sha256;
 struct b6_account_transition_result transition;
};
struct b6_replayed_commission { struct b6_account_context context; };
/* Explicit caller-owned output capacity: no allocator/global host dependency.
   Input spans and workspace arrays must be readable/writable for their sizes.
   Workspace arrays must not overlap each other or ANY input. Checked here.
   Capacity exhaustion is UNDETERMINED, not an invalid contractual history. */
struct b6_replay_workspace {
 struct b6_replayed_record *records;
 size_t record_capacity;
 struct b6_replayed_commission *commissions;
 size_t commission_capacity;
};
struct b6_account_replay_result {
 enum b6_replay_disposition disposition;
 enum b6_account_fact historical_context_present, current_context_present;
 struct b6_account_context store_context;
 struct b6_syntax_hex journal_sha256, last_payload_sha256;
 uint64_t last_event_seq;
 size_t record_count, commission_count;
 const struct b6_replayed_record *records;
 const struct b6_replayed_commission *commissions;
};
/* No valid-prefix salvage from an invalid candidate. Every failure returns an
   empty result; safely validated nonaliasing workspace is cleared. Invalid span,
   capacity-overflow or alias arguments return no view without unsafe writes.
   No partial projection is published. Historical
   success cannot claim current reuse. Success retains EVERY original event and
   next context, including superseded attempts/assignments/terminal evidence.
   Borrowed successful output is valid until this workspace is reused. */
struct b6_account_replay_result b6_account_replay(
 const struct b6_replay_input *, struct b6_replay_workspace);
#endif
